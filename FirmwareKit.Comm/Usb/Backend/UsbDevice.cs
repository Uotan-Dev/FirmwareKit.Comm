namespace FirmwareKit.Comm.Usb.Backend;

internal abstract class UsbDevice : IDisposable
{
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

    public virtual int ReadInto(byte[] buffer, int offset, int length)
    {
        if (length <= 0) return 0;
        if (offset < 0 || length < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        byte[] data = Read(length);
        if (data.Length == 0) return 0;
        Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
        return data.Length;
    }

    public virtual int ReadInto(byte[] buffer, int offset, int length, int timeoutMs) => ReadInto(buffer, offset, length);

    public abstract long Write(byte[] data, int length);
    public virtual long Write(byte[] data, int length, int timeoutMs) => Write(data, length);
    public abstract int GetSerialNumber();
    public abstract int CreateHandle();
    public abstract void Reset();
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
