namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Represents an opened USB device session with asynchronous I/O methods.
/// <para>表示支持异步 I/O 的已打开 USB 设备会话。</para>
/// </summary>
public interface IAsyncUsbDeviceSession
{
    /// <summary>
    /// Gets the default timeout used by this session, if the caller omits one.
    /// <para>获取该会话在调用方未显式指定超时时使用的默认超时。</para>
    /// </summary>
    int DefaultTimeoutMs { get; }

    /// <summary>
    /// Reads up to the specified number of bytes asynchronously.
    /// <para>异步读取最多指定字节数的数据。</para>
    /// </summary>
    /// <param name="length">Maximum number of bytes to read. <para>最多读取的字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the bytes read. <para>返回读取字节数组的任务。</para></returns>
    Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads into a caller-provided buffer asynchronously.
    /// <para>异步读取到调用方提供的缓冲区。</para>
    /// </summary>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>读取字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes read. <para>返回实际读取字节数的任务。</para></returns>
    Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes bytes to the device asynchronously.
    /// <para>异步向设备写入字节数据。</para>
    /// </summary>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes written. <para>返回实际写入字节数的任务。</para></returns>
    Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends or receives a USB control transfer asynchronously.
    /// <para>异步发送或接收 USB 控制传输。</para>
    /// </summary>
    /// <param name="setupPacket">The setup packet. <para>setup 包。</para></param>
    /// <param name="buffer">The data buffer, or <c>null</c> for a zero-length transfer. <para>数据缓冲区，零长度传输可传 <c>null</c>。</para></param>
    /// <param name="offset">The buffer offset. <para>缓冲区偏移量。</para></param>
    /// <param name="length">The number of bytes to transfer. <para>传输字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes transferred. <para>返回实际传输字节数的任务。</para></returns>
    Task<int> ControlTransferAsync(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the device transport asynchronously.
    /// <para>异步重置设备传输层。</para>
    /// </summary>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that completes when reset is done. <para>重置完成的任务。</para></returns>
    Task ResetAsync(CancellationToken cancellationToken = default);
}
