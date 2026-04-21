using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend;

namespace FirmwareKit.Comm.Usb.Core;

internal static class UsbDeviceInfoFactory
{
    public static UsbDeviceInfo FromBackendDevice(string apiName, UsbApiKind apiKind, UsbDevice device)
    {
        var info = new UsbDeviceInfo
        {
            ApiName = apiName,
            SourceApiKind = apiKind,
            SourceDeviceType = device.GetType().Name,
            DevicePath = device.DevicePath,
            SerialNumber = device.SerialNumber,
            VendorId = device.VendorId,
            ProductId = device.ProductId,
            InterfaceClass = device.InterfaceClass,
            InterfaceSubClass = device.InterfaceSubClass,
            InterfaceProtocol = device.InterfaceProtocol,
            InterfaceMetadataObserved = device.InterfaceMetadataObserved
        };

        info.DeviceKey = UsbDeviceIdentity.BuildKey(info);
        return info;
    }
}