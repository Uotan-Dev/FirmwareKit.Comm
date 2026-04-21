namespace FirmwareKit.Comm.Usb.Backend;

internal abstract class UsbDevice : IDisposable
{
    public virtual int DefaultTimeoutMs => UsbTransferPolicies.DefaultTimeoutMs;

    public string DevicePath { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public ushort VendorId { get; set; }
    public ushort ProductId { get; set; }
    public byte? InterfaceClass { get; set; }
    public byte? InterfaceSubClass { get; set; }
    public byte? InterfaceProtocol { get; set; }
    public UsbDeviceType UsbDeviceType { get; set; }

    public abstract byte[] Read(int length);

    public virtual byte[] Read(int length, int timeoutMs) => Read(length);

    public virtual int ControlTransfer(FirmwareKit.Comm.Usb.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        throw new NotSupportedException($"{GetType().Name} does not support control transfers.");
    }

    internal static void ValidateBufferRange(byte[] buffer, int offset, int length)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length < 0 || length > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }

    internal static void ValidateWriteData(byte[] data, int length)
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

    public virtual int ReadInto(byte[] buffer, int offset, int length)
    {
        if (length <= 0) return 0;
        ValidateBufferRange(buffer, offset, length);

        byte[] data = Read(length);
        if (data.Length == 0) return 0;
        Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
        return data.Length;
    }

    public virtual int ReadInto(byte[] buffer, int offset, int length, int timeoutMs) => ReadInto(buffer, offset, length);

    public virtual Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Read(length, timeoutMs);
        }, cancellationToken);
    }

    public virtual Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadInto(buffer, offset, length, timeoutMs);
        }, cancellationToken);
    }

    public abstract long Write(byte[] data, int length);
    public virtual long Write(byte[] data, int length, int timeoutMs) => Write(data, length);

    public virtual Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Write(data, length, timeoutMs);
        }, cancellationToken);
    }

    public virtual Task<int> ControlTransferAsync(FirmwareKit.Comm.Usb.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ControlTransfer(setupPacket, buffer, offset, length, timeoutMs);
        }, cancellationToken);
    }

    public abstract int GetSerialNumber();
    public abstract int CreateHandle();
    public abstract void Reset();
    public virtual Task ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reset();
        }, cancellationToken);
    }

    public abstract void Dispose();
}

internal enum UsbDeviceType
{
    WinLegacy = 0,
    WinUSB = 1,
    Linux = 2,
    LibUSB = 3,
    MacOS = 4
}
