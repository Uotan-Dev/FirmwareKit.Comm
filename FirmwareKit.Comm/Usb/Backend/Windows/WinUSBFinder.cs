using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace FirmwareKit.Comm.Usb.Backend.Windows
{
    internal static class WinUSBFinder
    {
        private static readonly Guid[] KnownInterfaceGUIDs = new[]
        {
            new Guid("a5dcbf10-6530-11d2-901f-00c04fb951ed"), // USB Device
            new Guid("f72fe0d4-cbcb-407d-8814-9ed673d0dd6b"), // WinUSB generic
            new Guid("77395066-6C05-4B91-8071-3D7E2409546E"), // ADB
            new Guid("4D36E978-E325-11CE-BFC1-08002BE10318")  // Ports
        };

        public static List<UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
        {
            var devices = new List<UsbDevice>();
            var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var guid in KnownInterfaceGUIDs)
            {
                Win32API.GUID apiGuid = ToApiGuid(guid);
                var devInfo = Win32API.SetupDiGetClassDevsW(ref apiGuid, null, 0,
                    Win32API.DIGCF_PRESENT | Win32API.DIGCF_DEVICEINTERFACE);

                if (devInfo == (IntPtr)(-1)) continue;

                try
                {
                    uint index = 0;
                    Win32API.SpDeviceInterfaceData interfaceData = new Win32API.SpDeviceInterfaceData();
                    interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

                    while (Win32API.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref apiGuid, index++, ref interfaceData))
                    {
                        uint detailSize = 0;
                        Win32API.SetupDiGetDeviceInterfaceDetailW(devInfo, ref interfaceData, IntPtr.Zero, 0, out detailSize, IntPtr.Zero);

                        IntPtr detailBuffer = Marshal.AllocHGlobal((int)detailSize);
                        try
                        {
                            Marshal.WriteInt32(detailBuffer, (IntPtr.Size == 4) ? 6 : 8);
                            uint requiredSize;
                            if (Win32API.SetupDiGetDeviceInterfaceDetailW(devInfo, ref interfaceData, detailBuffer, detailSize, out requiredSize, IntPtr.Zero))
                            {
                                string path = Marshal.PtrToStringUni(new IntPtr(detailBuffer.ToInt64() + 4)) ?? "";
                                string lowerPath = path.ToLower();

                                if (!PathMatchesFilter(path, filter))
                                {
                                    continue;
                                }

                                // Ϊ Google �豸�Ż�����Щ�豸ͨ����ѡ WinUSB
                                bool isGoogleWinUsb = lowerPath.Contains("vid_18d1&pid_d00d");

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
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(detailBuffer);
                        }
                    }
                }
                finally
                {
                    Win32API.SetupDiDestroyDeviceInfoList(devInfo);
                }
            }
            return devices;
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
                int returned;
                if (Win32API.DeviceIoControl(hDevice, LegacyUsbDevice.IoGetSerialCode, null, 0, buffer, buffer.Length, out returned, IntPtr.Zero))
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




