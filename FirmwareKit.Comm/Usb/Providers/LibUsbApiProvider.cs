using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;
using FirmwareKit.Comm.Usb.Backend.libusbdotnet;
using FirmwareKit.Comm.Usb.Core;
using FirmwareKit.Comm.Usb.Diagnostics;

namespace FirmwareKit.Comm.Usb.Providers;

internal sealed class LibUsbApiProvider : IUsbApiProvider, IUsbApiDiscoveryProvider, IUsbApiCapabilityProvider
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
        return UsbProviderProjection.ToSessions(ApiName, ApiKind, devices, filter);
    }

    public IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null)
    {
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
            SupportsNativeAsyncIo = true,
            SupportsNativeHotPlugMonitoring = false,
            RequiresExternalRuntime = true,
            Notes = "LibUsbDotNet requires the native libusb runtime; async access uses the upstream libusb async API where available and hot-plug monitoring is polling-based."
        };
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
