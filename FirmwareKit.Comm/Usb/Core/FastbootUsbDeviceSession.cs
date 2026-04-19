using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;

namespace FirmwareKit.Comm.Usb.Core;

internal sealed class FastbootUsbDeviceSession : IUsbDeviceSession
{
    private readonly UsbDevice _device;

    public FastbootUsbDeviceSession(string apiName, UsbApiKind kind, UsbDevice device)
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
            ProductId = device.ProductId
        };
    }

    public UsbDeviceInfo DeviceInfo { get; }

    public byte[] Read(int length) => _device.Read(length);

    public byte[] Read(int length, int timeoutMs) => _device.Read(length, timeoutMs);

    public int ReadInto(byte[] buffer, int offset, int length) => _device.ReadInto(buffer, offset, length);

    public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs) => _device.ReadInto(buffer, offset, length, timeoutMs);

    public long Write(byte[] data, int length) => _device.Write(data, length);

    public long Write(byte[] data, int length, int timeoutMs) => _device.Write(data, length, timeoutMs);

    public void Reset() => _device.Reset();

    public void Dispose() => _device.Dispose();
}
