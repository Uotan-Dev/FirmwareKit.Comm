using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Core;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the UsbSessionCollection wrapper: enumeration and bulk dispose.
/// <para>覆盖 UsbSessionCollection 包装：枚举与批量释放。</para>
/// </summary>
public sealed class UsbSessionCollectionTests
{
    [Fact]
    public void Sessions_ExposesWrappedList()
    {
        var a = new FakeSession();
        var b = new FakeSession();
        var collection = new UsbSessionCollection(new IUsbDeviceSession[] { a, b });

        Assert.Equal(2, collection.Sessions.Count);
        Assert.Same(a, collection.Sessions[0]);
        Assert.Same(b, collection.Sessions[1]);
    }

    [Fact]
    public void GetEnumerator_EnumeratesAllSessions()
    {
        var a = new FakeSession();
        var b = new FakeSession();
        var collection = new UsbSessionCollection(new IUsbDeviceSession[] { a, b });

        var sessions = new List<IUsbDeviceSession>();
        foreach (var session in collection)
        {
            sessions.Add(session);
        }

        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public void Dispose_DisposesAllWrappedSessions()
    {
        var a = new FakeSession();
        var b = new FakeSession();
        var collection = new UsbSessionCollection(new IUsbDeviceSession[] { a, b });

        collection.Dispose();

        Assert.True(a.Disposed);
        Assert.True(b.Disposed);
    }

    [Fact]
    public void Dispose_EmptyCollection_DoesNotThrow()
    {
        var collection = new UsbSessionCollection(Array.Empty<IUsbDeviceSession>());

        collection.Dispose();
    }

    private sealed class FakeSession : IUsbDeviceSession
    {
        public int DefaultTimeoutMs => 1000;
        public UsbDeviceInfo DeviceInfo { get; } = new();
        public bool Disposed { get; private set; }

        public byte[] Read(int length) => Array.Empty<byte>();

        public byte[] Read(int length, int timeoutMs) => Array.Empty<byte>();

        public int ReadInto(byte[] buffer, int offset, int length) => 0;

        public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs) => 0;

        public long Write(byte[] data, int length) => 0;

        public long Write(byte[] data, int length, int timeoutMs) => 0;

        public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs) => 0;

        public void Reset() { }

        public void Dispose() => Disposed = true;
    }
}
