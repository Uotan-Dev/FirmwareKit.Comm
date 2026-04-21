namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Provides async adapters for USB sessions.
/// 为 USB 会话提供异步适配器。
/// </summary>
public static class UsbDeviceSessionAsyncExtensions
{
    /// <summary>
    /// Converts a synchronous session into an async-capable session adapter.
    /// 将同步会话转换为支持异步调用的适配器。
    /// </summary>
    /// <param name="session">The source session. 源会话。</param>
    /// <returns>An async-capable session view. 支持异步调用的会话视图。</returns>
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
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _session.Read(length, timeoutMs);
            }, cancellationToken);
        }

        public Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _session.ReadInto(buffer, offset, length, timeoutMs);
            }, cancellationToken);
        }

        public Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _session.Write(data, length, timeoutMs);
            }, cancellationToken);
        }

        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _session.Reset();
            }, cancellationToken);
        }
    }
}
