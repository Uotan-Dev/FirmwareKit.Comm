using FirmwareKit.Comm.Usb.Abstractions;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Usb.Backend.macOS.MacHostUsbAPI;

namespace FirmwareKit.Comm.Usb.Backend.macOS;

/// <summary>
/// Enumerates USB devices through the IOUSBHost (IOUSBLib) user-space API (macOS 10.15+).
/// Replaces the legacy IOKit IOServiceMatching-based finder.
/// </summary>
internal static class MacHostUsbFinder
{
    public static List<UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
    {
        List<UsbDevice> devices = new List<UsbDevice>();

        IntPtr cfDevices = IntPtr.Zero;
        // NULL matching dictionary requests all USB devices.
        int kr = IOUSBLibCopyDevices(IntPtr.Zero, out cfDevices);
        if (kr != kIOReturnSuccess || cfDevices == IntPtr.Zero) return devices;

        try
        {
            long count = CFArrayGetCount(cfDevices);
            for (long i = 0; i < count; i++)
            {
                IntPtr device = CFArrayGetValueAtIndex(cfDevices, i);
                if (device == IntPtr.Zero) continue;

                ushort vid = 0, pid = 0;
                ulong registryEntryId = 0;
                _ = IOUSBHostDeviceGetVendorID(device, out vid);
                _ = IOUSBHostDeviceGetProductID(device, out pid);
                _ = IOUSBHostDeviceGetRegistryEntryID(device, out registryEntryId);

                if (filter?.VendorId is ushort filterVid && vid != filterVid) continue;
                if (filter?.ProductId is ushort filterPid && pid != filterPid) continue;

                if (!TryGetBulkEndpoints(device, filter?.InterfaceNumber, out byte bulkIn, out byte bulkOut, out byte ifcClass, out byte ifcSubClass, out byte ifcProtocol, out IReadOnlyList<UsbInterfaceInfo> interfaces)) continue;

                var dev = new MacHostUsbDevice
                {
                    RegistryEntryId = registryEntryId,
                    DevicePath = $"IOUSBLib:{registryEntryId}",
                    VendorId = vid,
                    ProductId = pid,
                    InterfaceClass = ifcClass,
                    InterfaceSubClass = ifcSubClass,
                    InterfaceProtocol = ifcProtocol,
                    InterfaceMetadataObserved = true,
                    Interfaces = interfaces,
                    bulkIn = bulkIn,
                    bulkOut = bulkOut,
                    UsbDeviceType = UsbDeviceType.MacOS
                };

                if (dev.CreateHandle() == 0)
                {
                    devices.Add(dev);
                }
                else
                {
                    dev.Dispose();
                }
            }
        }
        finally
        {
            CFRelease(cfDevices);
        }

        return devices;
    }

    /// <summary>
    /// Walks the device's configuration descriptor, collects every interface and endpoint,
    /// and finds the first interface that exposes both a bulk-IN and a bulk-OUT endpoint
    /// (matching the legacy backend behavior). Returns the endpoint numbers (pipe IDs) to use
    /// with IOUSBHostInterfaceCopyPipe, the REAL class/subclass/protocol of the matched
    /// interface, and the full interface list for <see cref="UsbDeviceInfo"/>.
    /// </summary>
    private static bool TryGetBulkEndpoints(
        IntPtr device,
        byte? interfaceNumber,
        out byte bulkIn,
        out byte bulkOut,
        out byte interfaceClass,
        out byte interfaceSubClass,
        out byte interfaceProtocol,
        out IReadOnlyList<UsbInterfaceInfo> interfaces)
    {
        bulkIn = 0;
        bulkOut = 0;
        interfaceClass = 0;
        interfaceSubClass = 0;
        interfaceProtocol = 0;
        interfaces = Array.Empty<UsbInterfaceInfo>();

        IntPtr configPtr = IntPtr.Zero;
        if (IOUSBHostDeviceCopyConfigurationDescriptor(device, out configPtr) != kIOReturnSuccess || configPtr == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var config = Marshal.PtrToStructure<UsbConfigurationDescriptor>(configPtr);
            int totalLength = config.wTotalLength;
            if (totalLength < 9) return false;

            int offset = config.bLength; // skip configuration descriptor header
            byte curIn = 0, curOut = 0;
            byte curClass = 0, curSubClass = 0, curProtocol = 0;
            var collected = new List<UsbInterfaceInfo>();
            List<UsbEndpointInfo>? currentEndpoints = null;
            bool found = false;

            while (offset + 2 <= totalLength)
            {
                byte len = Marshal.ReadByte(configPtr, offset);
                byte type = Marshal.ReadByte(configPtr, offset + 1);
                if (len == 0) break;

                if (type == USB_DESCRIPTOR_TYPE_INTERFACE && offset + 9 <= totalLength)
                {
                    // New interface: reset per-interface bulk endpoint candidates and
                    // record the interface's real descriptor metadata (not the filter).
                    var ifcDesc = Marshal.PtrToStructure<UsbInterfaceDescriptor>(new IntPtr(configPtr.ToInt64() + offset));
                    curIn = 0;
                    curOut = 0;
                    curClass = ifcDesc.bInterfaceClass;
                    curSubClass = ifcDesc.bInterfaceSubClass;
                    curProtocol = ifcDesc.bInterfaceProtocol;
                    if (interfaceNumber.HasValue && ifcDesc.bInterfaceNumber != interfaceNumber.Value)
                    {
                        // Not the requested interface: still walk it, but do not collect
                        // its endpoints or let its bulk endpoints drive the match.
                        currentEndpoints = null;
                        continue;
                    }
                    currentEndpoints = new List<UsbEndpointInfo>();
                    collected.Add(new UsbInterfaceInfo
                    {
                        InterfaceNumber = ifcDesc.bInterfaceNumber,
                        Class = ifcDesc.bInterfaceClass,
                        SubClass = ifcDesc.bInterfaceSubClass,
                        Protocol = ifcDesc.bInterfaceProtocol,
                        Endpoints = currentEndpoints
                    });
                }
                else if (type == USB_DESCRIPTOR_TYPE_ENDPOINT && offset + 7 <= totalLength)
                {
                    var ep = Marshal.PtrToStructure<UsbEndpointDescriptor>(new IntPtr(configPtr.ToInt64() + offset));
                    currentEndpoints?.Add(new UsbEndpointInfo
                    {
                        EndpointAddress = ep.bEndpointAddress,
                        Attributes = ep.bmAttributes,
                        MaxPacketSize = ep.wMaxPacketSize,
                        Interval = ep.bInterval
                    });
                    if (currentEndpoints != null && (ep.bmAttributes & 0x03) == 0x02) // bulk transfer type
                    {
                        bool isIn = (ep.bEndpointAddress & 0x80) != 0;
                        byte pipeId = (byte)(ep.bEndpointAddress & 0x0F);
                        if (isIn && curIn == 0) curIn = pipeId;
                        else if (!isIn && curOut == 0) curOut = pipeId;
                    }
                }

                offset += len;

                if (!found && curIn != 0 && curOut != 0)
                {
                    bulkIn = curIn;
                    bulkOut = curOut;
                    interfaceClass = curClass;
                    interfaceSubClass = curSubClass;
                    interfaceProtocol = curProtocol;
                    found = true;
                }
            }

            interfaces = collected;
            return found;
        }
        finally
        {
            // IOUSBLib descriptor memory is owned by the caller; free() it.
            MacHostUsbAPI.Free(configPtr);
        }
    }
}
