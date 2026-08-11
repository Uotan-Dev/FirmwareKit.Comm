namespace FirmwareKit.Comm.Abstractions;

/// <summary>
/// Provides async adapters for USB sessions.
/// <para>为 USB 会话提供异步适配器。</para>
/// </summary>
public static class UsbDeviceSessionAsyncExtensions
{
    /// <summary>
    /// Converts a synchronous session into an async-capable session adapter.
    /// <para>将同步会话转换为支持异步调用的适配器。</para>
    /// </summary>
    /// <param name="session">The source session. <para>源会话。</para></param>
    /// <returns>An async-capable session view. <para>支持异步调用的会话视图。</para></returns>
    public static IAsyncUsbDeviceSession AsAsync(this IUsbDeviceSession session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (session is IAsyncUsbDeviceSession asyncSession)
        {
            return asyncSession;
        }

        return new AsyncUsbDeviceSessionAdapter(session);
    }

    /// <summary>
    /// Reads up to the specified number of bytes asynchronously, using the session default timeout.
    /// <para>异步读取最多指定字节数，使用会话默认超时。</para>
    /// </summary>
    /// <param name="session">The async session. <para>异步会话。</para></param>
    /// <param name="length">Maximum number of bytes to read. <para>最多读取的字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the bytes read. <para>返回读取字节数组的任务。</para></returns>
    public static Task<byte[]> ReadAsync(this IAsyncUsbDeviceSession session, int length, CancellationToken cancellationToken = default)
        => session.ReadAsync(length, session.DefaultTimeoutMs, cancellationToken);

    /// <summary>
    /// Reads into a caller-provided buffer asynchronously, using the session default timeout.
    /// <para>异步读取到调用方提供的缓冲区，使用会话默认超时。</para>
    /// </summary>
    /// <param name="session">The async session. <para>异步会话。</para></param>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>读取字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes read. <para>返回实际读取字节数的任务。</para></returns>
    public static Task<int> ReadIntoAsync(this IAsyncUsbDeviceSession session, byte[] buffer, int offset, int length, CancellationToken cancellationToken = default)
        => session.ReadIntoAsync(buffer, offset, length, session.DefaultTimeoutMs, cancellationToken);

    /// <summary>
    /// Reads into a caller-provided buffer asynchronously and reports the transfer outcome,
    /// using the session default timeout.
    /// <para>异步读取到调用方提供的缓冲区并报告传输结果，使用会话默认超时。</para>
    /// </summary>
    /// <param name="session">The async session. <para>异步会话。</para></param>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>读取字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the read result. <para>返回读取结果的任务。</para></returns>
    public static Task<UsbReadResult> ReadPacketAsync(this IAsyncUsbDeviceSession session, byte[] buffer, int offset, int length, CancellationToken cancellationToken = default)
        => session.ReadPacketAsync(buffer, offset, length, session.DefaultTimeoutMs, cancellationToken);

    /// <summary>
    /// Writes bytes to the device asynchronously, using the session default timeout.
    /// <para>异步向设备写入字节数据，使用会话默认超时。</para>
    /// </summary>
    /// <param name="session">The async session. <para>异步会话。</para></param>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes written. <para>返回实际写入字节数的任务。</para></returns>
    public static Task<long> WriteAsync(this IAsyncUsbDeviceSession session, byte[] data, int length, CancellationToken cancellationToken = default)
        => session.WriteAsync(data, length, session.DefaultTimeoutMs, cancellationToken);

    /// <summary>
    /// Writes bytes to the device asynchronously starting at the specified offset,
    /// using the session default timeout.
    /// <para>异步从指定偏移量开始向设备写入字节数据，使用会话默认超时。</para>
    /// </summary>
    /// <param name="session">The async session. <para>异步会话。</para></param>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="offset">The offset into <paramref name="data"/> at which to start. <para><paramref name="data"/> 中的起始偏移量。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes written. <para>返回实际写入字节数的任务。</para></returns>
    public static Task<long> WriteAsync(this IAsyncUsbDeviceSession session, byte[] data, int offset, int length, CancellationToken cancellationToken = default)
        => session.WriteAsync(data, offset, length, session.DefaultTimeoutMs, cancellationToken);

    /// <summary>
    /// Sends a zero-length packet (ZLP) asynchronously using the session default timeout.
    /// <para>异步发送零长度包（ZLP），使用会话默认超时。</para>
    /// </summary>
    /// <param name="session">The async session. <para>异步会话。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that completes when the ZLP is sent. <para>ZLP 发送完成的任务。</para></returns>
    public static Task WriteZlpAsync(this IAsyncUsbDeviceSession session, CancellationToken cancellationToken = default)
        => session.WriteZlpAsync(session.DefaultTimeoutMs, cancellationToken);

    /// <summary>
    /// Sends or receives a USB control transfer asynchronously, using the session default timeout.
    /// <para>异步发送或接收 USB 控制传输，使用会话默认超时。</para>
    /// </summary>
    /// <param name="session">The async session. <para>异步会话。</para></param>
    /// <param name="setupPacket">The setup packet. <para>setup 包。</para></param>
    /// <param name="buffer">The data buffer, or <c>null</c> for a zero-length transfer. <para>数据缓冲区，零长度传输可传 <c>null</c>。</para></param>
    /// <param name="offset">The buffer offset. <para>缓冲区偏移量。</para></param>
    /// <param name="length">The number of bytes to transfer. <para>传输字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes transferred. <para>返回实际传输字节数的任务。</para></returns>
    public static Task<int> ControlTransferAsync(this IAsyncUsbDeviceSession session, UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, CancellationToken cancellationToken = default)
        => session.ControlTransferAsync(setupPacket, buffer, offset, length, session.DefaultTimeoutMs, cancellationToken);

    /// <summary>
    /// Reads bytes from an interrupt endpoint asynchronously, using the session default timeout.
    /// <para>异步从中断端点读取字节数据，使用会话默认超时。</para>
    /// </summary>
    /// <param name="session">The async session. <para>异步会话。</para></param>
    /// <param name="endpointAddress">The interrupt IN endpoint address (bit 7 set). <para>中断 IN 端点地址（bit 7 置位）。</para></param>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">Maximum number of bytes to read. <para>最多读取的字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the read result. <para>返回读取结果的任务。</para></returns>
    public static Task<UsbReadResult> ReadInterruptAsync(this IAsyncUsbDeviceSession session, byte endpointAddress, byte[] buffer, int offset, int length, CancellationToken cancellationToken = default)
        => session.ReadInterruptAsync(endpointAddress, buffer, offset, length, session.DefaultTimeoutMs, cancellationToken);

    /// <summary>
    /// Writes bytes to an interrupt endpoint asynchronously, using the session default timeout.
    /// <para>异步向中断端点写入字节数据，使用会话默认超时。</para>
    /// </summary>
    /// <param name="session">The async session. <para>异步会话。</para></param>
    /// <param name="endpointAddress">The interrupt OUT endpoint address (bit 7 clear). <para>中断 OUT 端点地址（bit 7 清零）。</para></param>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="offset">The offset into <paramref name="data"/> at which to start. <para><paramref name="data"/> 中的起始偏移量。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the number of bytes written. <para>返回实际写入字节数的任务。</para></returns>
    public static Task<long> WriteInterruptAsync(this IAsyncUsbDeviceSession session, byte endpointAddress, byte[] data, int offset, int length, CancellationToken cancellationToken = default)
        => session.WriteInterruptAsync(endpointAddress, data, offset, length, session.DefaultTimeoutMs, cancellationToken);

    private sealed class AsyncUsbDeviceSessionAdapter : IAsyncUsbDeviceSession
    {
        private readonly IUsbDeviceSession _session;

        public AsyncUsbDeviceSessionAdapter(IUsbDeviceSession session)
        {
            _session = session;
        }

        public int DefaultTimeoutMs => _session.DefaultTimeoutMs;

        /// <inheritdoc />
        public UsbDeviceInfo DeviceInfo => _session.DeviceInfo;

        /// <inheritdoc />
        public byte EndpointIn => _session.EndpointIn;

        /// <inheritdoc />
        public byte EndpointOut => _session.EndpointOut;

        public Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.Read(length, timeoutMs), cancellationToken);
        }

        public Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.ReadInto(buffer, offset, length, timeoutMs), cancellationToken);
        }

        public Task<UsbReadResult> ReadPacketAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.ReadPacket(buffer, offset, length, timeoutMs), cancellationToken);
        }

        public Task<UsbReadResult> ReadPacketAsync(byte[] buffer, int offset, int length, int timeoutMs, IProgress<long>? progress, CancellationToken cancellationToken = default)
        {
            if (progress == null)
            {
                return ReadPacketAsync(buffer, offset, length, timeoutMs, cancellationToken);
            }

            // 同步会话接口没有带进度的 ReadPacket，模拟基类分块循环并逐块报告。
            return UsbAsyncExecution.Run(() =>
            {
                int total = 0;
                var last = new UsbReadResult(0, isTimeout: false, isShortPacket: false);
                int remaining = length;
                while (remaining > 0)
                {
                    int lenToRead = Math.Min(remaining, FirmwareKit.Comm.Backend.UsbTransferPolicies.MaxChunkSize);
                    last = _session.ReadPacket(buffer, offset + total, lenToRead, timeoutMs);
                    total += last.Count;
                    remaining -= last.Count;
                    progress.Report(total);
                    if (last.Count < lenToRead) break; // short packet or timeout ends the message
                }

                return last;
            }, cancellationToken);
        }

        public Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.Write(data, length, timeoutMs), cancellationToken);
        }

        public Task WriteZlpAsync(int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.WriteZlp(timeoutMs), cancellationToken);
        }

        public Task<long> WriteAsync(byte[] data, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.Write(data, offset, length, timeoutMs), cancellationToken);
        }

        public Task<long> WriteAsync(byte[] data, int offset, int length, int timeoutMs, IProgress<long>? progress, CancellationToken cancellationToken = default)
        {
            if (progress == null)
            {
                return WriteAsync(data, offset, length, timeoutMs, cancellationToken);
            }

            // 同步会话接口没有带进度的 Write，模拟基类分块循环并逐块报告。
            return UsbAsyncExecution.Run(() =>
            {
                long total = 0;
                int remaining = length;
                while (remaining > 0)
                {
                    int lenToSend = Math.Min(remaining, FirmwareKit.Comm.Backend.UsbTransferPolicies.MaxChunkSize);
                    long written = _session.Write(data, offset + (int)total, lenToSend, timeoutMs);
                    total += written;
                    remaining -= (int)written;
                    progress.Report(total);
                    if (written < lenToSend) break;
                }

                return total;
            }, cancellationToken);
        }

        public Task<int> ControlTransferAsync(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.ControlTransfer(setupPacket, buffer, offset, length, timeoutMs), cancellationToken);
        }

        public Task SetInterfaceAltSettingAsync(byte interfaceNumber, byte altSetting, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.SetInterfaceAltSetting(interfaceNumber, altSetting), cancellationToken);
        }

        public Task SetConfigurationAsync(byte configuration, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.SetConfiguration(configuration), cancellationToken);
        }

        public Task<UsbReadResult> ReadInterruptAsync(byte endpointAddress, byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.ReadInterrupt(endpointAddress, buffer, offset, length, timeoutMs), cancellationToken);
        }

        public Task<long> WriteInterruptAsync(byte endpointAddress, byte[] data, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.WriteInterrupt(endpointAddress, data, offset, length, timeoutMs), cancellationToken);
        }

        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(_session.Reset, cancellationToken);
        }

        /// <summary>
        /// Disposes the underlying synchronous session.
        /// <para>释放底层同步会话。</para>
        /// </summary>
        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
