using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;

namespace FirmwareKit.Comm.Usb.Core;

internal static class UsbProviderProjection
{
    public static IReadOnlyList<IUsbDeviceSession> ToSessions(
        string apiName,
        UsbApiKind apiKind,
        IReadOnlyList<UsbDevice> devices,
        UsbDeviceFilter? filter)
    {
        var sessions = new List<IUsbDeviceSession>(devices.Count);
        foreach (var device in devices)
        {
            var session = new UsbDeviceSession(apiName, apiKind, device);
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

    public static IReadOnlyList<UsbDeviceInfo> ToInfos(
        string apiName,
        UsbApiKind apiKind,
        IReadOnlyList<UsbDevice> devices,
        UsbDeviceFilter? filter)
    {
        var infos = new List<UsbDeviceInfo>(devices.Count);

        foreach (var device in devices)
        {
            try
            {
                var info = UsbDeviceInfoFactory.FromBackendDevice(apiName, apiKind, device);
                if (filter == null || filter.Matches(info))
                {
                    infos.Add(info);
                }
            }
            finally
            {
                device.Dispose();
            }
        }

        return infos;
    }
}