namespace FirmwareKit.Comm.Abstractions;

/// <summary>
/// Describes how recoverable USB transfer errors (e.g. transient Linux usbfs ioctl failures)
/// are retried by the backends.
/// <para>描述可恢复的 USB 传输错误（例如 Linux usbfs ioctl 的瞬时失败）由后端如何重试。</para>
/// The default policy matches the historical hard-coded behaviour (5 retries, 500 ms apart).
/// Set <see cref="UsbTransferPolicies.DefaultRetryPolicy"/> at startup to tune retry
/// behaviour for a whole process, e.g. a protocol layer that prefers fast-fail over long
/// blocking on flaky cables.
/// <para>默认策略与历史硬编码行为一致（重试 5 次、间隔 500 ms）。
/// 可在启动时设置 <see cref="UsbTransferPolicies.DefaultRetryPolicy"/> 来调整整个进程的
 /// 重试行为，例如协议层在劣质线缆上更倾向快速失败而非长时间阻塞。</para>
/// </summary>
public sealed class UsbTransferRetryPolicy
{
    /// <summary>
    /// Gets the default retry policy (5 retries, 500 ms apart).
    /// <para>获取默认重试策略（重试 5 次、间隔 500 ms）。</para>
    /// </summary>
    public static UsbTransferRetryPolicy Default { get; } = new UsbTransferRetryPolicy(maxRetries: 5, retryDelayMs: 500);

    /// <summary>
    /// Initializes a new retry policy.
    /// <para>初始化新的重试策略。</para>
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries for a recoverable error. <para>可恢复错误的最大重试次数。</para></param>
    /// <param name="retryDelayMs">The delay in milliseconds between retries. <para>重试之间的间隔（毫秒）。</para></param>
    public UsbTransferRetryPolicy(int maxRetries, int retryDelayMs)
    {
        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        }

        if (retryDelayMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelayMs));
        }

        MaxRetries = maxRetries;
        RetryDelayMs = retryDelayMs;
    }

    /// <summary>
    /// Gets the maximum number of retries for a recoverable error.
    /// <para>获取可恢复错误的最大重试次数。</para>
    /// </summary>
    public int MaxRetries { get; }

    /// <summary>
    /// Gets the delay in milliseconds between retries.
    /// <para>获取重试之间的间隔（毫秒）。</para>
    /// </summary>
    public int RetryDelayMs { get; }

    /// <inheritdoc />
    public override string ToString() => $"UsbTransferRetryPolicy(MaxRetries={MaxRetries}, RetryDelayMs={RetryDelayMs})";
}
