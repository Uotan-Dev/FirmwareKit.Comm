using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;
using FirmwareKit.Comm.Usb.Core;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Verifies that a fatal native error classified as a disconnection throws
/// <see cref="UsbDeviceDisconnectedException"/> while other fatal errors stay
/// <see cref="IOException"/> (the shared base-class mapping used by all backends).
/// <para>验证被归类为断开的致命原生错误抛出 <see cref="UsbDeviceDisconnectedException"/>，
/// 而其他致命错误保持 <see cref="IOException"/>（所有后端共用的基类映射）。</para>
/// </summary>
public sealed class DisconnectionTests
{
    [Fact]
    public void Read_FatalDisconnectionError_ThrowsDisconnectedException()
    {
        using var session = CreateSession(disconnection: true);

        Assert.Throws<UsbDeviceDisconnectedException>(() => session.ReadInto(new byte[64], 0, 64, 1000));
    }

    [Fact]
    public void Read_FatalNonDisconnectionError_ThrowsIoException()
    {
        using var session = CreateSession(disconnection: false);

        Assert.Throws<IOException>(() => session.ReadInto(new byte[64], 0, 64, 1000));
    }

    [Fact]
    public void Write_FatalDisconnectionError_ThrowsDisconnectedException()
    {
        using var session = CreateSession(disconnection: true);

        Assert.Throws<UsbDeviceDisconnectedException>(() => session.Write(new byte[64], 64, 1000));
    }

    [Fact]
    public void Write_FatalNonDisconnectionError_ThrowsIoException()
    {
        using var session = CreateSession(disconnection: false);

        Assert.Throws<IOException>(() => session.Write(new byte[64], 64, 1000));
    }

    private static UsbDeviceSession CreateSession(bool disconnection)
    {
        return new UsbDeviceSession("fake", UsbApiKind.Custom, new FakeUsbDevice(disconnection));
    }

    /// <summary>
    /// A minimal backend that always fails every chunk with native error 19 (arbitrary;
    /// <see cref="IsDisconnectionError"/> decides how the shared loop classifies it).
    /// <para>始终以原生错误码 19（任意值）失败每个块的最小后端；
    /// 由 <see cref="IsDisconnectionError"/> 决定共享循环如何归类。</para>
    /// </summary>
    private sealed class FakeUsbDevice : UsbDevice
    {
        private readonly bool _disconnection;

        public FakeUsbDevice(bool disconnection)
        {
            _disconnection = disconnection;
        }

        protected override string BackendName => "fake";

        protected override bool IsOpen => true;

        protected override bool IsDisconnectionError(int nativeError) => _disconnection;

        protected override UsbChunkResult ReadChunk(IntPtr buffer, int length, int timeoutMs)
            => UsbChunkResult.Fatal(19);

        protected override UsbChunkResult WriteChunk(IntPtr buffer, int length, int timeoutMs)
            => UsbChunkResult.Fatal(19);

        public override byte[] Read(int length) => throw new NotImplementedException();

        public override long Write(byte[] data, int length) => throw new NotImplementedException();

        public override int GetSerialNumber() => 0;

        public override int CreateHandle() => 0;

        public override void Reset()
        {
        }

        public override void Dispose()
        {
        }
    }
}
