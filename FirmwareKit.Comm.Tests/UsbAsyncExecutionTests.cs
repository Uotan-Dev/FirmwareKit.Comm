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
        using var cts = new CancellationTokenSource();
        var task = UsbAsyncExecution.Run(
            () =>
            {
                Thread.Sleep(150);
                return 42;
            },
            cts.Token);
        cts.CancelAfter(30);

        Assert.Equal(42, await task);
    }
}
