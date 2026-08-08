namespace FirmwareKit.Comm.Backend;

/// <summary>
/// Centralizes shared USB transfer policy constants and timeout normalization logic.
/// <para>集中管理共享的 USB 传输策略常量及超时规范化逻辑。</para>
/// </summary>
internal static class UsbTransferPolicies
{
    /// <summary>
    /// The sentinel timeout value meaning "wait indefinitely".
    /// <para>表示"无限等待"的哨兵超时值。</para>
    /// Passing <see cref="InfiniteTimeoutMs"/> (-1) as a transfer timeout blocks until the
    /// transfer completes or the device disconnects; it is the only way to express
    /// "no deadline" (0 and other non-positive values fall back to the default timeout).
    /// <para>将 <see cref="InfiniteTimeoutMs"/>（-1）作为传输超时传入时会阻塞直到传输完成
    /// 或设备断开；这是表达"无期限"的唯一方式（0 及其他非正值回退到默认超时）。</para>
    /// </summary>
    public const int InfiniteTimeoutMs = -1;

    /// <summary>
    /// The default timeout in milliseconds for USB transfers.
    /// <para>USB 传输的默认超时时间（毫秒）。</para>
    /// </summary>
    public const int DefaultTimeoutMs = 5000;

    /// <summary>
    /// The default timeout in milliseconds for WinUSB operations.
    /// <para>WinUSB 操作的默认超时时间（毫秒）。</para>
    /// </summary>
    public const int WinUsbDefaultTimeoutMs = 60000;

    /// <summary>
    /// The maximum chunk size in bytes for a single USB transfer.
    /// <para>单次 USB 传输的最大块大小（字节）。</para>
    /// </summary>
    public const int MaxChunkSize = 1024 * 1024;

    /// <summary>
    /// The maximum bulk transfer size for the Linux usbfs backend.
    /// <para>Linux usbfs 后端的最大批量传输大小。</para>
    /// </summary>
    public const int LinuxUsbFsMaxBulkSize = 16384;

    /// <summary>
    /// The maximum number of retries for recoverable Linux USB transfer errors.
    /// <para>可恢复的 Linux USB 传输错误的最大重试次数。</para>
    /// </summary>
    public const int LinuxMaxRetries = 5;

    /// <summary>
    /// Returns the effective timeout, substituting the default when the provided value is zero or negative.
    /// <para>返回有效的超时值；当提供的值为零或负数时替换为默认值。</para>
    /// The <see cref="InfiniteTimeoutMs"/> (-1) sentinel is preserved verbatim so callers can
    /// request an unbounded wait; all other non-positive values fall back to the default.
    /// <para><see cref="InfiniteTimeoutMs"/>（-1）哨兵值原样保留，供调用方请求无期限等待；
    /// 其余非正值回退到默认值。</para>
    /// </summary>
    /// <param name="timeoutMs">The requested timeout in milliseconds. <para>请求的超时时间（毫秒）。</para></param>
    /// <param name="defaultTimeoutMs">The fallback default timeout. <para>回退的默认超时时间。</para></param>
    /// <returns>The normalized timeout value. <para>规范化后的超时值。</para></returns>
    public static int NormalizeTimeout(int timeoutMs, int defaultTimeoutMs)
    {
        if (timeoutMs == InfiniteTimeoutMs)
        {
            return InfiniteTimeoutMs;
        }

        return timeoutMs > 0 ? timeoutMs : defaultTimeoutMs;
    }
}
