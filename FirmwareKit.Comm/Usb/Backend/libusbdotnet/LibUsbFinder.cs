using FirmwareKit.Comm.Usb.Abstractions;
using LibUsbDotNet.LibUsb;

namespace FirmwareKit.Comm.Usb.Backend.libusbdotnet;

internal class LibUsbFinder
{
    private static bool TryGetBulkInterface(LibUsbDotNet.LibUsb.UsbDevice device, UsbDeviceFilter? filter, out byte interfaceId, out byte inEndpoint, out byte outEndpoint)
    {
        interfaceId = 0;
        inEndpoint = 0;
        outEndpoint = 0;

        try
        {
            foreach (var config in device.Configs)
            {
                foreach (var ifc in config.Interfaces)
                {
                    if (filter?.InterfaceClass is byte c && (byte)ifc.Class != c) continue;
                    if (filter?.InterfaceSubClass is byte s && (byte)ifc.SubClass != s) continue;
                    if (filter?.InterfaceProtocol is byte p && (byte)ifc.Protocol != p) continue;

                    bool hasIn = false;
                    bool hasOut = false;
                    byte candidateIn = 0;
                    byte candidateOut = 0;
                    foreach (var endpoint in ifc.Endpoints)
                    {
                        if ((endpoint.Attributes & 0x03) != 0x02) continue;

                        if ((endpoint.EndpointAddress & 0x80) != 0)
                        {
                            hasIn = true;
                            if (candidateIn == 0) candidateIn = endpoint.EndpointAddress;
                        }
                        else
                        {
                            hasOut = true;
                            if (candidateOut == 0) candidateOut = endpoint.EndpointAddress;
                        }
                    }

                    if (hasIn && hasOut)
                    {
                        interfaceId = (byte)ifc.Number;
                        inEndpoint = candidateIn;
                        outEndpoint = candidateOut;
                        return true;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static List<UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
    {
        List<UsbDevice> devices = new List<UsbDevice>();
        using (var context = new UsbContext())
        {
            var deviceList = context.List();

            foreach (var device in deviceList)
            {
                var libUsbDevice = device as LibUsbDotNet.LibUsb.UsbDevice;
                if (libUsbDevice == null) continue;
                if (filter?.VendorId is ushort filterVid && (ushort)device.VendorId != filterVid) continue;
                if (filter?.ProductId is ushort filterPid && (ushort)device.ProductId != filterPid) continue;

                if (!TryGetBulkInterface(libUsbDevice, filter, out byte interfaceId, out byte readEndpoint, out byte writeEndpoint)) continue;

                byte busNumber = libUsbDevice?.BusNumber ?? 0;
                byte address = libUsbDevice?.Address ?? 0;

                var usbDevice = new LibUsbDevice
                {
                    Vid = (ushort)device.VendorId,
                    Pid = (ushort)device.ProductId,
                    BusNumber = busNumber,
                    DeviceAddress = address,
                    InterfaceId = interfaceId,
                    ReadEndpointId = readEndpoint,
                    WriteEndpointId = writeEndpoint,
                    DevicePath = $"Bus {busNumber} Device {address}: {device.VendorId:X4}:{device.ProductId:X4}",
                    UsbDeviceType = UsbDeviceType.LibUSB
                };

                if (usbDevice.CreateHandle() == 0)
                {
                    devices.Add(usbDevice);
                }
                else
                {
                    usbDevice.Dispose();
                }
            }
        }
        return devices;
    }


}



