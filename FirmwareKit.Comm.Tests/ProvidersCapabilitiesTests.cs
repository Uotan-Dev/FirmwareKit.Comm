using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Providers;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the identity and capability profile of every built-in provider.
/// <para>覆盖所有内置提供器的身份与能力轮廓。</para>
/// </summary>
public sealed class ProvidersCapabilitiesTests
{
    // ---- native ----

    [Fact]
    public void NativeProvider_Identity_IsNative()
    {
        var provider = new NativeUsbApiProvider();

        Assert.Equal("native", provider.ApiName);
        Assert.Equal(UsbApiKind.Native, provider.ApiKind);
    }

    [Fact]
    public void NativeProvider_GetCapabilities_StaticProfile()
    {
        var caps = new NativeUsbApiProvider().GetCapabilities();

        Assert.Equal("native", caps.ApiName);
        Assert.True(caps.SupportsNativeDiscovery);
        Assert.True(caps.SupportsDeviceSessions);
        Assert.True(caps.SupportsControlTransfers);
        Assert.True(caps.SupportsInterfaceConfigSwitching);
        Assert.False(caps.SupportsNativeAsyncIo);
        Assert.False(caps.SupportsNativeHotPlugMonitoring);
        Assert.False(caps.RequiresExternalRuntime);
        Assert.False(string.IsNullOrEmpty(caps.Notes));
    }

    [Fact]
    public void NativeProvider_GetCapabilities_Backends_ListAllNativeBackends()
    {
        var backends = new NativeUsbApiProvider().GetCapabilities().Backends!;

        Assert.Equal(6, backends.Count);
        Assert.Contains(backends, b => b.BackendName == "winusb");
        Assert.Contains(backends, b => b.BackendName == "linux-usbfs");
        Assert.Contains(backends, b => b.BackendName == "macos-iokit");
        Assert.Contains(backends, b => b.BackendName == "macos-iousbhost");
        Assert.Contains(backends, b => b.BackendName == "harmony-ddk");
        Assert.Contains(backends, b => b.BackendName == "winusb-legacy" && !b.SupportsNativeAsyncIo);
    }

    [Fact]
    public void NativeProvider_EnumerateDeviceInfos_DoesNotThrow()
    {
        // On Windows/Linux/macOS the native provider is supported and performs a real
        // (possibly empty) enumeration; it must never throw in a hardware-less CI.
        var infos = new NativeUsbApiProvider().EnumerateDeviceInfos();

        Assert.NotNull(infos);
    }

    // ---- libusb ----

    [Fact]
    public void LibUsbProvider_Identity_IsLibUsb()
    {
        var provider = new LibUsbApiProvider();

        Assert.Equal("libusb", provider.ApiName);
        Assert.Equal(UsbApiKind.LibUsbDotNet, provider.ApiKind);
    }

    [Fact]
    public void LibUsbProvider_GetCapabilities_Profile()
    {
        var caps = new LibUsbApiProvider().GetCapabilities();

        Assert.Equal("libusb", caps.ApiName);
        Assert.True(caps.SupportsNativeDiscovery);
        Assert.True(caps.SupportsNativeAsyncIo);
        Assert.True(caps.SupportsInterfaceConfigSwitching);
        Assert.False(caps.SupportsNativeHotPlugMonitoring);
        Assert.True(caps.RequiresExternalRuntime);
    }

    [Fact]
    public void LibUsbProvider_EnumerateDeviceInfos_DoesNotThrow()
    {
        // Without the native libusb runtime the provider degrades to an empty list
        // (SafeEnumerate swallows the expected DllNotFoundException).
        var infos = new LibUsbApiProvider().EnumerateDeviceInfos();

        Assert.NotNull(infos);
    }

    // ---- harmony ----

    [Fact]
    public void HarmonyOSProvider_Identity_IsHarmony()
    {
        var provider = new HarmonyOSUsbApiProvider();

        Assert.Equal("harmony", provider.ApiName);
        Assert.Equal(UsbApiKind.HarmonyOS, provider.ApiKind);
    }

    [Fact]
    public void HarmonyOSProvider_GetCapabilities_Profile()
    {
        var caps = new HarmonyOSUsbApiProvider().GetCapabilities();

        Assert.Equal("harmony", caps.ApiName);
        Assert.True(caps.SupportsNativeDiscovery);
        Assert.False(caps.SupportsNativeHotPlugMonitoring);
        Assert.False(caps.RequiresExternalRuntime);
        Assert.Contains("FIRMWAREKIT_USB_ENABLE_HARMONY", caps.Notes!);
    }

    [Fact]
    public void HarmonyOSProvider_EnumerateDeviceInfos_DoesNotThrow()
    {
        // Hidden unless FIRMWAREKIT_USB_ENABLE_HARMONY=1; without the opt-in the
        // provider reports unsupported and enumeration stays empty.
        var infos = new HarmonyOSUsbApiProvider().EnumerateDeviceInfos();

        Assert.NotNull(infos);
    }
}
