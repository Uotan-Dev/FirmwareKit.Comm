using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;
using FirmwareKit.Comm.Usb.Backend.Linux;
using FirmwareKit.Comm.Usb.Backend.macOS;
using FirmwareKit.Comm.Usb.Backend.Windows;
using FirmwareKit.Comm.Usb.Core;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Usb.Providers;

internal sealed class NativeUsbApiProvider : IUsbApiProvider
{
    public const string ApiNameConst = "native";

    public string ApiName => ApiNameConst;

    public UsbApiKind ApiKind => UsbApiKind.Native;

    public bool IsSupportedOnCurrentPlatform =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
    {
        if (!IsSupportedOnCurrentPlatform) return Array.Empty<IUsbDeviceSession>();

        List<UsbDevice> devices = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WinUSBFinder.FindDevice()
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? LinuxUsbFinder.FindDevice()
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? MacOSUsbFinder.FindDevice()
                    : new List<UsbDevice>();

        var sessions = new List<IUsbDeviceSession>(devices.Count);
        foreach (var device in devices)
        {
            var session = new FastbootUsbDeviceSession(ApiName, ApiKind, device);
            if (filter == null || filter.Matches(session.DeviceInfo))
            {
                sessions.Add(session);
            }
            else
            {
                session.Dispose();
            }
        }

        return sessions;
    }
}
