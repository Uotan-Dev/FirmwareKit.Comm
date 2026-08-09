using System.Diagnostics;
using FirmwareKit.Comm.Backend;

namespace FirmwareKit.Comm.Abstractions;

/// <summary>
/// Additional read/write helpers for USB sessions.
/// <para>USB 会话的附加读写辅助方法。</para>
/// </summary>
public static class UsbDeviceSessionExtensions
{
    /// <summary>
    /// Reads exactly <paramref name="length"/> bytes, looping over short reads until the
    /// buffer is full or the operation timeout elapses.
    /// <para>读取恰好 <paramref name="length"/> 字节：在短读时循环读取，
    /// 直到缓冲区填满或操作超时。</para>
    /// Unlike <see cref="IUsbDeviceSession.Read(int,int)"/>, which returns after the first
    /// short packet, this is useful for protocol layers (fastboot/EDL) that must consume a
    /// fixed-size response.
    /// <para>与遇短包即返回的 <see cref="IUsbDeviceSession.Read(int,int)"/> 不同，
    /// 本方法适合必须读取定长响应的协议层（fastboot/EDL）。</para>
    /// </summary>
    /// <param name="session">The source session. <para>源会话。</para></param>
    /// <param name="length">The exact number of bytes to read. <para>要读取的字节数。</para></param>
    /// <param name="timeoutMs">Operation timeout; zero/negative substitutes the session default. <para>操作超时；零或负数时使用会话默认值。</para></param>
    /// <returns>
    /// A buffer of <paramref name="length"/> bytes when the read completed, or a shorter
    /// buffer with the bytes actually received when it timed out.
    /// <para>读取完成时返回 <paramref name="length"/> 字节的缓冲区；超时时返回实际收到的较短缓冲区。</para>
    /// </returns>
    public static byte[] ReadExact(this IUsbDeviceSession session, int length, int timeoutMs)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (length > UsbTransferPolicies.MaxReadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"length exceeds the safety cap of {UsbTransferPolicies.MaxReadLength} bytes; clamp device-provided frame lengths.");
        }

        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        int effectiveTimeout = UsbTransferPolicies.NormalizeTimeout(timeoutMs, session.DefaultTimeoutMs);
        var stopwatch = Stopwatch.StartNew();
        byte[] buffer = new byte[length];
        int count = 0;
        while (count < length)
        {
            int remaining;
            if (effectiveTimeout == UsbTransferPolicies.InfiniteTimeoutMs)
            {
                // Unbounded wait: no deadline check, pass the sentinel straight through.
                remaining = UsbTransferPolicies.InfiniteTimeoutMs;
            }
            else
            {
                long elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed >= effectiveTimeout)
                {
                    break;
                }

                remaining = effectiveTimeout - (int)elapsed;
            }

            int read = session.ReadInto(buffer, count, length - count, remaining);
            if (read <= 0)
            {
                break;
            }

            count += read;
        }

        if (count == length)
        {
            return buffer;
        }

        byte[] partial = new byte[count];
        Buffer.BlockCopy(buffer, 0, partial, 0, count);
        return partial;
    }

    /// <summary>
    /// Asynchronously reads exactly <paramref name="length"/> bytes, looping over short reads
    /// until the buffer is full or the operation timeout elapses.
    /// <para>异步读取恰好 <paramref name="length"/> 字节：在短读时循环读取，
    /// 直到缓冲区填满或操作超时。</para>
    /// </summary>
    /// <param name="session">The source session. <para>源会话。</para></param>
    /// <param name="length">The exact number of bytes to read. <para>要读取的字节数。</para></param>
    /// <param name="timeoutMs">Operation timeout; zero/negative substitutes the session default. <para>操作超时；零或负数时使用会话默认值。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>
    /// A buffer of <paramref name="length"/> bytes when the read completed, or a shorter
    /// buffer with the bytes actually received when it timed out.
    /// <para>读取完成时返回 <paramref name="length"/> 字节的缓冲区；超时时返回实际收到的较短缓冲区。</para>
    /// </returns>
    public static async Task<byte[]> ReadExactAsync(this IAsyncUsbDeviceSession session, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (length > UsbTransferPolicies.MaxReadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"length exceeds the safety cap of {UsbTransferPolicies.MaxReadLength} bytes; clamp device-provided frame lengths.");
        }

        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        int effectiveTimeout = UsbTransferPolicies.NormalizeTimeout(timeoutMs, session.DefaultTimeoutMs);
        var stopwatch = Stopwatch.StartNew();
        byte[] buffer = new byte[length];
        int count = 0;
        while (count < length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int remaining;
            if (effectiveTimeout == UsbTransferPolicies.InfiniteTimeoutMs)
            {
                // Unbounded wait: no deadline check, pass the sentinel straight through.
                remaining = UsbTransferPolicies.InfiniteTimeoutMs;
            }
            else
            {
                long elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed >= effectiveTimeout)
                {
                    break;
                }

                remaining = effectiveTimeout - (int)elapsed;
            }

            int read = await session.ReadIntoAsync(buffer, count, length - count, remaining, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            count += read;
        }

        if (count == length)
        {
            return buffer;
        }

        byte[] partial = new byte[count];
        Buffer.BlockCopy(buffer, 0, partial, 0, count);
        return partial;
    }
}
