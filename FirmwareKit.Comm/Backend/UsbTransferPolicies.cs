namespace FirmwareKit.Comm.Backend;

/// <summary>
/// Centralizes shared USB transfer policy constants and timeout normalization logic.
/// <para>集中管理共享的 USB 传输策略常量及超时规范化逻辑。</para>
/// </summary>
internal static class UsbTransferPolicies
{
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
    /// </summary>
    /// <param name="timeoutMs">The requested timeout in milliseconds. <para>请求的超时时间（毫秒）。</para></param>
    /// <param name="defaultTimeoutMs">The fallback default timeout. <para>回退的默认超时时间。</para></param>
    /// <returns>The normalized timeout value. <para>规范化后的超时值。</para></returns>
    public static int NormalizeTimeout(int timeoutMs, int defaultTimeoutMs)
    {
        return timeoutMs > 0 ? timeoutMs : defaultTimeoutMs;
    }
}
