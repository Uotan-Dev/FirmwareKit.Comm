namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Represents an opened USB device session with asynchronous I/O methods.
/// 表示支持异步 I/O 的已打开 USB 设备会话。
/// </summary>
public interface IAsyncUsbDeviceSession
{
    /// <summary>
    /// Gets the default timeout used by this session, if the caller omits one.
    /// 获取该会话在调用方未显式指定超时时使用的默认超时。
    /// </summary>
    int DefaultTimeoutMs { get; }

    /// <summary>
    /// Reads up to the specified number of bytes asynchronously.
    /// 异步读取最多指定字节数的数据。
    /// </summary>
    /// <param name="length">Maximum number of bytes to read. 最多读取的字节数。</param>
    /// <param name="timeoutMs">Timeout in milliseconds. 超时时间（毫秒）。</param>
    /// <param name="cancellationToken">A cancellation token. 取消令牌。</param>
    /// <returns>A task that resolves to the bytes read. 返回读取字节数组的任务。</returns>
    Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads into a caller-provided buffer asynchronously.
    /// 异步读取到调用方提供的缓冲区。
    /// </summary>
    /// <param name="buffer">The destination buffer. 目标缓冲区。</param>
    /// <param name="offset">The destination offset. 目标偏移量。</param>
    /// <param name="length">The number of bytes to read. 读取字节数。</param>
    /// <param name="timeoutMs">Timeout in milliseconds. 超时时间（毫秒）。</param>
    /// <param name="cancellationToken">A cancellation token. 取消令牌。</param>
    /// <returns>A task that resolves to the number of bytes read. 返回实际读取字节数的任务。</returns>
    Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes bytes to the device asynchronously.
    /// 异步向设备写入字节数据。
    /// </summary>
    /// <param name="data">The data to write. 待写入数据。</param>
    /// <param name="length">The number of bytes to write. 写入字节数。</param>
    /// <param name="timeoutMs">Timeout in milliseconds. 超时时间（毫秒）。</param>
    /// <param name="cancellationToken">A cancellation token. 取消令牌。</param>
    /// <returns>A task that resolves to the number of bytes written. 返回实际写入字节数的任务。</returns>
    Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the device transport asynchronously.
    /// 异步重置设备传输层。
    /// </summary>
    /// <param name="cancellationToken">A cancellation token. 取消令牌。</param>
    /// <returns>A task that completes when reset is done. 重置完成的任务。</returns>
    Task ResetAsync(CancellationToken cancellationToken = default);
}
