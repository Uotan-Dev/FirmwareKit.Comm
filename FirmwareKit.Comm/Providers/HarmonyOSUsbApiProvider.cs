using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Backend;
using FirmwareKit.Comm.Backend.HarmonyOS;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Backend.HarmonyOS.HarmonyOSUsbDDK;

namespace FirmwareKit.Comm.Providers;

internal sealed class HarmonyOSUsbApiProvider : UsbApiProviderBase
{
    private static readonly Lazy<bool> DdkAvailable =
        new(() =>
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;
                if (!HarmonyOSUsbDDK.IsHarmonyOSPlatform()) return false;

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

    public override string ApiName => ApiNameConst;

    public override UsbApiKind ApiKind => UsbApiKind.HarmonyOS;

    public override bool IsSupportedOnCurrentPlatform => DdkAvailable.Value;

    protected override List<UsbDevice> EnumerateBackendDevices(UsbDeviceFilter? filter)
        => SafeEnumerate(() => HarmonyOSUsbFinder.FindDevice(filter), "HarmonyOS USB DDK");

    public override UsbApiCapabilities GetCapabilities()
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
}
