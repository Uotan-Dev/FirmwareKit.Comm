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

    [Fact]
    public void SessionTimeoutMethods_AreInvokableThroughRegisteredProvider()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom-timeout", () => new TimeoutProvider());

        using var sessions = layer.OpenDeviceSessions(UsbApiKind.Auto, new UsbDeviceFilter
        {
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        });

        var session = Assert.Single(sessions.Sessions);
        var read = session.Read(4, 1234);
        var count = session.ReadInto(new byte[8], 0, 8, 4321);
        var written = session.Write(new byte[] { 1, 2, 3 }, 3, 987);

        Assert.Equal(4, read.Length);
        Assert.Equal(8, count);
        Assert.Equal(3, written);

        var timeoutSession = Assert.IsType<TimeoutSession>(session);
        Assert.Equal(1234, timeoutSession.LastReadTimeoutMs);
        Assert.Equal(4321, timeoutSession.LastReadIntoTimeoutMs);
        Assert.Equal(987, timeoutSession.LastWriteTimeoutMs);
    }

    private sealed class EmptyProvider : IUsbApiProvider
    {
        public string ApiName => "custom";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null) => [];
    }

    private sealed class TimeoutProvider : IUsbApiProvider
    {
        public string ApiName => "custom-timeout";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
        {
            var session = new TimeoutSession();
            if (filter == null || filter.Matches(session.DeviceInfo))
            {
                return new[] { (IUsbDeviceSession)session };
            }

            session.Dispose();
            return Array.Empty<IUsbDeviceSession>();
        }
    }

    private sealed class TimeoutSession : IUsbDeviceSession
    {
        public int LastReadTimeoutMs { get; private set; }

        public int LastReadIntoTimeoutMs { get; private set; }

        public int LastWriteTimeoutMs { get; private set; }

        public UsbDeviceInfo DeviceInfo { get; } = new()
        {
            ApiName = "custom-timeout",
            SourceApiKind = UsbApiKind.Custom,
            DevicePath = "mock://timeout",
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        };

        public byte[] Read(int length) => new byte[length];

        public byte[] Read(int length, int timeoutMs)
        {
            LastReadTimeoutMs = timeoutMs;
            return new byte[length];
        }

        public int ReadInto(byte[] buffer, int offset, int length) => length;

        public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
        {
            LastReadIntoTimeoutMs = timeoutMs;
            Array.Fill(buffer, (byte)0x5A, offset, length);
            return length;
        }

        public long Write(byte[] data, int length) => length;

        public long Write(byte[] data, int length, int timeoutMs)
        {
            LastWriteTimeoutMs = timeoutMs;
            return length;
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }
}
