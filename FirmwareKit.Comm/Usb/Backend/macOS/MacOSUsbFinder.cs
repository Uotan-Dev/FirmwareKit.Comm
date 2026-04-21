using FirmwareKit.Comm.Usb.Abstractions;
using System.Text;
using static FirmwareKit.Comm.Usb.Backend.macOS.MacOSUsbAPI;

namespace FirmwareKit.Comm.Usb.Backend.macOS;

internal class MacOSUsbFinder
{
    public static List<UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
    {
        List<UsbDevice> devices = new List<UsbDevice>();
        IntPtr matchingDict = IOServiceMatching("IOUSBDevice");
        if (matchingDict == IntPtr.Zero) return devices;

        int kr = IOServiceGetMatchingServices(IntPtr.Zero, matchingDict, out IntPtr iterator);
        if (kr != 0) return devices;

        IntPtr service;
        while ((service = IOIteratorNext(iterator)) != IntPtr.Zero)
        {
            ulong registryEntryId = 0;
            _ = IORegistryEntryGetRegistryEntryID(service, out registryEntryId);

            StringBuilder sbPath = new StringBuilder(1024);
            IORegistryEntryGetPath(service, kIOServicePlane, sbPath);
            string devicePath = sbPath.ToString();

            

            IntPtr pluginInterface = IntPtr.Zero;
            int score = 0;
            var pluginTypeGuid = kIOUSBDeviceUserClientTypeID;
            var pluginInterfaceGuid = kIOCFPlugInInterfaceID;
            kr = IOCreatePlugInInterfaceForService(service, ref pluginTypeGuid, ref pluginInterfaceGuid, out pluginInterface, out score);
            if (kr != 0 || pluginInterface == IntPtr.Zero)
            {
                IOObjectRelease(service);
                continue;
            }

            try
            {
                IntPtr deviceInterface = IntPtr.Zero;
                if (!TryQueryInterface(pluginInterface, out deviceInterface, kIOUSBDeviceInterfaceID197, kIOUSBDeviceInterfaceID))
                {
                    continue;
                }

                try
                {
                    var getVendor = GetDelegate<USBGetDeviceVendorDelegate>(deviceInterface, Offset_USBGetDeviceVendor);
                    var getProduct = GetDelegate<USBGetDeviceProductDelegate>(deviceInterface, Offset_USBGetDeviceProduct);
                    var createIter = GetDelegate<USBDeviceCreateInterfaceIteratorDelegate>(deviceInterface, Offset_USBDeviceCreateInterfaceIterator);
                    ushort vid, pid;
                    getVendor(deviceInterface, out vid);
                    getProduct(deviceInterface, out pid);

                    if (filter?.VendorId is ushort filterVid && vid != filterVid)
                    {
                        continue;
                    }

                    if (filter?.ProductId is ushort filterPid && pid != filterPid)
                    {
                        continue;
                    }

                    IOUSBFindInterfaceRequest request = new IOUSBFindInterfaceRequest
                    {
                        bInterfaceClass = filter?.InterfaceClass ?? kIOUSBFindInterfaceDontCare,
                        bInterfaceSubClass = filter?.InterfaceSubClass ?? kIOUSBFindInterfaceDontCare,
                        bInterfaceProtocol = filter?.InterfaceProtocol ?? kIOUSBFindInterfaceDontCare,
                        bAlternateSetting = kIOUSBFindInterfaceDontCare
                    };

                    IntPtr interfaceIter;
                    if (createIter(deviceInterface, ref request, out interfaceIter) == 0 && interfaceIter != IntPtr.Zero)
                    {
                        IntPtr ifcService;
                        while ((ifcService = IOIteratorNext(interfaceIter)) != IntPtr.Zero)
                        {

                            IntPtr ifcPlugin = IntPtr.Zero;
                            var ifcPluginTypeGuid = kIOUSBInterfaceUserClientTypeID;
                            var ifcPluginInterfaceGuid = kIOCFPlugInInterfaceID;
                            if (IOCreatePlugInInterfaceForService(ifcService, ref ifcPluginTypeGuid, ref ifcPluginInterfaceGuid, out ifcPlugin, out score) == 0 && ifcPlugin != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr ifcIntf = IntPtr.Zero;
                                    if (TryQueryInterface(ifcPlugin, out ifcIntf, kIOUSBInterfaceInterfaceID197, kIOUSBInterfaceInterfaceID190, kIOUSBInterfaceInterfaceID))
                                    {
                                        try
                                        {
                                            var getNumEpts = GetDelegate<GetNumEndpointsDelegate>(ifcIntf, Offset_GetNumEndpoints);
                                            var getPipeProps = GetDelegate<GetPipePropertiesDelegate>(ifcIntf, Offset_GetPipeProperties);

                                            byte numEpts;
                                            getNumEpts(ifcIntf, out numEpts);
                                            byte bulkIn = 0, bulkOut = 0;

                                            for (byte i = 1; i <= numEpts; i++)
                                            {
                                                byte direction, number, transferType, interval;
                                                ushort maxPacketSize;
                                                if (getPipeProps(ifcIntf, i, out direction, out number, out transferType, out maxPacketSize, out interval) == 0)
                                                {
                                                    if (transferType == 0x02)
                                                    {
                                                        if (direction == 1) bulkIn = i;
                                                        else bulkOut = i;
                                                    }
                                                }
                                            }

                                            if (bulkIn != 0 && bulkOut != 0)
                                            {
                                                var dev = new MacOSUsbDevice
                                                {
                                                    RegistryEntryId = registryEntryId,
                                                    DevicePath = devicePath,
                                                    VendorId = vid,
                                                    ProductId = pid,
                                                    InterfaceClass = filter?.InterfaceClass,
                                                    InterfaceSubClass = filter?.InterfaceSubClass,
                                                    InterfaceProtocol = filter?.InterfaceProtocol,
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
                                        finally { GetDelegate<ReleaseDelegate>(ifcIntf, Offset_IUnknown_Release)(ifcIntf); }
                                    }
                                }
                                finally { GetDelegate<ReleaseDelegate>(ifcPlugin, Offset_Plugin_Release)(ifcPlugin); }
                            }
                            IOObjectRelease(ifcService);
                        }
                        IOObjectRelease(interfaceIter);
                    }
                }
                finally
                {
                    var devRelease = GetDelegate<ReleaseDelegate>(deviceInterface, Offset_IUnknown_Release);
                    devRelease(deviceInterface);
                }
            }
            finally
            {
                var pluginRelease = GetDelegate<ReleaseDelegate>(pluginInterface, Offset_Plugin_Release);
                pluginRelease(pluginInterface);
                IOObjectRelease(service);
            }
        }

        IOObjectRelease(iterator);
        return devices;
    }


}



