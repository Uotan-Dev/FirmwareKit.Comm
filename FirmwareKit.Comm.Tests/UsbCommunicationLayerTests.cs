using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Core;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the remaining UsbCommunicationLayer surface: callback enumeration,
/// single-session open and provider registration.
/// <para>覆盖 UsbCommunicationLayer 的其余接口面：回调枚举、单会话打开与提供器注册。</para>
/// </summary>
public sealed class UsbCommunicationLayerTests
{
    [Fact]
    public void EnumerateDevices_Callback_NullCallback_Throws()
    {
        var layer = new UsbCommunicationLayer();

        Assert.Throws<ArgumentNullException>(() => layer.EnumerateDevices(null!));
    }

    [Fact]
    public void EnumerateDevices_Callback_InvokedPerDiscoveredDevice()
    {
        var registry = new UsbApiRegistry();
        registry.Register("fake", () => new FakeProvider
        {
            Infos = new[]
            {
                new UsbDeviceInfo { ApiName = "fake", VendorId = 1 },
                new UsbDeviceInfo { ApiName = "fake", VendorId = 2 }
            }
        });
        var layer = new UsbCommunicationLayer(registry);
        var seen = new List<UsbDeviceInfo>();

        layer.EnumerateDevices(seen.Add, UsbApiKind.Auto);

        Assert.Equal(2, seen.Count);
        Assert.Equal(1, seen[0].VendorId);
        Assert.Equal(2, seen[1].VendorId);
    }

    [Fact]
    public void OpenDeviceSession_NoMatchingDevices_ReturnsNull()
    {
        var registry = new UsbApiRegistry();
        registry.Register("fake", () => new FakeProvider());
        var layer = new UsbCommunicationLayer(registry);

        var session = layer.OpenDeviceSession(UsbApiKind.Auto);

        Assert.Null(session);
    }

    [Fact]
    public void OpenDeviceSession_WithDevices_ReturnsFirst_AndDisposesRest()
    {
        var first = new FakeSession("first");
        var second = new FakeSession("second");
        var registry = new UsbApiRegistry();
        registry.Register("fake", () => new FakeProvider
        {
            Sessions = new IUsbDeviceSession[] { first, second }
        });
        var layer = new UsbCommunicationLayer(registry);

        using var session = layer.OpenDeviceSession(UsbApiKind.Auto);

        Assert.NotNull(session);
        Assert.Same(first, session);
        Assert.True(second.Disposed);
    }

    [Fact]
    public void RegisterApi_RegistersNewProvider_AndAppearsInAvailableApis()
    {
        var layer = new UsbCommunicationLayer();

        layer.RegisterApi("custom", () => new FakeProvider());

        Assert.Contains("custom", layer.GetAvailableApis());
    }

    [Fact]
    public void GetAvailableApiCapabilities_InfersProfile_ForPlainProvider()
    {
        var registry = new UsbApiRegistry();
        registry.Register("plain", () => new FakeProvider { ApiName = "plain" });
        var layer = new UsbCommunicationLayer(registry);

        var caps = layer.GetAvailableApiCapabilities();

        Assert.Contains(caps, c => c.ApiName == "plain" && c.SupportsDeviceSessions);
    }

    /// <summary>
    /// Provider stub with configurable infos and sessions.
    /// <para>可配置信息与会话的提供器桩。</para>
    /// </summary>
    private sealed class FakeProvider : IUsbApiProvider, IUsbApiDiscoveryProvider
    {
        public string ApiName { get; set; } = "fake";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform { get; set; } = true;

        public IReadOnlyList<UsbDeviceInfo> Infos { get; set; } = Array.Empty<UsbDeviceInfo>();

        public IReadOnlyList<IUsbDeviceSession> Sessions { get; set; } = Array.Empty<IUsbDeviceSession>();

        public IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null) => Infos;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null) => Sessions;
    }

    private sealed class FakeSession : IUsbDeviceSession
    {
        public FakeSession(string name) => DeviceInfo = new UsbDeviceInfo { ApiName = name };
        public byte EndpointIn => 0x81;
        public byte EndpointOut => 0x01;

        public int DefaultTimeoutMs => 1000;

        public UsbDeviceInfo DeviceInfo { get; }

        public bool Disposed { get; private set; }

        public byte[] Read(int length) => Array.Empty<byte>();

        public byte[] Read(int length, int timeoutMs) => Array.Empty<byte>();

        public int ReadInto(byte[] buffer, int offset, int length) => 0;

        public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs) => 0;

        public UsbReadResult ReadPacket(byte[] buffer, int offset, int length, int timeoutMs)
            => new(0, isTimeout: true, isShortPacket: false);

        public UsbReadResult ReadInterrupt(byte endpointAddress, byte[] buffer, int offset, int length, int timeoutMs)
            => throw new NotSupportedException();

        public long WriteInterrupt(byte endpointAddress, byte[] data, int offset, int length, int timeoutMs)
            => throw new NotSupportedException();

        public long Write(byte[] data, int length) => 0;

        public long Write(byte[] data, int length, int timeoutMs) => 0;

        public long Write(byte[] data, int offset, int length, int timeoutMs) => 0;

        public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs) => 0;

        public void WriteZlp(int timeoutMs)
        {
        }

#if NET8_0_OR_GREATER
        public int ReadInto(Span<byte> buffer, int timeoutMs) => 0;
#endif

        public void SetInterfaceAltSetting(byte interfaceNumber, byte altSetting) { }

        public void SetConfiguration(byte configuration) { }

        public void Reset() { }

        public void Dispose() => Disposed = true;
    }
}
