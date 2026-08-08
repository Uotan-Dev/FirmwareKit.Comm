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

        public Task<UsbReadResult> ReadPacketAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.ReadPacket(buffer, offset, length, timeoutMs), cancellationToken);
        }

        public Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.Write(data, length, timeoutMs), cancellationToken);
        }

        public Task<long> WriteAsync(byte[] data, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return UsbAsyncExecution.Run(() => _session.Write(data, offset, length, timeoutMs), cancellationToken);
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
