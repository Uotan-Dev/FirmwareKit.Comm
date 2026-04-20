using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;

namespace FirmwareKit.Comm.Usb.Core;

internal sealed class UsbDeviceSession : IUsbDeviceSession, IAsyncUsbDeviceSession
{
    private readonly UsbDevice _device;

    public UsbDeviceSession(string apiName, UsbApiKind kind, UsbDevice device)
    {
        _device = device;
        DeviceInfo = new UsbDeviceInfo
        {
            ApiName = apiName,
            SourceApiKind = kind,
            SourceDeviceType = device.GetType().Name,
            DevicePath = device.DevicePath,
            SerialNumber = device.SerialNumber,
            VendorId = device.VendorId,
            ProductId = device.ProductId,
            InterfaceClass = device.InterfaceClass,
            InterfaceSubClass = device.InterfaceSubClass,
            InterfaceProtocol = device.InterfaceProtocol
        };
    }

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
        ValidateBufferRange(buffer, offset, length);
        return _device.ReadInto(buffer, offset, length);
    }

    public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
    {
        ValidateBufferRange(buffer, offset, length);
        return _device.ReadInto(buffer, offset, length, timeoutMs);
    }

    public Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        ValidateBufferRange(buffer, offset, length);
        return _device.ReadIntoAsync(buffer, offset, length, timeoutMs, cancellationToken);
    }

    public long Write(byte[] data, int length)
    {
        ValidateWriteData(data, length);
        return _device.Write(data, length);
    }

    public long Write(byte[] data, int length, int timeoutMs)
    {
        ValidateWriteData(data, length);
        return _device.Write(data, length, timeoutMs);
    }

    public Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        ValidateWriteData(data, length);
        return _device.WriteAsync(data, length, timeoutMs, cancellationToken);
    }

    public Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return _device.ReadAsync(length, timeoutMs, cancellationToken);
    }

    public void Reset() => _device.Reset();

    public Task ResetAsync(CancellationToken cancellationToken = default) => _device.ResetAsync(cancellationToken);

    public void Dispose() => _device.Dispose();

    private static void ValidateBufferRange(byte[] buffer, int offset, int length)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || length < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }

    private static void ValidateWriteData(byte[] data, int length)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (length < 0 || length > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }
}