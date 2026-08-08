using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers ReadExact/ReadExactAsync aggregation and the AsAsync adapter.
/// <para>覆盖 ReadExact/ReadExactAsync 聚合与 AsAsync 适配器。</para>
/// </summary>
public sealed class ReadExactExtensionsTests
{
    [Fact]
    public void ReadExact_ReadsExactly_WhenEnoughData()
    {
        var session = new FakeSession { Pending = new byte[] { 1, 2, 3, 4, 5 } };

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, session.ReadExact(5, 1000));
    }

    [Fact]
    public void ReadExact_AccumulatesShortReads()
    {
        // FakeSession caps each ReadInto at 2 bytes, so 5 bytes require 3 reads.
        var session = new FakeSession { Pending = new byte[] { 1, 2, 3, 4, 5 }, MaxReadChunk = 2 };

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, session.ReadExact(5, 1000));
        Assert.True(session.ReadIntoCallCount >= 3);
    }

    [Fact]
    public void ReadExact_ShortData_ReturnsPartialBuffer()
    {
        var session = new FakeSession { Pending = new byte[] { 1, 2, 3 }, MaxReadChunk = 2 };

        Assert.Equal(new byte[] { 1, 2, 3 }, session.ReadExact(5, 1000));
    }

    [Fact]
    public void ReadExact_ZeroLength_ReturnsEmpty()
    {
        Assert.Empty(new FakeSession().ReadExact(0, 1000));
    }

    [Fact]
    public void ReadExact_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FakeSession().ReadExact(-1, 1000));
    }

    [Fact]
    public void ReadExact_NullSession_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IUsbDeviceSession)null!).ReadExact(1, 1000));
    }

    [Fact]
    public void ReadExact_ZeroTimeout_UsesSessionDefault()
    {
        var session = new FakeSession { DefaultTimeoutMs = 4321, Pending = new byte[] { 9 } };

        session.ReadExact(1, 0);

        Assert.Equal(4321, session.LastTimeoutMs);
    }

    [Fact]
    public async Task ReadExactAsync_AccumulatesShortReads()
    {
        var session = new FakeAsyncSession { Pending = new byte[] { 1, 2, 3, 4, 5 }, MaxReadChunk = 2 };

        var result = await session.ReadExactAsync(5, 1000, TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, result);
    }

    [Fact]
    public async Task ReadExactAsync_CanceledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new FakeAsyncSession().ReadExactAsync(5, 1000, cts.Token));
    }

    [Fact]
    public void AsAsync_SyncSession_Wraps_AndDelegates()
    {
        var session = new FakeSession { Pending = new byte[] { 7, 8, 9 } };
        var asyncSession = session.AsAsync();

        Assert.NotSame(session, asyncSession);
        Assert.Equal(1234, asyncSession.DefaultTimeoutMs);
    }

    [Fact]
    public void AsAsync_AsyncSession_ReturnsSameInstance()
    {
        var session = new FakeAsyncSession();

        Assert.Same(session, session.AsAsync());
    }

    [Fact]
    public void AsAsync_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IUsbDeviceSession)null!).AsAsync());
    }

    /// <summary>
    /// Synchronous fake session with configurable short reads.
    /// <para>可配置短读的同步假会话。</para>
    /// </summary>
    private sealed class FakeSession : IUsbDeviceSession
    {
        public int DefaultTimeoutMs { get; set; } = 1234;
        public UsbDeviceInfo DeviceInfo { get; } = new();

        public byte[] Pending { get; set; } = Array.Empty<byte>();
        public int MaxReadChunk { get; set; } = int.MaxValue;
        public int ReadPosition { get; private set; }
        public int ReadIntoCallCount { get; private set; }
        public int? LastTimeoutMs { get; private set; }

        public byte[] Read(int length) => Read(length, DefaultTimeoutMs);

        public byte[] Read(int length, int timeoutMs)
        {
            var buf = new byte[length];
            int n = ReadInto(buf, 0, length, timeoutMs);
            return buf[..n];
        }

        public int ReadInto(byte[] buffer, int offset, int length) => ReadInto(buffer, offset, length, DefaultTimeoutMs);

        public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
        {
            ReadIntoCallCount++;
            LastTimeoutMs = timeoutMs;
            if (ReadPosition >= Pending.Length || length <= 0) return 0;
            int n = Math.Min(Math.Min(length, Pending.Length - ReadPosition), MaxReadChunk);
            Array.Copy(Pending, ReadPosition, buffer, offset, n);
            ReadPosition += n;
            return n;
        }

        public long Write(byte[] data, int length) => length;

        public long Write(byte[] data, int length, int timeoutMs) => length;

        public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
            => throw new NotSupportedException();

        public void Reset() { }

        public void Dispose() { }
    }

    /// <summary>
    /// Async fake session for ReadExactAsync / AsAsync passthrough tests.
    /// <para>用于 ReadExactAsync / AsAsync 直通测试的异步假会话。</para>
    /// </summary>
    private sealed class FakeAsyncSession : IUsbDeviceSession, IAsyncUsbDeviceSession
    {
        public int DefaultTimeoutMs { get; set; } = 1234;
        public UsbDeviceInfo DeviceInfo { get; } = new();

        public byte[] Pending { get; set; } = Array.Empty<byte>();
        public int MaxReadChunk { get; set; } = int.MaxValue;
        public int ReadPosition { get; private set; }

        public byte[] Read(int length) => throw new NotSupportedException();

        public byte[] Read(int length, int timeoutMs) => throw new NotSupportedException();

        public int ReadInto(byte[] buffer, int offset, int length) => throw new NotSupportedException();

        public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs) => throw new NotSupportedException();

        public long Write(byte[] data, int length) => throw new NotSupportedException();

        public long Write(byte[] data, int length, int timeoutMs) => throw new NotSupportedException();

        public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
            => throw new NotSupportedException();

        public void Reset() { }

        public void Dispose() { }

        public Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ReadPosition >= Pending.Length || length <= 0) return Task.FromResult(0);
            int n = Math.Min(Math.Min(length, Pending.Length - ReadPosition), MaxReadChunk);
            Array.Copy(Pending, ReadPosition, buffer, offset, n);
            ReadPosition += n;
            return Task.FromResult(n);
        }

        public Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
            => Task.FromResult((long)length);

        public Task<int> ControlTransferAsync(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ResetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
