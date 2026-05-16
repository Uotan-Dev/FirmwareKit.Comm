using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;
using FirmwareKit.Comm.Usb.Backend.HarmonyOS;
using FirmwareKit.Comm.Usb.Backend.OpenHarmony;
using FirmwareKit.Comm.Usb.Core;
using FirmwareKit.Comm.Usb.Diagnostics;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Usb.Backend.HarmonyOS.HarmonyOSUsbDDK;

namespace FirmwareKit.Comm.Usb.Providers;

internal sealed class HarmonyOSUsbApiProvider : IUsbApiProvider, IUsbApiDiscoveryProvider, IUsbApiCapabilityProvider
{
    private static readonly Lazy<bool> DdkAvailable =
        new(() =>
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;
                if (!OpenHarmonyUsbAPI.IsHarmonyOSPlatform()) return false;

                int ret = OH_Usb_Init();
                if (ret == USB_DDK_NO_ERROR)
                {
                    OH_Usb_Release();
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }, isThreadSafe: true);

    public const string ApiNameConst = "harmony";

    public string ApiName => ApiNameConst;

    public UsbApiKind ApiKind => UsbApiKind.HarmonyOS;

    public bool IsSupportedOnCurrentPlatform => DdkAvailable.Value;

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
            Notes = "HarmonyOS USB DDK backend via libusb_ndk.z.so P/Invoke; pure C# implementation with no external bridge required. Must run within DriverExtensionAbility lifecycle with ohos.permission.ACCESS_DDK_USB."
        };
    }

    private static List<UsbDevice> EnumerateBackendDevices(UsbDeviceFilter? filter)
    {
        try
        {
            return HarmonyOSUsbFinder.FindDevice(filter);
        }
        catch (DllNotFoundException ex)
        {
            UsbTrace.Log($"HarmonyOS USB DDK unavailable: {ex.Message}");
            return new List<UsbDevice>();
        }
        catch (TypeInitializationException ex)
        {
            UsbTrace.Log($"HarmonyOS initialization failed: {ex.Message}");
            return new List<UsbDevice>();
        }
        catch
        {
            UsbTrace.Log("HarmonyOS enumeration failed with unknown exception.");
            return new List<UsbDevice>();
        }
    }
}

internal sealed class OpenHarmonyUsbApiProvider : IUsbApiProvider, IUsbApiDiscoveryProvider, IUsbApiCapabilityProvider
{
    private static readonly Lazy<bool> PlatformAvailable =
        new(() =>
        {
            if (!OpenHarmonyUsbAPI.IsOpenHarmonyPlatform()) return false;
            try
            {
                return Directory.Exists("/dev/bus/usb") || Directory.Exists("/dev/usb");
            }
            catch
            {
                return false;
            }
        }, isThreadSafe: true);

    public const string ApiNameConst = "openharmony";

    public string ApiName => ApiNameConst;

    public UsbApiKind ApiKind => UsbApiKind.OpenHarmony;

    public bool IsSupportedOnCurrentPlatform => PlatformAvailable.Value;

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
            Notes = "OpenHarmony native USB backend via usbfs; direct kernel access for USB device communication. Async access is adapter-based and hot-plug monitoring is polling-based."
        };
    }

    private static List<UsbDevice> EnumerateBackendDevices(UsbDeviceFilter? filter)
    {
        try
        {
            return OpenHarmonyUsbFinder.FindDevice(filter);
        }
        catch (DllNotFoundException ex)
        {
            UsbTrace.Log($"OpenHarmony backend unavailable: {ex.Message}");
            return new List<UsbDevice>();
        }
        catch (TypeInitializationException ex)
        {
            UsbTrace.Log($"OpenHarmony initialization failed: {ex.Message}");
            return new List<UsbDevice>();
        }
        catch
        {
            UsbTrace.Log("OpenHarmony enumeration failed with unknown exception.");
            return new List<UsbDevice>();
        }
    }
}
