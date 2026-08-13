using System.Runtime.InteropServices;
using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Backend;
using FirmwareKit.Comm.Backend.HarmonyOS;
using FirmwareKit.Comm.Backend.Linux;
using FirmwareKit.Comm.Backend.MacOS;
using FirmwareKit.Comm.Backend.Windows;
using FirmwareKit.Comm.Diagnostics;

namespace FirmwareKit.Comm.Providers;

internal sealed class NativeUsbApiProvider : UsbApiProviderBase
{
    private static readonly Lazy<bool> IsHarmonyOS =
        new(HarmonyOSUsbDDK.IsHarmonyOSPlatform, isThreadSafe: true);

    public const string ApiNameConst = "native";

    public override string ApiName => ApiNameConst;

    public override UsbApiKind ApiKind => UsbApiKind.Native;

    public override bool IsSupportedOnCurrentPlatform =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public override UsbApiCapabilities GetCapabilities()
    {
        bool isHarmonyOS = IsHarmonyOS.Value;
        string notes = "Native transport is synchronous; async access is currently adapter-based and hot-plug monitoring is polling-based.";

        if (isHarmonyOS)
        {
            notes = "HarmonyOS detected; use the dedicated 'harmony' provider for USBManager IPC bridge, or this native provider for direct usbfs access.";
        }

        return new UsbApiCapabilities
        {
            ApiName = ApiName,
            ApiKind = ApiKind,
            IsSupportedOnCurrentPlatform = IsSupportedOnCurrentPlatform,
            SupportsNativeDiscovery = true,
            SupportsDeviceSessions = true,
            SupportsControlTransfers = true,
            SupportsInterfaceConfigSwitching = true,
            SupportsNativeAsyncIo = false,
            SupportsNativeHotPlugMonitoring = false,
            RequiresExternalRuntime = false,
            // The native provider spans platform backends whose Reset semantics differ:
            // WinUSB/legacy/macOS are pipe-level (session stays valid), while Linux usbfs
            // and HarmonyOS DDK reset the device/DDK session (must re-open). The top-level
            // flag is therefore the conservative "true" (at least one backend re-enumerates).
            // <para>native 提供器覆盖多个平台后端，其 Reset 语义不同：
            // WinUSB/legacy/macOS 为管道级（会话保持有效），而 Linux usbfs 与 HarmonyOS DDK
            // 重置设备/DDK 会话（必须重新打开）。顶层标志因此取保守的 true
            // （至少一个后端会重新枚举）。</para>
            ResetReenumeratesDevice = true,
            Notes = notes,
            Backends = new[]
            {
                new UsbBackendCapability { BackendName = "winusb", SupportsNativeAsyncIo = true, ResetReenumeratesDevice = false },
                new UsbBackendCapability { BackendName = "winusb-legacy", SupportsNativeAsyncIo = false, ResetReenumeratesDevice = false },
                new UsbBackendCapability { BackendName = "linux-usbfs", SupportsNativeAsyncIo = true, ResetReenumeratesDevice = true },
                new UsbBackendCapability { BackendName = "macos-iousbhost", SupportsNativeAsyncIo = false, ResetReenumeratesDevice = false },
                new UsbBackendCapability { BackendName = "harmony-ddk", SupportsNativeAsyncIo = false, ResetReenumeratesDevice = true }
            }
        };
    }

    protected override List<UsbDevice> EnumerateBackendDevices(UsbDeviceFilter? filter)
    {
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
            // IOUSBHost (IOUSBLib) backend, requires macOS 10.15+ AND the
            // IOUSBHost.framework main binary being loadable. On current macOS
            // releases that binary is absent (only a BridgeSupport stub remains),
            // so MacHostUsbFinder.FindDevice throws DllNotFoundException — which
            // MacHostUsbFinder SILENTLY swallows and returns an empty list (leaving
            // LastCopyDevicesSucceeded = false). Detect that degraded state and
            // fall back to the IOKit classic API (IOServiceMatching +
            // IORegistryEntryCreateCFProperty), which is loadable on every macOS.
            // <para>IOUSBHost（IOUSBLib）后端，需 macOS 10.15+ 且 IOUSBHost.framework
            // 主二进制可加载。当前 macOS 发行版中该二进制缺失（仅剩 BridgeSupport 桩），
            // 故 MacHostUsbFinder.FindDevice 抛 DllNotFoundException——而
            // MacHostUsbFinder 静默吞之并返空列表（置 LastCopyDevicesSucceeded = false）。
            // 检测该降级态并回退到 IOKit 经典 API（IOServiceMatching +
            // IORegistryEntryCreateCFProperty），该 API 在每个 macOS 上均可加载。</para>
            List<UsbDevice> hostResult = MacHostUsbFinder.FindDevice(filter);
            if (MacHostUsbFinder.LastCopyDevicesSucceeded)
            {
                return hostResult;
            }

            UsbTrace.Log("IOUSBHost backend degraded (LastCopyDevicesSucceeded=false); falling back to IOKit classic API.");
            try
            {
                return IOKitUsbFinder.FindDevice(filter);
            }
            catch (Exception iokitEx) when (iokitEx is DllNotFoundException or EntryPointNotFoundException)
            {
                UsbTrace.Log($"IOKit classic backend also unavailable: {iokitEx.Message}");
                return hostResult;
            }
        }

        return new List<UsbDevice>();
    }
}
