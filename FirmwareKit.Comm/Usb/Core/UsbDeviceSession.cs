using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;

namespace FirmwareKit.Comm.Usb.Core;

/// <summary>
/// Represents an active session over a USB device, implementing both synchronous and asynchronous I/O.
/// <para>表示 USB 设备上的活动会话，实现同步和异步 I/O。</para>
/// </summary>
internal sealed class UsbDeviceSession : IUsbDeviceSession, IAsyncUsbDeviceSession
{
    private readonly UsbDevice _device;

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

        return _device.Read(length);
    }

    public byte[] Read(int length, int timeoutMs)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return _device.Read(length, timeoutMs);
    }

    public int ReadInto(byte[] buffer, int offset, int length)
    {
        UsbDevice.ValidateBufferRange(buffer, offset, length);
        return _device.ReadInto(buffer, offset, length);
    }

    public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
    {
        UsbDevice.ValidateBufferRange(buffer, offset, length);
        return _device.ReadInto(buffer, offset, length, timeoutMs);
    }

    public Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        UsbDevice.ValidateBufferRange(buffer, offset, length);
        return _device.ReadIntoAsync(buffer, offset, length, timeoutMs, cancellationToken);
    }

    public long Write(byte[] data, int length)
    {
        UsbDevice.ValidateWriteData(data, length);
        return _device.Write(data, length);
    }

    public long Write(byte[] data, int length, int timeoutMs)
    {
        UsbDevice.ValidateWriteData(data, length);
        return _device.Write(data, length, timeoutMs);
    }

    public Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        UsbDevice.ValidateWriteData(data, length);
        return _device.WriteAsync(data, length, timeoutMs, cancellationToken);
    }

    public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
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

        return _device.ControlTransfer(setupPacket, buffer, offset, length, timeoutMs);
    }

    public Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return _device.ReadAsync(length, timeoutMs, cancellationToken);
    }

    public Task<int> ControlTransferAsync(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
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

        return _device.ControlTransferAsync(setupPacket, buffer, offset, length, timeoutMs, cancellationToken);
    }

    public void Reset() => _device.Reset();

    public Task ResetAsync(CancellationToken cancellationToken = default) => _device.ResetAsync(cancellationToken);

    public void Dispose() => _device.Dispose();

}