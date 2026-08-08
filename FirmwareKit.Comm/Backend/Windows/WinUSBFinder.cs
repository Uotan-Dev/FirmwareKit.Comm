using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace FirmwareKit.Comm.Backend.Windows
{
    internal static class WinUSBFinder
    {
        // Heuristic: prefer the WinUSB path for Google ADB-style devices (VID_18D1).
        // Kept as a named constant so it can be lifted into configuration later.
        private const string PreferWinUsbVidPid = "vid_18d1&pid_d00d";

        private static readonly Guid[] KnownInterfaceGUIDs = new[]
        {
            // Function-interface GUIDs only. GUID_DEVINTERFACE_USB_DEVICE (a5dcbf10) is the
            // device-level interface every USB device registers; opening it yields a WinUSB
            // handle whose control transfers fail (no function interface context). A Zadig
            // WinUSB install assigns its own function GUID, which must be present here.
            new Guid("f72fe0d4-cbcb-407d-8814-9ed673d0dd6b"), // WinUSB generic
            new Guid("77395066-6C05-4B91-8071-3D7E2409546E"), // ADB
            new Guid("4D36E978-E325-11CE-BFC1-08002BE10318"), // Ports
            new Guid("88bae032-5a81-49f0-bc3d-a4ff138216d6"), // Zadig WinUSB device class
            new Guid("c73fa344-bf0b-459d-9222-a60437c27d29")  // FTDI FT2232H/FT232H WinUSB interface GUID
        };

        public static List<UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
        {
            var devices = new List<UsbDevice>();
            var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Enumerate ALL device nodes (no interface-GUID whitelist), then for each USB
            // node read its registered DeviceInterfaceGUIDs from the registry and enumerate
            // the interfaces under those GUIDs. This discovers any driver (WinUSB/Zadig/
            // libusb-win32/vendor) without a hardcoded GUID list.
            IntPtr devInfoSet = Win32API.SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero,
                Win32API.DIGCF_PRESENT | Win32API.DIGCF_ALLCLASSES);
            if (devInfoSet == (IntPtr)(-1))
            {
                UsbTrace.Log($"WinUSBFinder: SetupDiGetClassDevsW(all classes) failed err={Marshal.GetLastWin32Error()}");
                return devices;
            }

            try
            {
                var seenGuids = new HashSet<Guid>();
                uint memberIndex = 0;
                while (true)
                {
                    var devInfoData = new Win32API.SpDevInfoData();
                    devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);
                    if (!Win32API.SetupDiEnumDeviceInfo(devInfoSet, memberIndex++, ref devInfoData))
                    {
                        break;
                    }

                    string instanceId = GetDeviceInstanceId(devInfoSet, ref devInfoData);
                    if (string.IsNullOrEmpty(instanceId) ||
                        instanceId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    UsbTrace.Log($"WinUSBFinder: node [{instanceId}]");
                    foreach (Guid guid in ReadDeviceInterfaceGuids(instanceId))
                    {
                        if (!seenGuids.Add(guid))
                        {
                            continue;
                        }

                        UsbTrace.Log($"WinUSBFinder:   interface GUID {guid}");
                        EnumerateInterfacesForGuid(guid, filter, devices, uniqueKeys);
                    }
                }
            }
            finally
            {
                Win32API.SetupDiDestroyDeviceInfoList(devInfoSet);
            }

            return devices;
        }

        private static string GetDeviceInstanceId(IntPtr devInfoSet, ref Win32API.SpDevInfoData devInfoData)
        {
            uint required = 0;
            Win32API.SetupDiGetDeviceInstanceIdW(devInfoSet, ref devInfoData, null, 0, out required);
            if (required == 0)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder((int)required);
            return Win32API.SetupDiGetDeviceInstanceIdW(devInfoSet, ref devInfoData, sb, (uint)sb.Capacity, out required)
                ? sb.ToString()
                : string.Empty;
        }

        /// <summary>
        /// Reads the DeviceInterfaceGUIDs (REG_MULTI_SZ) registered for a device node under
        /// HKLM\SYSTEM\CurrentControlSet\Enum\&lt;instance&gt;\Device Parameters.
        /// <para>读取设备节点在 HKLM\SYSTEM\CurrentControlSet\Enum\&lt;instance&gt;\Device Parameters
        /// 下注册的 DeviceInterfaceGUIDs（REG_MULTI_SZ）。</para>
        /// </summary>
        private static IEnumerable<Guid> ReadDeviceInterfaceGuids(string instanceId)
        {
            var result = new List<Guid>();
            string subKey = @"SYSTEM\CurrentControlSet\Enum\" + instanceId + @"\Device Parameters";
            IntPtr key;
            if (Win32API.RegOpenKeyExW(Win32API.HKEY_LOCAL_MACHINE, subKey, 0, Win32API.KEY_READ, out key) != 0)
            {
                return result;
            }

            try
            {
                uint type = 0;
                uint size = 0;
                if (Win32API.RegQueryValueExW(key, "DeviceInterfaceGUIDs", IntPtr.Zero, out type, IntPtr.Zero, ref size) != 0 ||
                    size == 0)
                {
                    return result;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (Win32API.RegQueryValueExW(key, "DeviceInterfaceGUIDs", IntPtr.Zero, out type, buffer, ref size) == 0)
                    {
                        // REG_MULTI_SZ: NUL-separated strings ending with double NUL.
                        string multi = Marshal.PtrToStringUni(buffer, (int)size / 2) ?? string.Empty;
                        foreach (string part in multi.Split('\0'))
                        {
                            string trimmed = part.Trim();
                            if (Guid.TryParse(trimmed.Trim('{', '}'), out Guid guid))
                            {
                                result.Add(guid);
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                Win32API.RegCloseKey(key);
            }

            return result;
        }

        private static void EnumerateInterfacesForGuid(
            Guid guid,
            UsbDeviceFilter? filter,
            List<UsbDevice> devices,
            HashSet<string> uniqueKeys)
        {
            Win32API.GUID apiGuid = ToApiGuid(guid);
            IntPtr devInfo = Win32API.SetupDiGetClassDevsW(ref apiGuid, null, IntPtr.Zero,
                Win32API.DIGCF_PRESENT | Win32API.DIGCF_DEVICEINTERFACE);
            if (devInfo == (IntPtr)(-1))
            {
                UsbTrace.Log($"WinUSBFinder: SetupDiGetClassDevsW({guid}) failed err={Marshal.GetLastWin32Error()}");
                return;
            }

            try
            {
                EnumerateInterfaces(devInfo, ref apiGuid, filter, devices, uniqueKeys);
            }
            finally
            {
                Win32API.SetupDiDestroyDeviceInfoList(devInfo);
            }
        }

        private delegate bool InterfaceEnumerator(uint index, ref Win32API.SpDeviceInterfaceData interfaceData);

        private static void EnumerateInterfaces(
            IntPtr devInfo,
            ref Win32API.GUID interfaceClassGuid,
            UsbDeviceFilter? filter,
            List<UsbDevice> devices,
            HashSet<string> uniqueKeys)
        {
            // Copy to a local so the local function can capture it (ref parameters cannot
            // be captured); SetupDiEnumDeviceInterfaces only reads the GUID per call.
            Win32API.GUID guid = interfaceClassGuid;
            bool Enumerate(uint index, ref Win32API.SpDeviceInterfaceData iface)
                => Win32API.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref guid, index, ref iface);

            EnumerateInterfacesCore(devInfo, Enumerate, filter, devices, uniqueKeys, requireUsbStylePath: false);
        }

        private static void EnumerateInterfaces(
            IntPtr devInfo,
            IntPtr interfaceClassGuid,
            UsbDeviceFilter? filter,
            List<UsbDevice> devices,
            HashSet<string> uniqueKeys,
            bool requireUsbStylePath)
        {
            bool Enumerate(uint index, ref Win32API.SpDeviceInterfaceData iface)
                => Win32API.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, interfaceClassGuid, index, ref iface);

            EnumerateInterfacesCore(devInfo, Enumerate, filter, devices, uniqueKeys, requireUsbStylePath);
        }

        private static void EnumerateInterfacesCore(
            IntPtr devInfo,
            InterfaceEnumerator enumerator,
            UsbDeviceFilter? filter,
            List<UsbDevice> devices,
            HashSet<string> uniqueKeys,
            bool requireUsbStylePath)
        {
            uint index = 0;
            Win32API.SpDeviceInterfaceData interfaceData = new Win32API.SpDeviceInterfaceData();
            interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

            while (enumerator(index++, ref interfaceData))
            {
                uint detailSize = 0;
                Win32API.SetupDiGetDeviceInterfaceDetailW(devInfo, ref interfaceData, IntPtr.Zero, 0, out detailSize, IntPtr.Zero);

                IntPtr detailBuffer = Marshal.AllocHGlobal((int)detailSize);
                try
                {
                    // SP_DEVICE_INTERFACE_DETAIL_DATA_W: cbSize (DWORD, 4 bytes) is followed
                    // immediately by DevicePath (WCHAR[]) — the field offset is 4 on BOTH
                    // x86 and x64 (no pointer padding inside the struct). Using 8 on x64
                    // skips the leading "\\" of "\\?\..." and makes CreateFile fail (err=123).
                    int cbSize = IntPtr.Size == 8 ? 8 : 6;
                    int pathOffset = 4;
                    Marshal.WriteInt32(detailBuffer, cbSize);
                    uint requiredSize;
                    if (Win32API.SetupDiGetDeviceInterfaceDetailW(devInfo, ref interfaceData, detailBuffer, detailSize, out requiredSize, IntPtr.Zero))
                    {
                        string path = Marshal.PtrToStringUni(new IntPtr(detailBuffer.ToInt64() + pathOffset)) ?? "";
                        string lowerPath = path.ToLower();

                        // The all-classes sweep also returns HID/disk/network interfaces; only
                        // probe paths that look like a USB device (contain VID_&PID_).
                        if (requireUsbStylePath && !(lowerPath.Contains("vid_") && lowerPath.Contains("pid_")))
                        {
                            continue;
                        }

                        if (!PathMatchesFilter(path, filter))
                        {
                            continue;
                        }

                        // Prefer WinUSB for Google devices
                        bool isGoogleWinUsb = lowerPath.Contains(PreferWinUsbVidPid);

                        UsbDevice? device = null;
                        if (isGoogleWinUsb)
                        {
                            UsbTrace.Log($"Prefers WinUSB for Google device: {path}");
                            device = TryOpenWinUSB(path);
                        }

                        if (device == null)
                        {
                            device = ProbeDevice(path);
                        }

                        if (device != null)
                        {
                            var key = BuildDeviceKey(device);
                            if (uniqueKeys.Add(key))
                            {
                                UsbTrace.Log($"Confirmed device added: key={key} using {(device is WinUSBDevice ? "WinUSB" : "Legacy")}");
                                devices.Add(device);
                            }
                            else
                            {
                                device.Dispose();
                            }
                        }
                        else
                        {
                            UsbTrace.Log($"WinUSBFinder: ProbeDevice returned null for [{path}]");
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }

            UsbTrace.Log($"WinUSBFinder: EnumerateInterfacesCore done, lastErr={Marshal.GetLastWin32Error()}, devices={devices.Count}");
        }

        private static bool PathMatchesFilter(string path, UsbDeviceFilter? filter)
        {
            if (filter == null) return true;

            if (!string.IsNullOrWhiteSpace(filter.DevicePathContains) &&
                path.IndexOf(filter.DevicePathContains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (!filter.VendorId.HasValue && !filter.ProductId.HasValue)
            {
                return true;
            }

            (ushort? vid, ushort? pid) = TryParseVidPid(path);

            if (filter.VendorId.HasValue && (!vid.HasValue || vid.Value != filter.VendorId.Value))
            {
                return false;
            }

            if (filter.ProductId.HasValue && (!pid.HasValue || pid.Value != filter.ProductId.Value))
            {
                return false;
            }

            return true;
        }

        private static (ushort? vid, ushort? pid) TryParseVidPid(string path)
        {
            Match vidMatch = Regex.Match(path, @"VID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
            Match pidMatch = Regex.Match(path, @"PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);

            ushort? vid = null;
            ushort? pid = null;

            if (vidMatch.Success && ushort.TryParse(vidMatch.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out ushort parsedVid))
            {
                vid = parsedVid;
            }

            if (pidMatch.Success && ushort.TryParse(pidMatch.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out ushort parsedPid))
            {
                pid = parsedPid;
            }

            return (vid, pid);
        }

        private static UsbDevice? ProbeDevice(string path)
        {
            (ushort? vid, ushort? pid) = TryParseVidPid(path);
            IntPtr hDevice = Win32API.SimpleCreateHandle(path);
            if (hDevice == Win32API.INVALID_HANDLE_VALUE) return null;

            bool isLegacy = false;
            try
            {
                byte[] buffer = new byte[256];
                uint returned;
                if (Win32API.DeviceIoControl(hDevice, LegacyUsbDevice.IoGetSerialCode, null, 0, buffer, (uint)buffer.Length, out returned, IntPtr.Zero))
                {
                    isLegacy = true;
                }
            }
            finally
            {
                Win32API.CloseHandle(hDevice);
            }

            if (isLegacy)
            {
                var dev = new LegacyUsbDevice
                {
                    DevicePath = path,
                    VendorId = vid ?? 0,
                    ProductId = pid ?? 0
                };
                if (dev.CreateHandle() == 0) return dev;
                dev.Dispose();
            }
            else
            {
                var dev = new WinUSBDevice
                {
                    DevicePath = path,
                    VendorId = vid ?? 0,
                    ProductId = pid ?? 0
                };
                if (dev.CreateHandle() == 0) return dev;
                dev.Dispose();
            }

            return null;
        }

        private static UsbDevice? TryOpenWinUSB(string path)
        {
            (ushort? vid, ushort? pid) = TryParseVidPid(path);
            var dev = new WinUSBDevice
            {
                DevicePath = path,
                VendorId = vid ?? 0,
                ProductId = pid ?? 0
            };
            if (dev.CreateHandle() == 0) return dev;
            dev.Dispose();
            return null;
        }

        private static string BuildDeviceKey(UsbDevice device)
        {
            if (!string.IsNullOrWhiteSpace(device.SerialNumber))
            {
                return $"serial:{device.SerialNumber}";
            }

            // Fallback for devices that do not expose serial numbers.
            return $"path:{device.DevicePath}|vid:{device.VendorId:X4}|pid:{device.ProductId:X4}|type:{device.GetType().Name}";
        }

        private static Win32API.GUID ToApiGuid(Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            return new Win32API.GUID
            {
                Data1 = BitConverter.ToUInt32(bytes, 0),
                Data2 = BitConverter.ToUInt16(bytes, 4),
                Data3 = BitConverter.ToUInt16(bytes, 6),
                Data4 = bytes.Skip(8).Take(8).ToArray()
            };
        }
    }
}




