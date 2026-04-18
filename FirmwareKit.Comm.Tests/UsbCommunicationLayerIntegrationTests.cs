using FirmwareKit.Comm.Usb;
using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Core;

namespace FirmwareKit.Comm.IntegrationTests;

public sealed class UsbCommunicationLayerIntegrationTests
{
    [Fact]
    public void DefaultLayer_ContainsNativeAndLibUsbApis()
    {
        var layer = new UsbCommunicationLayer();
        var apis = layer.GetAvailableApis();

        Assert.Contains("native", apis, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("libusb", apis, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnumerateDevices_WithFilter_DoesNotThrow()
    {
        var layer = new UsbCommunicationLayer();
        var filter = new UsbDeviceFilter
        {
            VendorId = 0xFFFF,
            ProductId = 0xFFFF
        };

        var devices = layer.EnumerateDevices(UsbApiKind.Auto, filter);
        Assert.NotNull(devices);
    }

    [Fact]
    public async Task EnumerateDevicesAsync_DoesNotThrow()
    {
        var devices = await UsbComm.EnumerateDevicesAsync(UsbApiKind.Auto, new UsbDeviceFilter());
        Assert.NotNull(devices);
    }

    [Fact]
    public void RegisterCustomProvider_IsInvokable()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom", () => new EmptyProvider());

        var apis = layer.GetAvailableApis();
        Assert.Contains("custom", apis, StringComparer.OrdinalIgnoreCase);

        var devices = layer.EnumerateDevices(UsbApiKind.Auto, new UsbDeviceFilter());
        Assert.Empty(devices);
    }

    private sealed class EmptyProvider : IUsbApiProvider
    {
        public string ApiName => "custom";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null) => [];
    }
}
