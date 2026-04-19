using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend.libusbdotnet;
using FirmwareKit.Comm.Usb.Backend.Linux;
using FirmwareKit.Comm.Usb.Backend.macOS;
using FirmwareKit.Comm.Usb.Backend.Windows;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Usb.Backend;

internal static class UsbManager
{
    public static bool ForceLibUsb { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public static List<UsbDevice> GetAllDevices(UsbDeviceFilter? filter = null)
    {
        if (ForceLibUsb)
        {
            try
            {
                return LibUsbFinder.FindDevice(filter);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to use libusb. Ensure libusb is properly installed and configured.", ex);
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return WinUSBFinder.FindDevice(filter);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return LinuxUsbFinder.FindDevice(filter);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return MacOSUsbFinder.FindDevice(filter);
        }
        try
        {
            return LibUsbFinder.FindDevice(filter);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Fallback to libusb failed. Ensure libusb is properly installed and configured.", ex);
        }
    }


}




