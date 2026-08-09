using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Backend;
using FirmwareKit.Comm.Core;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Verifies the session's direction-gate design: reads and writes use independent gates so
/// a blocked read never stalls a concurrent write (full-duplex), while same-direction
/// operations remain strictly serialized to keep transfers on the same endpoint ordered.
/// <para>验证会话的方向门闩设计：读写使用独立门闩，使阻塞读不会阻塞并发的写（全双工），
/// 而同向操作保持严格串行，确保同一端点上的传输有序。</para>
/// </summary>
public sealed class UsbSessionConcurrencyTests
{
    [Fact]
    public async Task BlockedRead_DoesNotStallConcurrentWrite()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        // Start a read that blocks inside the device (holding the read gate).
        var readTask = Task.Run(() => session.Read(16, 30000), TestContext.Current.CancellationToken);
        Assert.True(SpinWaitUntil(() => device.ReadEntered > 0, TimeSpan.FromSeconds(5)), "read never reached the device");

        // A write must complete while the read is still blocked (full-duplex).
        var writeTask = Task.Run(() => session.Write(new byte[] { 1, 2, 3, 4 }, 4, 2000), TestContext.Current.CancellationToken);
        await AssertCompletesWithin(writeTask, TimeSpan.FromSeconds(2), "write was stalled by a blocked read");

        device.ReleaseReads();
        await AssertCompletesWithin(readTask, TimeSpan.FromSeconds(5), "read did not complete after release");
        Assert.Equal(1, device.WriteEntered);
    }

    [Fact]
    public async Task BlockedRead_DoesNotStallConcurrentAsyncWrite()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        var readTask = Task.Run(() => session.Read(16, 30000), TestContext.Current.CancellationToken);
        Assert.True(SpinWaitUntil(() => device.ReadEntered > 0, TimeSpan.FromSeconds(5)), "read never reached the device");

        var writeTask = session.WriteAsync(new byte[] { 1, 2, 3, 4 }, 4, 2000, TestContext.Current.CancellationToken);
        await AssertCompletesWithin(writeTask, TimeSpan.FromSeconds(2), "async write was stalled by a blocked read");

        device.ReleaseReads();
        await AssertCompletesWithin(readTask, TimeSpan.FromSeconds(5), "read did not complete after release");
        Assert.Equal(1, device.WriteEntered);
    }

    [Fact]
    public async Task SameDirectionReads_AreSerialized()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        var first = Task.Run(() => session.Read(16, 30000), TestContext.Current.CancellationToken);
        Assert.True(SpinWaitUntil(() => device.ReadEntered > 0, TimeSpan.FromSeconds(5)), "first read never reached the device");

        var second = Task.Run(() => session.Read(16, 30000), TestContext.Current.CancellationToken);
        Thread.Sleep(300);
        Assert.Equal(1, device.ReadEntered); // second read is queued behind the read gate

        device.ReleaseReads();
        await AssertCompletesWithin(first, TimeSpan.FromSeconds(5), "first read did not complete");
        await AssertCompletesWithin(second, TimeSpan.FromSeconds(5), "second read did not complete");
    }

    [Fact]
    public async Task Reset_WaitsForInFlightRead()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        var readTask = Task.Run(() => session.Read(16, 30000), TestContext.Current.CancellationToken);
        Assert.True(SpinWaitUntil(() => device.ReadEntered > 0, TimeSpan.FromSeconds(5)), "read never reached the device");

        // Reset must not overlap the in-flight read: it blocks until the read releases.
        var resetTask = Task.Run(() => session.Reset(), TestContext.Current.CancellationToken);
        Thread.Sleep(200);
        Assert.False(resetTask.IsCompleted);

        device.ReleaseReads();
        await AssertCompletesWithin(resetTask, TimeSpan.FromSeconds(5), "reset did not complete");
        await AssertCompletesWithin(readTask, TimeSpan.FromSeconds(5), "read did not complete after release");
        Assert.Equal(1, device.ResetCount);
    }

    [Fact]
    public void SetInterfaceAltSetting_IssuesStandardRequest()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        session.SetInterfaceAltSetting(interfaceNumber: 2, altSetting: 1);

        Assert.NotNull(device.LastSetupPacket);
        Assert.Equal(0x01, device.LastSetupPacket!.Value.RequestType); // host→device, standard, interface
        Assert.Equal(0x0B, device.LastSetupPacket.Value.Request);      // SET_INTERFACE
        Assert.Equal(1, device.LastSetupPacket.Value.Value);
        Assert.Equal(2, device.LastSetupPacket.Value.Index);
        Assert.Equal(0, device.LastSetupPacket.Value.Length);
    }

    [Fact]
    public void SetConfiguration_IssuesStandardRequest()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        session.SetConfiguration(configuration: 2);

        Assert.NotNull(device.LastSetupPacket);
        Assert.Equal(0x00, device.LastSetupPacket!.Value.RequestType); // host→device, standard, device
        Assert.Equal(0x09, device.LastSetupPacket.Value.Request);      // SET_CONFIGURATION
        Assert.Equal(2, device.LastSetupPacket.Value.Value);
        Assert.Equal(0, device.LastSetupPacket.Value.Index);
        Assert.Equal(0, device.LastSetupPacket.Value.Length);
    }

    [Fact]
    public void ReadInterrupt_DelegatesToDeviceWithEndpointAndBuffer()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        var buffer = new byte[16];
        // Request 8 bytes but the probe device returns only 4 -> short packet boundary.
        var result = session.ReadInterrupt(endpointAddress: 0x83, buffer, 2, 8, 1000);

        Assert.Equal(0x83, device.LastInterruptEndpoint);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, buffer.Skip(2).Take(4).ToArray());
        Assert.Equal(4, result.Count);
        Assert.True(result.IsShortPacket);
        Assert.False(result.IsTimeout);
    }

    [Fact]
    public void WriteInterrupt_DelegatesToDeviceWithEndpointAndOffset()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        long written = session.WriteInterrupt(endpointAddress: 0x03, new byte[] { 9, 1, 2, 3, 9 }, 1, 3, 1000);

        Assert.Equal(0x03, device.LastInterruptWriteEndpoint);
        Assert.Equal(new byte[] { 1, 2, 3 }, device.LastInterruptWrite);
        Assert.Equal(3, written);
    }

    [Fact]
    public void ControlWrite_BuildsStandardSetupPacket()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        session.ControlWrite(0x21, 0x22, value: 1, index: 0, new byte[] { 0xAA, 0xBB }, 1000);

        Assert.NotNull(device.LastSetupPacket);
        Assert.Equal(0x21, device.LastSetupPacket!.Value.RequestType);
        Assert.Equal(0x22, device.LastSetupPacket.Value.Request);
        Assert.Equal(1, device.LastSetupPacket.Value.Value);
        Assert.Equal(0, device.LastSetupPacket.Value.Index);
        Assert.Equal(2, device.LastSetupPacket.Value.Length);
    }

    [Fact]
    public void ControlRead_BuildsInDirectionPacket()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        _ = session.ControlRead(0xA1, 0x21, value: 0, index: 0, length: 7, 1000);

        Assert.NotNull(device.LastSetupPacket);
        Assert.Equal(0xA1, device.LastSetupPacket!.Value.RequestType);
        Assert.Equal(0x21, device.LastSetupPacket.Value.Request);
        Assert.Equal(7, device.LastSetupPacket.Value.Length);
    }

    [Fact]
    public void SetLineCoding_BuildsCdcClassRequest()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        session.SetLineCoding(interfaceNumber: 0, baudRate: 115200, charFormat: 0, parityType: 0, dataBits: 8, 1000);

        Assert.NotNull(device.LastSetupPacket);
        Assert.Equal(0x21, device.LastSetupPacket!.Value.RequestType); // host→device, class, interface
        Assert.Equal(0x20, device.LastSetupPacket.Value.Request);      // SET_LINE_CODING
        Assert.Equal(0, device.LastSetupPacket.Value.Value);
        Assert.Equal(0, device.LastSetupPacket.Value.Index);
        Assert.Equal(7, device.LastSetupPacket.Value.Length);          // 7-byte payload
    }

    [Fact]
    public void GetLineCoding_BuildsCdcClassRequest()
    {
        var device = new GateProbeDevice();
        using var session = new UsbDeviceSession("test", UsbApiKind.Native, device);

        _ = session.GetLineCoding(interfaceNumber: 0, 1000);

        Assert.NotNull(device.LastSetupPacket);
        Assert.Equal(0xA1, device.LastSetupPacket!.Value.RequestType); // device→host, class, interface
        Assert.Equal(0x21, device.LastSetupPacket.Value.Request);      // GET_LINE_CODING
        Assert.Equal(7, device.LastSetupPacket.Value.Length);
    }

    /// <summary>
    /// Asserts <paramref name="task"/> completes within <paramref name="timeout"/>, surfacing its
    /// exception when it faulted. Await-based alternative to Task.Wait that keeps the xUnit
    /// analyzers (xUnit1031/xUnit1051) quiet.
    /// <para>断言 <paramref name="task"/> 在 <paramref name="timeout"/> 内完成，出错时上抛其异常。
    /// 基于 await 的 Task.Wait 替代方案，避免触发 xUnit 分析器（xUnit1031/xUnit1051）。</para>
    /// </summary>
    private static async Task AssertCompletesWithin(Task task, TimeSpan timeout, string failureMessage)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout, TestContext.Current.CancellationToken));
        Assert.True(completed == task, failureMessage);
        await task;
    }

    private static bool SpinWaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            Thread.Sleep(10);
        }

        return condition();
    }

    /// <summary>
    /// Backend probe whose read blocks until <see cref="ReleaseReads"/> is called, letting
    /// tests observe gate behavior deterministically.
    /// <para>读取在调用 <see cref="ReleaseReads"/> 前阻塞的后端探针，使测试可确定性地观察门闩行为。</para>
    /// </summary>
    private sealed class GateProbeDevice : UsbDevice
    {
        private readonly ManualResetEventSlim _releaseReads = new(false);

        public int ReadEntered;
        public int WriteEntered;
        public int ResetCount;
        public UsbSetupPacket? LastSetupPacket;
        public byte LastInterruptEndpoint;
        public byte LastInterruptWriteEndpoint;
        public byte[]? LastInterruptWrite;

        public void ReleaseReads() => _releaseReads.Set();

        public override int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
        {
            LastSetupPacket = setupPacket;
            return 0;
        }

        public override UsbReadResult ReadInterrupt(byte endpointAddress, byte[] buffer, int offset, int length, int timeoutMs)
        {
            LastInterruptEndpoint = endpointAddress;
            byte[] data = { 0xAA, 0xBB, 0xCC, 0xDD };
            int n = Math.Min(length, data.Length);
            Array.Copy(data, 0, buffer, offset, n);
            return new UsbReadResult(n, isTimeout: false, isShortPacket: n < length);
        }

        public override long WriteInterrupt(byte endpointAddress, byte[] data, int offset, int length, int timeoutMs)
        {
            LastInterruptWriteEndpoint = endpointAddress;
            LastInterruptWrite = new byte[length];
            Array.Copy(data, offset, LastInterruptWrite, 0, length);
            return length;
        }

        protected override string BackendName => "gate-probe";

        protected override bool IsOpen => true;

        protected override UsbChunkResult ReadChunk(IntPtr buffer, int length, int timeoutMs)
            => throw new NotSupportedException();

        protected override UsbChunkResult WriteChunk(IntPtr buffer, int length, int timeoutMs)
            => throw new NotSupportedException();

        public override byte[] Read(int length)
        {
            Interlocked.Increment(ref ReadEntered);
            _releaseReads.Wait();
            return new byte[length];
        }

        public override long Write(byte[] data, int length, int timeoutMs)
        {
            Interlocked.Increment(ref WriteEntered);
            return length;
        }

        public override long Write(byte[] data, int length)
        {
            Interlocked.Increment(ref WriteEntered);
            return length;
        }

        public override int GetSerialNumber() => 0;

        public override int CreateHandle() => 0;

        public override void Reset() => Interlocked.Increment(ref ResetCount);

        public override void Dispose()
        {
            _releaseReads.Set();
            _releaseReads.Dispose();
        }
    }
}
