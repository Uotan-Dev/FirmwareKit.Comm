using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Backend;

namespace FirmwareKit.Comm.Core;

/// <summary>
/// Represents an active session over a USB device, implementing both synchronous and asynchronous I/O.
/// <para>表示 USB 设备上的活动会话，实现同步和异步 I/O。</para>
/// All operations are serialized through a gate so concurrent protocol threads (e.g. a status
/// thread plus a transfer thread in adb-style clients) cannot interleave transfers on the
/// same endpoints.
/// <para>所有操作通过门闩串行化，避免并发协议线程（例如 adb 风格客户端的状态线程与
/// 传输线程）在同一端点上交错传输。</para>
/// </summary>
internal sealed class UsbDeviceSession : IUsbDeviceSession, IAsyncUsbDeviceSession
{
    private readonly UsbDevice _device;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UsbDeviceSession(string apiName, UsbApiKind kind, UsbDevice device)
    {
        _device = device;
        DeviceInfo = UsbDeviceInfoFactory.FromBackendDevice(apiName, kind, device);
    }

    public int DefaultTimeoutMs => _device.DefaultTimeoutMs;

    public UsbDeviceInfo DeviceInfo { get; }

    public byte[] Read(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _gate.Wait();
        try
        {
            return _device.Read(length);
        }
        finally
        {
            _gate.Release();
        }
    }

    public byte[] Read(int length, int timeoutMs)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _gate.Wait();
        try
        {
            return _device.Read(length, timeoutMs);
        }
        finally
        {
            _gate.Release();
        }
    }

    public int ReadInto(byte[] buffer, int offset, int length)
    {
        UsbDevice.ValidateBufferRange(buffer, offset, length);
        _gate.Wait();
        try
        {
            return _device.ReadInto(buffer, offset, length);
        }
        finally
        {
            _gate.Release();
        }
    }

    public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
    {
        UsbDevice.ValidateBufferRange(buffer, offset, length);
        _gate.Wait();
        try
        {
            return _device.ReadInto(buffer, offset, length, timeoutMs);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        UsbDevice.ValidateBufferRange(buffer, offset, length);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _device.ReadIntoAsync(buffer, offset, length, timeoutMs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public long Write(byte[] data, int length)
    {
        UsbDevice.ValidateWriteData(data, length);
        _gate.Wait();
        try
        {
            return _device.Write(data, length);
        }
        finally
        {
            _gate.Release();
        }
    }

    public long Write(byte[] data, int length, int timeoutMs)
    {
        UsbDevice.ValidateWriteData(data, length);
        _gate.Wait();
        try
        {
            return _device.Write(data, length, timeoutMs);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        UsbDevice.ValidateWriteData(data, length);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _device.WriteAsync(data, length, timeoutMs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        ValidateControlTransfer(buffer, offset, length);
        _gate.Wait();
        try
        {
            return _device.ControlTransfer(setupPacket, buffer, offset, length, timeoutMs);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _device.ReadAsync(length, timeoutMs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> ControlTransferAsync(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        ValidateControlTransfer(buffer, offset, length);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _device.ControlTransferAsync(setupPacket, buffer, offset, length, timeoutMs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Reset()
    {
        _gate.Wait();
        try
        {
            _device.Reset();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _device.ResetAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        // Waiting here also drains any in-flight async operation before the device is released.
        _gate.Wait();
        try
        {
            _device.Dispose();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private static void ValidateControlTransfer(byte[]? buffer, int offset, int length)
    {
        if (buffer == null)
        {
            if (length != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
        }
        else
        {
            UsbDevice.ValidateBufferRange(buffer, offset, length);
        }

        if (length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "USB control transfers are limited to 65535 bytes (16-bit wLength).");
        }
    }
}
