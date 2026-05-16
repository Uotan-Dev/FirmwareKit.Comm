using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Usb.Backend.OpenHarmony.OpenHarmonyUsbAPI;

namespace FirmwareKit.Comm.Usb.Backend.OpenHarmony;

internal static class OpenHarmonyUsbFinder
{
    public static List<UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
    {
        List<UsbDevice> devices = new List<UsbDevice>();

        string[] searchPaths = GetUsbDevicePaths();

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            foreach (var busDir in Directory.GetDirectories(basePath))
            {
                foreach (var devPath in Directory.GetFiles(busDir))
                {
                    ProbeDevice(devPath, filter, devices);
                }
            }
        }

        return devices;
    }

    private static string[] GetUsbDevicePaths()
    {
        return new[]
        {
            "/dev/bus/usb",
            "/dev/usb"
        };
    }

    private static void ProbeDevice(string devPath, UsbDeviceFilter? filter, List<UsbDevice> devices)
    {
        int fd = open(devPath, O_RDWR | O_CLOEXEC);
        if (fd < 0)
        {
            fd = open(devPath, 0 | O_CLOEXEC);
            if (fd < 0) return;
        }

        byte[] desc = new byte[1024];
        IntPtr ptr = Marshal.AllocHGlobal(desc.Length);
        try
        {
            int n = read(fd, ptr, (uint)desc.Length);
            if (n < 18) { close(fd); fd = -1; return; }
            Marshal.Copy(ptr, desc, 0, n);

            if (n < 18) { close(fd); fd = -1; return; }
            ushort idVendor = (ushort)(desc[8] | (desc[9] << 8));
            ushort idProduct = (ushort)(desc[10] | (desc[11] << 8));
            byte iSerialNumber = desc[14];

            if (filter?.VendorId is ushort filterVid && idVendor != filterVid)
            {
                return;
            }

            if (filter?.ProductId is ushort filterPid && idProduct != filterPid)
            {
                return;
            }

            int pos = desc[0];
            while (pos < n - 1)
            {
                int len = desc[pos];
                if (len < 2 || pos + len > n) break;
                byte type = desc[pos + 1];

                if (type == 0x04)
                {
                    if (len < 9) { pos += len; continue; }
                    byte ifcClass = desc[pos + 5];
                    byte ifcSubClass = desc[pos + 6];
                    byte ifcProtocol = desc[pos + 7];
                    byte ifcId = desc[pos + 2];

                    if (InterfaceMatchesFilter(ifcClass, ifcSubClass, ifcProtocol, filter))
                    {
                        byte numEpts = desc[pos + 4];
                        byte epIn = 0, epOut = 0;
                        int eptPos = pos + len;
                        int checkedEpts = 0;

                        while (eptPos < n - 1 && checkedEpts < numEpts)
                        {
                            int eptLen = desc[eptPos];
                            if (eptLen < 2 || eptPos + eptLen > n) break;
                            byte eptType = desc[eptPos + 1];

                            if (eptType == 0x05)
                            {
                                if (eptLen >= 7)
                                {
                                    byte addr = desc[eptPos + 2];
                                    byte attr = desc[eptPos + 3];
                                    if ((attr & 0x03) == 0x02)
                                    {
                                        if ((addr & 0x80) != 0) epIn = addr;
                                        else epOut = addr;
                                    }
                                }

                                checkedEpts++;
                            }

                            eptPos += eptLen;
                        }

                        if (epIn != 0 && epOut != 0)
                        {
                            var dev = new OpenHarmonyUsbDevice
                            {
                                DevicePath = devPath,
                                VendorId = idVendor,
                                ProductId = idProduct,
                                InterfaceClass = ifcClass,
                                InterfaceSubClass = ifcSubClass,
                                InterfaceProtocol = ifcProtocol,
                                InterfaceMetadataObserved = true,
                                ep_in = epIn,
                                ep_out = epOut,
                                InterfaceId = ifcId,
                                iSerialNumber = iSerialNumber,
                                UsbDeviceType = UsbDeviceType.OpenHarmony,
                                SerialNumber = iSerialNumber == 0 ? null : "UNKNOWN"
                            };

                            if (dev.CreateHandle() == 0)
                            {
                                devices.Add(dev);
                            }
                            else
                            {
                                UsbTrace.Log($"OpenHarmonyUsbFinder: CreateHandle failed for {devPath}");
                                dev.Dispose();
                            }

                            break;
                        }
                    }
                }

                pos += len;
            }
        }
        catch (Exception ex)
        {
            UsbTrace.Log($"OpenHarmonyUsbFinder failed for path '{devPath}': {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
            if (fd >= 0) close(fd);
            fd = -1;
        }
    }

    private static bool InterfaceMatchesFilter(byte interfaceClass, byte interfaceSubClass, byte interfaceProtocol, UsbDeviceFilter? filter)
    {
        if (filter?.InterfaceClass is byte c && interfaceClass != c) return false;
        if (filter?.InterfaceSubClass is byte s && interfaceSubClass != s) return false;
        if (filter?.InterfaceProtocol is byte p && interfaceProtocol != p) return false;
        return true;
    }
}
