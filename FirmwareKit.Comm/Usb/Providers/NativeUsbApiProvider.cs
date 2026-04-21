using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;
using FirmwareKit.Comm.Usb.Backend.Linux;
using FirmwareKit.Comm.Usb.Backend.macOS;
using FirmwareKit.Comm.Usb.Backend.Windows;
using FirmwareKit.Comm.Usb.Core;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Usb.Providers;

internal sealed class NativeUsbApiProvider : IUsbApiProvider, IUsbApiDiscoveryProvider
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
        if (!IsSupportedOnCurrentPlatform) return Array.Empty<UsbDeviceInfo>();

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
                info.DeviceKey = UsbDeviceIdentity.BuildKey(info);

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
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WinUSBFinder.FindDevice(filter)
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? LinuxUsbFinder.FindDevice(filter)
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? MacOSUsbFinder.FindDevice(filter)
                    : new List<UsbDevice>();
    }
}
