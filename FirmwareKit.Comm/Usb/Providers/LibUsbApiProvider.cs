using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;
using FirmwareKit.Comm.Usb.Backend.libusbdotnet;
using FirmwareKit.Comm.Usb.Core;
using FirmwareKit.Comm.Usb.Diagnostics;

namespace FirmwareKit.Comm.Usb.Providers;

internal sealed class LibUsbApiProvider : IUsbApiProvider, IUsbApiDiscoveryProvider
{
    private static readonly Lazy<bool> RuntimeAvailable =
        new(() => LibUsbFinder.IsRuntimeAvailable(out _), isThreadSafe: true);

    public const string ApiNameConst = "libusb";

    public string ApiName => ApiNameConst;

    public UsbApiKind ApiKind => UsbApiKind.LibUsbDotNet;

    public bool IsSupportedOnCurrentPlatform => RuntimeAvailable.Value;

    public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
    {
        var devices = EnumerateBackendDevices(filter);

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

    public IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null)
    {
        var devices = EnumerateBackendDevices(filter);
        var infos = new List<UsbDeviceInfo>(devices.Count);

        foreach (var device in devices)
        {
            try
            {
                var info = new UsbDeviceInfo
                {
                    ApiName = ApiName,
                    SourceApiKind = ApiKind,
                    SourceDeviceType = device.GetType().Name,
                    DevicePath = device.DevicePath,
                    SerialNumber = device.SerialNumber,
                    VendorId = device.VendorId,
                    ProductId = device.ProductId,
                    InterfaceClass = device.InterfaceClass,
                    InterfaceSubClass = device.InterfaceSubClass,
                    InterfaceProtocol = device.InterfaceProtocol
                };

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

    private static List<UsbDevice> EnumerateBackendDevices(UsbDeviceFilter? filter)
    {
        try
        {
            return LibUsbFinder.FindDevice(filter);
        }
        catch (DllNotFoundException ex)
        {
            UsbTrace.Log($"LibUsb backend unavailable: {ex.Message}");
            return new List<UsbDevice>();
        }
        catch (TypeInitializationException ex)
        {
            UsbTrace.Log($"LibUsb initialization failed: {ex.Message}");
            return new List<UsbDevice>();
        }
        catch
        {
            UsbTrace.Log("LibUsb enumeration failed with unknown exception.");
            return new List<UsbDevice>();
        }
    }
}
