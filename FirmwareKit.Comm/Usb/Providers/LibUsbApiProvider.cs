using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;
using FirmwareKit.Comm.Usb.Backend.libusbdotnet;
using FirmwareKit.Comm.Usb.Core;

namespace FirmwareKit.Comm.Usb.Providers;

internal sealed class LibUsbApiProvider : IUsbApiProvider
{
    public const string ApiNameConst = "libusb";

    public string ApiName => ApiNameConst;

    public UsbApiKind ApiKind => UsbApiKind.LibUsbDotNet;

    public bool IsSupportedOnCurrentPlatform => true;

    public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
    {
        List<UsbDevice> devices;
        try
        {
            devices = LibUsbFinder.FindDevice(filter);
        }
        catch
        {
            return Array.Empty<IUsbDeviceSession>();
        }

        var sessions = new List<IUsbDeviceSession>(devices.Count);
        foreach (var device in devices)
        {
            var session = new UsbDeviceSession(ApiName, ApiKind, device);
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
