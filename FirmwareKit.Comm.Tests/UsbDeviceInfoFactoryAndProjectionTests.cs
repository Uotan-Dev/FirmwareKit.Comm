using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Backend;
using FirmwareKit.Comm.Core;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers backend-to-metadata projection: UsbDeviceInfoFactory and UsbProviderProjection.
/// <para>覆盖后端到元数据的投影：UsbDeviceInfoFactory 与 UsbProviderProjection。</para>
/// </summary>
public sealed class UsbDeviceInfoFactoryAndProjectionTests
{
    [Fact]
    public void FromBackendDevice_PopulatesAllFields_AndBuildsDeviceKey()
    {
        using var device = NewDevice();
        var info = UsbDeviceInfoFactory.FromBackendDevice("native", UsbApiKind.Native, device);

        Assert.Equal("native", info.ApiName);
        Assert.Equal(UsbApiKind.Native, info.SourceApiKind);
        Assert.Equal("/dev/bus/usb/001/002", info.DevicePath);
        Assert.Equal("FT123", info.SerialNumber);
        Assert.Equal((ushort)0x0403, info.VendorId);
        Assert.Equal((ushort)0x6001, info.ProductId);
        Assert.Equal((byte)0xFF, info.InterfaceClass);
        Assert.Equal((byte)0xFF, info.InterfaceSubClass);
        Assert.Equal((byte)0x00, info.InterfaceProtocol);
        Assert.True(info.InterfaceMetadataObserved);
        Assert.Equal(UsbDeviceSpeed.High, info.Speed);
        Assert.Single(info.Interfaces);
        Assert.Equal("native|Native|0403|6001|FF|FF|00|FT123|/dev/bus/usb/001/002", info.DeviceKey);
    }

    [Fact]
    public void ToInfos_NoFilter_ProjectsAll_AndDisposesDevices()
    {
        var deviceA = NewDevice();
        var deviceB = NewDevice();

        var infos = UsbProviderProjection.ToInfos("native", UsbApiKind.Native, new[] { deviceA, deviceB }, null);

        Assert.Equal(2, infos.Count);
        Assert.True(deviceA.Disposed);
        Assert.True(deviceB.Disposed);
    }

    [Fact]
    public void ToInfos_Filter_OnlyMatchingInfos()
    {
        var match = NewDevice(serial: "keep", path: "/dev/bus/usb/001/001");
        var skip = NewDevice(serial: "drop", path: "/dev/bus/usb/001/002");
        var filter = new UsbDeviceFilter { SerialNumber = "keep" };

        var infos = UsbProviderProjection.ToInfos("native", UsbApiKind.Native, new[] { match, skip }, filter);

        Assert.Single(infos);
        Assert.Equal("keep", infos[0].SerialNumber);
        Assert.True(match.Disposed);
        Assert.True(skip.Disposed);
    }

    [Fact]
    public void ToSessions_NoFilter_ReturnsAll()
    {
        var deviceA = NewDevice();
        var deviceB = NewDevice();

        var sessions = UsbProviderProjection.ToSessions("native", UsbApiKind.Native, new[] { deviceA, deviceB }, null);

        Assert.Equal(2, sessions.Count);
        Assert.False(deviceA.Disposed); // sessions own their devices
    }

    [Fact]
    public void ToSessions_Filter_DisposesNonMatching()
    {
        var match = NewDevice(serial: "keep", path: "/dev/bus/usb/001/001");
        var skip = NewDevice(serial: "drop", path: "/dev/bus/usb/001/002");
        var filter = new UsbDeviceFilter { SerialNumber = "keep" };

        var sessions = UsbProviderProjection.ToSessions("native", UsbApiKind.Native, new[] { match, skip }, filter);

        Assert.Single(sessions);
        Assert.False(match.Disposed);
        Assert.True(skip.Disposed);
    }

    private static FakeUsbDevice NewDevice(string serial = "FT123", string path = "/dev/bus/usb/001/002") => new()
    {
        DevicePath = path,
        SerialNumber = serial,
        VendorId = 0x0403,
        ProductId = 0x6001,
        InterfaceClass = 0xFF,
        InterfaceSubClass = 0xFF,
        InterfaceProtocol = 0x00,
        InterfaceMetadataObserved = true,
        Speed = UsbDeviceSpeed.High,
        Interfaces = new[] { new UsbInterfaceInfo { InterfaceNumber = 0, Class = 0xFF } }
    };

    /// <summary>
    /// Minimal in-memory backend device for projection tests.
    /// <para>投影测试用的内存后端设备。</para>
    /// </summary>
    private sealed class FakeUsbDevice : UsbDevice
    {
        public bool Disposed { get; private set; }

        protected override string BackendName => "fake";

        protected override bool IsOpen => true;

        protected override UsbChunkResult ReadChunk(IntPtr buffer, int length, int timeoutMs)
            => UsbChunkResult.Success(0);

        protected override UsbChunkResult WriteChunk(IntPtr buffer, int length, int timeoutMs)
            => UsbChunkResult.Success(0);

        public override byte[] Read(int length) => Array.Empty<byte>();

        public override long Write(byte[] data, int length) => length;

        public override int GetSerialNumber() => 0;

        public override int CreateHandle() => 0;

        public override void Reset() { }

        public override void Dispose() => Disposed = true;
    }
}
