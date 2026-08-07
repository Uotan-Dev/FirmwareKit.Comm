using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;
using FirmwareKit.Comm.Usb.Backend.Linux;
using FirmwareKit.Comm.Usb.Backend.HarmonyOS;
using FirmwareKit.Comm.Usb.Backend.macOS;
using FirmwareKit.Comm.Usb.Backend.Windows;
using FirmwareKit.Comm.Usb.Core;
using FirmwareKit.Comm.Usb.Diagnostics;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Usb.Providers;

internal sealed class NativeUsbApiProvider : IUsbApiProvider, IUsbApiDiscoveryProvider, IUsbApiCapabilityProvider
{
    private static readonly Lazy<bool> IsHarmonyOS =
        new(HarmonyOSUsbDDK.IsHarmonyOSPlatform, isThreadSafe: true);

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
            SupportsNativeAsyncIo = false,
            SupportsNativeHotPlugMonitoring = false,
            RequiresExternalRuntime = false,
            Notes = notes,
            Backends = new[]
            {
                new UsbBackendCapability { BackendName = "winusb", SupportsNativeAsyncIo = true },
                new UsbBackendCapability { BackendName = "winusb-legacy", SupportsNativeAsyncIo = false },
                new UsbBackendCapability { BackendName = "linux-usbfs", SupportsNativeAsyncIo = true },
                new UsbBackendCapability { BackendName = "macos-iousbhost", SupportsNativeAsyncIo = false },
                new UsbBackendCapability { BackendName = "harmony-ddk", SupportsNativeAsyncIo = false }
            }
        };
    }

    private static List<UsbDevice> EnumerateBackendDevices(UsbDeviceFilter? filter)
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
            try
            {
                // IOUSBHost (IOUSBLib) backend, requires macOS 10.15+.
                return MacHostUsbFinder.FindDevice(filter);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                UsbTrace.Log($"IOUSBHost backend unavailable (requires macOS 10.15+): {ex.Message}");
                return new List<UsbDevice>();
            }
        }

        return new List<UsbDevice>();
    }
}
