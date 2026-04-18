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

    public int ReadInto(byte[] buffer, int offset, int length) => _device.ReadInto(buffer, offset, length);

    public long Write(byte[] data, int length) => _device.Write(data, length);

    public void Reset() => _device.Reset();

    public void Dispose() => _device.Dispose();
}
