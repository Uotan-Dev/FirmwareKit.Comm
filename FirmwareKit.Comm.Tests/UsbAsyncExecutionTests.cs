using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the thread-pool helper UsbAsyncExecution used by the async adapters.
/// <para>覆盖异步适配器使用的线程池辅助类 UsbAsyncExecution。</para>
/// </summary>
public sealed class UsbAsyncExecutionTests
{
    [Fact]
    public async Task Run_ExecutesAction_AndCompletes()
    {
        bool ran = false;

        await UsbAsyncExecution.Run(() => ran = true, TestContext.Current.CancellationToken);

        Assert.True(ran);
    }

    [Fact]
    public async Task Run_AlreadyCanceled_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => UsbAsyncExecution.Run(() => { }, cts.Token));
    }

    [Fact]
    public async Task RunT_ReturnsResult()
    {
        var result = await UsbAsyncExecution.Run(() => 42, TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunT_AlreadyCanceled_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => UsbAsyncExecution.Run(() => 42, cts.Token));
    }

    [Fact]
    public async Task RunT_CancellationAfterStart_DoesNotAbortRunningAction()
    {
        // Cancellation is honored at the entry/checkpoint of UsbAsyncExecution, not by
        // aborting an action that already started: the action completes normally.
        // The cancellation must only fire AFTER the action has actually begun, so the
        // test synchronizes on a started signal instead of racing a fixed CancelAfter
        // delay (on a loaded CI runner the thread-pool lambda may not reach the action
        // before the delay fires, and the checkpoint throws OperationCanceledException).
        // <para>取消仅在 UsbAsyncExecution 的入口/检查点生效，不会中止已启动的操作：
        // 操作会正常完成。取消必须发生在操作真正开始之后，因此测试通过 started 信号同步，
        // 而不是与固定的 CancelAfter 延迟竞速（高负载 CI 上线程池 lambda 可能在延迟触发前
        // 尚未执行到操作，检查点会抛出 OperationCanceledException）。</para>
        using var cts = new CancellationTokenSource();
        using var started = new ManualResetEventSlim(false);
        var task = UsbAsyncExecution.Run(
            () =>
            {
                started.Set();
                Thread.Sleep(150);
                return 42;
            },
            cts.Token);

        Assert.True(started.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken), "action did not start");
        cts.Cancel();

        Assert.Equal(42, await task);
    }
}
