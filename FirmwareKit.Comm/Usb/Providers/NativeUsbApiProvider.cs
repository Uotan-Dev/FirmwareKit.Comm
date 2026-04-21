using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;
using FirmwareKit.Comm.Usb.Backend.Linux;
using FirmwareKit.Comm.Usb.Backend.macOS;
using FirmwareKit.Comm.Usb.Backend.Windows;
using FirmwareKit.Comm.Usb.Core;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Usb.Providers;

internal sealed class NativeUsbApiProvider : IUsbApiProvider, IUsbApiDiscoveryProvider, IUsbApiCapabilityProvider
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
        return UsbProviderProjection.ToSessions(ApiName, ApiKind, devices, filter);
    }

    public IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null)
    {
        if (!IsSupportedOnCurrentPlatform) return Array.Empty<UsbDeviceInfo>();

        var devices = EnumerateBackendDevices(filter);
        return UsbProviderProjection.ToInfos(ApiName, ApiKind, devices, filter);
    }

    public UsbApiCapabilities GetCapabilities()
    {
        return new UsbApiCapabilities
        {
            ApiName = ApiName,
            ApiKind = ApiKind,
            IsSupportedOnCurrentPlatform = IsSupportedOnCurrentPlatform,
            SupportsNativeDiscovery = true,
            SupportsDeviceSessions = true,
            SupportsControlTransfers = true,
            SupportsNativeAsyncIo = false,
            SupportsNativeHotPlugMonitoring = false,
            RequiresExternalRuntime = false,
            Notes = "Native transport is synchronous; async access is currently adapter-based and hot-plug monitoring is polling-based."
        };
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
