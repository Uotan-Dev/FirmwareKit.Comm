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

    private sealed class AsyncUsbDeviceSessionAdapter : IAsyncUsbDeviceSession
    {
        private readonly IUsbDeviceSession _session;

        public AsyncUsbDeviceSessionAdapter(IUsbDeviceSession session)
        {
            _session = session;
        }

        public int DefaultTimeoutMs => _session.DefaultTimeoutMs;

        public Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.Read(length, timeoutMs), cancellationToken);
        }

        public Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.ReadInto(buffer, offset, length, timeoutMs), cancellationToken);
        }

#if NET8_0_OR_GREATER
        public int ReadInto(Span<byte> buffer, int timeoutMs)
        {
            return _session.ReadInto(buffer, timeoutMs);
        }
#endif

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

            // The sync session interface has no progress-aware ReadPacket, so emulate the
            // base chunk loop here and report cumulative bytes after each chunk.
            // <para>同步会话接口没有带进度的 ReadPacket，因此在此模拟基类的分块循环，
            // 并在每个分块完成后报告累计字节数。</para>
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

            // The sync session interface has no progress-aware Write, so emulate the
            // base chunk loop here and report cumulative bytes after each chunk.
            // <para>同步会话接口没有带进度的 Write，因此在此模拟基类的分块循环，
            // 并在每个分块完成后报告累计字节数。</para>
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
    }
}
