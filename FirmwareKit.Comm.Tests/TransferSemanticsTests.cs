using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;

namespace FirmwareKit.Comm.IntegrationTests;

/// <summary>
/// Covers the shared transfer semantics: policy normalization, string descriptor
/// decoding, and the UsbStream adapter.
/// <para>覆盖共享传输语义：策略规范化、字符串描述符解码与 UsbStream 适配器。</para>
/// </summary>
public sealed class TransferSemanticsTests
{
    // ---- UsbTransferPolicies ----

    [Fact]
    public void NormalizeTimeout_ZeroOrNegative_UsesDefault()
    {
        Assert.Equal(5000, UsbTransferPolicies.NormalizeTimeout(0, 5000));
        Assert.Equal(5000, UsbTransferPolicies.NormalizeTimeout(-1, 5000));
        Assert.Equal(5000, UsbTransferPolicies.NormalizeTimeout(int.MinValue, 5000));
    }

    [Fact]
    public void NormalizeTimeout_Positive_Preserved()
    {
        Assert.Equal(1234, UsbTransferPolicies.NormalizeTimeout(1234, 5000));
        Assert.Equal(1, UsbTransferPolicies.NormalizeTimeout(1, 5000));
    }

    [Fact]
    public void TransferPolicyConstants_AreReasonable()
    {
        Assert.True(UsbTransferPolicies.DefaultTimeoutMs > 0);
        Assert.True(UsbTransferPolicies.WinUsbDefaultTimeoutMs > 0);
        Assert.True(UsbTransferPolicies.MaxChunkSize > 0);
        Assert.True(UsbTransferPolicies.LinuxUsbFsMaxBulkSize > 0);
        Assert.True(UsbTransferPolicies.MaxChunkSize % UsbTransferPolicies.LinuxUsbFsMaxBulkSize == 0);
        Assert.True(UsbTransferPolicies.LinuxMaxRetries > 0);
    }

    // ---- UsbStringDescriptor ----

    [Fact]
    public void Decode_UTF16LE_SkipsHeaderAndDecodes()
    {
        // bLength=6, bDescriptorType=3, then "AB" in UTF-16LE.
        byte[] desc = { 6, 3, 0x41, 0x00, 0x42, 0x00 };
        Assert.Equal("AB", UsbStringDescriptor.Decode(desc, desc.Length));
    }

    [Fact]
    public void Decode_ShortPayload_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, UsbStringDescriptor.Decode(new byte[] { 2, 3 }, 2));
        Assert.Equal(string.Empty, UsbStringDescriptor.Decode(new byte[] { 1 }, 1));
    }

    [Fact]
    public void Decode_TrimsTrailingNulCharacters()
    {
        // "AB\0\0" in UTF-16LE with a 2-byte header; trailing NULs are trimmed.
        byte[] desc = { 8, 3, 0x41, 0x00, 0x42, 0x00, 0x00, 0x00 };
        Assert.Equal("AB", UsbStringDescriptor.Decode(desc, desc.Length));
    }

    [Fact]
    public void Decode_HonorsResponseLength()
    {
        // Buffer larger than the actual response; only responseLength bytes are used.
        byte[] desc = { 6, 3, 0x41, 0x00, 0x42, 0x00, 0xFF, 0xFF };
        Assert.Equal("AB", UsbStringDescriptor.Decode(desc, 6));
    }

    // ---- UsbStream ----

    [Fact]
    public void UsbStream_Read_DelegatesToSessionWithReadTimeout()
    {
        var session = new FakeSession { ReadBuffer = new byte[] { 1, 2, 3, 4 } };
        using var stream = session.AsStream();

        var buffer = new byte[8];
        int count = stream.Read(buffer, 1, 4);

        Assert.Equal(4, count);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer.Skip(1).Take(4).ToArray());
        Assert.Equal(session.DefaultTimeoutMs, session.LastReadTimeoutMs);
    }

    [Fact]
    public void UsbStream_Write_SlicesAndDelegates()
    {
        var session = new FakeSession();
        using var stream = session.AsStream();

        stream.Write(new byte[] { 9, 1, 2, 3, 9 }, 1, 3);

        Assert.Equal(new byte[] { 1, 2, 3 }, session.LastWrite);
    }

    [Fact]
    public void UsbStream_IsNotSeekable()
    {
        var session = new FakeSession();
        using var stream = session.AsStream();

        Assert.True(stream.CanRead);
        Assert.True(stream.CanWrite);
        Assert.False(stream.CanSeek);
        Assert.True(stream.CanTimeout);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => _ = stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.SetLength(10));
    }

    [Fact]
    public void UsbStream_Timeouts_DefaultFromSession()
    {
        var session = new FakeSession { DefaultTimeoutMs = 4321 };
        using var stream = session.AsStream();

        Assert.Equal(4321, stream.ReadTimeout);
        Assert.Equal(4321, stream.WriteTimeout);
    }

    [Fact]
    public void UsbStream_Read_InvalidArguments_Throw()
    {
        var session = new FakeSession();
        using var stream = session.AsStream();

        Assert.Throws<ArgumentNullException>(() => stream.Read(null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(new byte[4], -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(new byte[4], 0, 5));
    }

    [Fact]
    public void UsbStream_DoesNotOwnTheSession()
    {
        var session = new FakeSession();
        var stream = session.AsStream();
        stream.Dispose();

        // The session remains usable after the stream is disposed.
        Assert.False(session.Disposed);
        Assert.Equal(0, stream.Read(Array.Empty<byte>(), 0, 0));
    }

    private sealed class FakeSession : IUsbDeviceSession
    {
        public int DefaultTimeoutMs { get; set; } = 1234;
        public UsbDeviceInfo DeviceInfo { get; } = new UsbDeviceInfo();

        public byte[]? ReadBuffer { get; set; }
        public byte[]? LastWrite { get; private set; }
        public int LastReadTimeoutMs { get; private set; } = -1;
        public int LastWriteTimeoutMs { get; private set; } = -1;
        public bool Disposed { get; private set; }

        public byte[] Read(int length) => Read(length, DefaultTimeoutMs);

        public byte[] Read(int length, int timeoutMs)
        {
            LastReadTimeoutMs = timeoutMs;
            if (ReadBuffer == null || length <= 0) return Array.Empty<byte>();
            byte[] result = new byte[Math.Min(length, ReadBuffer.Length)];
            Array.Copy(ReadBuffer, result, result.Length);
            return result;
        }

        public int ReadInto(byte[] buffer, int offset, int length) => ReadInto(buffer, offset, length, DefaultTimeoutMs);

        public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
        {
            LastReadTimeoutMs = timeoutMs;
            if (ReadBuffer == null || length <= 0) return 0;
            int n = Math.Min(length, ReadBuffer.Length);
            Array.Copy(ReadBuffer, 0, buffer, offset, n);
            return n;
        }

        public long Write(byte[] data, int length) => Write(data, length, DefaultTimeoutMs);

        public long Write(byte[] data, int length, int timeoutMs)
        {
            LastWriteTimeoutMs = timeoutMs;
            LastWrite = new byte[length];
            Array.Copy(data, LastWrite, length);
            return length;
        }

        public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
            => throw new NotSupportedException();

        public void Reset() { }

        public void Dispose() => Disposed = true;
    }
}
