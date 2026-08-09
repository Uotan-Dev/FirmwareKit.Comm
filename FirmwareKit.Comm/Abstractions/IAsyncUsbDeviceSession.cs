namespace FirmwareKit.Comm.Abstractions;

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
    /// Reads into a caller-provided buffer asynchronously and reports the transfer outcome.
    /// <para>异步读取到调用方提供的缓冲区并报告传输结果。</para>
    /// See <see cref="IUsbDeviceSession.ReadPacket"/> for the short-packet/timeout distinction.
    /// <para>短包/超时区分参见 <see cref="IUsbDeviceSession.ReadPacket"/>。</para>
    /// </summary>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>读取字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the read result. <para>返回读取结果的任务。</para></returns>
    Task<UsbReadResult> ReadPacketAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads into a caller-provided buffer asynchronously, reporting cumulative bytes read
    /// after each completed chunk.
    /// <para>异步读取到调用方提供的缓冲区，并在每个分块完成后报告累计读取字节数。</para>
    /// </summary>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>读取字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="progress">Receives the cumulative transferred byte count after each chunk. <para>每块完成后接收累计传输字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the read result. <para>返回读取结果的任务。</para></returns>
    Task<UsbReadResult> ReadPacketAsync(byte[] buffer, int offset, int length, int timeoutMs, IProgress<long>? progress, CancellationToken cancellationToken = default);

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
    /// Writes bytes to the device asynchronously, starting at the specified offset.
    /// <para>异步从指定偏移量开始向设备写入字节数据。</para>
    /// </summary>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="offset">The offset into <paramref name="data"/> at which to start. <para><paramref name="data"/> 中的起始偏移量。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes written. <para>返回实际写入字节数的任务。</para></returns>
    Task<long> WriteAsync(byte[] data, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes bytes to the device asynchronously, reporting cumulative bytes written after
    /// each completed chunk.
    /// <para>异步从指定偏移量向设备写入字节数据，并在每个分块完成后报告累计写入字节数。</para>
    /// </summary>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="offset">The offset into <paramref name="data"/> at which to start. <para><paramref name="data"/> 中的起始偏移量。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="progress">Receives the cumulative transferred byte count after each chunk. <para>每块完成后接收累计传输字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes written. <para>返回实际写入字节数的任务。</para></returns>
    Task<long> WriteAsync(byte[] data, int offset, int length, int timeoutMs, IProgress<long>? progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a zero-length packet (ZLP) on the bulk OUT endpoint asynchronously.
    /// <para>异步在批量 OUT 端点上发送零长度包（ZLP）。</para>
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that completes when the zero-length packet is sent. <para>零长度包发送完成的任务。</para></returns>
    Task WriteZlpAsync(int timeoutMs, CancellationToken cancellationToken = default);

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
    /// Sets the active alternate setting of an interface asynchronously (SET_INTERFACE).
    /// <para>异步设置接口的活动备用设置（SET_INTERFACE）。</para>
    /// </summary>
    /// <param name="interfaceNumber">The interface number. <para>接口编号。</para></param>
    /// <param name="altSetting">The alternate setting to activate. <para>要激活的备用设置。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that completes when the setting is applied. <para>设置应用完成的任务。</para></returns>
    Task SetInterfaceAltSettingAsync(byte interfaceNumber, byte altSetting, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active device configuration asynchronously (SET_CONFIGURATION).
    /// <para>异步设置设备的活动配置（SET_CONFIGURATION）。</para>
    /// </summary>
    /// <param name="configuration">The configuration value to activate. <para>要激活的配置值。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that completes when the configuration is applied. <para>配置应用完成的任务。</para></returns>
    Task SetConfigurationAsync(byte configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads bytes from an interrupt endpoint into a caller-provided buffer asynchronously.
    /// <para>异步从中断端点将数据读取到调用方提供的缓冲区。</para>
    /// </summary>
    /// <param name="endpointAddress">The interrupt IN endpoint address (bit 7 set). <para>中断 IN 端点地址（bit 7 置位）。</para></param>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">Maximum number of bytes to read. <para>最多读取的字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the read result. <para>返回读取结果的任务。</para></returns>
    Task<UsbReadResult> ReadInterruptAsync(byte endpointAddress, byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes bytes to an interrupt endpoint asynchronously.
    /// <para>异步向中断端点写入字节数据。</para>
    /// </summary>
    /// <param name="endpointAddress">The interrupt OUT endpoint address (bit 7 clear). <para>中断 OUT 端点地址（bit 7 清零）。</para></param>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="offset">The offset into <paramref name="data"/> at which to start. <para><paramref name="data"/> 中的起始偏移量。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes written. <para>返回实际写入字节数的任务。</para></returns>
    Task<long> WriteInterruptAsync(byte endpointAddress, byte[] data, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the device transport asynchronously.
    /// <para>异步重置设备传输层。</para>
    /// </summary>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that completes when reset is done. <para>重置完成的任务。</para></returns>
    Task ResetAsync(CancellationToken cancellationToken = default);
}
