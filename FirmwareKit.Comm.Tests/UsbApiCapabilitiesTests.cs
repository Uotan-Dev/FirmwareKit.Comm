using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the capability profile model defaults and round-trips.
/// <para>覆盖能力轮廓模型的默认值与往返。</para>
/// </summary>
public sealed class UsbApiCapabilitiesTests
{
    [Fact]
    public void Defaults_AreEmptyAndFalse()
    {
        var caps = new UsbApiCapabilities();

        Assert.Equal(string.Empty, caps.ApiName);
        Assert.False(caps.SupportsNativeDiscovery);
        Assert.False(caps.SupportsDeviceSessions);
        Assert.False(caps.SupportsControlTransfers);
        Assert.False(caps.SupportsInterfaceConfigSwitching);
        Assert.False(caps.SupportsNativeAsyncIo);
        Assert.False(caps.SupportsNativeHotPlugMonitoring);
        Assert.False(caps.RequiresExternalRuntime);
        Assert.Null(caps.Notes);
        Assert.Null(caps.Backends);
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var caps = new UsbApiCapabilities
        {
            ApiName = "native",
            ApiKind = UsbApiKind.Native,
            IsSupportedOnCurrentPlatform = true,
            SupportsNativeDiscovery = true,
            SupportsDeviceSessions = true,
            SupportsControlTransfers = true,
            SupportsInterfaceConfigSwitching = true,
            SupportsNativeAsyncIo = false,
            SupportsNativeHotPlugMonitoring = true,
            RequiresExternalRuntime = false,
            Notes = "winusb/usbfs/IOUSBLib"
        };

        Assert.Equal("native", caps.ApiName);
        Assert.Equal(UsbApiKind.Native, caps.ApiKind);
        Assert.True(caps.IsSupportedOnCurrentPlatform);
        Assert.True(caps.SupportsNativeDiscovery);
        Assert.True(caps.SupportsDeviceSessions);
        Assert.True(caps.SupportsControlTransfers);
        Assert.True(caps.SupportsInterfaceConfigSwitching);
        Assert.False(caps.SupportsNativeAsyncIo);
        Assert.True(caps.SupportsNativeHotPlugMonitoring);
        Assert.False(caps.RequiresExternalRuntime);
        Assert.Equal("winusb/usbfs/IOUSBLib", caps.Notes);
    }

    [Fact]
    public void Backends_List_IsRoundTripped()
    {
        var caps = new UsbApiCapabilities
        {
            Backends = new[]
            {
                new UsbBackendCapability { BackendName = "winusb", SupportsNativeAsyncIo = true },
                new UsbBackendCapability { BackendName = "usbfs", SupportsNativeAsyncIo = false }
            }
        };

        Assert.Equal(2, caps.Backends!.Count);
        Assert.Equal("winusb", caps.Backends[0].BackendName);
        Assert.True(caps.Backends[0].SupportsNativeAsyncIo);
        Assert.False(caps.Backends[1].SupportsNativeAsyncIo);
    }

    [Fact]
    public void BackendCapability_Defaults_AreEmptyAndFalse()
    {
        var backend = new UsbBackendCapability();

        Assert.Equal(string.Empty, backend.BackendName);
        Assert.False(backend.SupportsNativeAsyncIo);
    }
}
