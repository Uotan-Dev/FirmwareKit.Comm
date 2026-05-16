using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Usb.Backend.HarmonyOS.HarmonyOSUsbDDK;

namespace FirmwareKit.Comm.Usb.Backend.HarmonyOS;

internal static class HarmonyOSUsbFinder
{
    public static List<UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
    {
        var devices = new List<UsbDevice>();

        int ret = OH_Usb_Init();
        if (ret != USB_DDK_NO_ERROR)
        {
            UsbTrace.Log($"HarmonyOSUsbFinder: OH_Usb_Init failed: {GetErrorMessage(ret)}");
            return devices;
        }

        try
        {
            for (ulong deviceId = 1; deviceId <= 128; deviceId++)
            {
                ProbeDevice(deviceId, filter, devices);
            }
        }
        finally
        {
            OH_Usb_Release();
        }

        return devices;
    }

    private static void ProbeDevice(ulong deviceId, UsbDeviceFilter? filter, List<UsbDevice> devices)
    {
        var devDesc = new UsbDeviceDescriptor();
        int ret = OH_Usb_GetDeviceDescriptor(deviceId, ref devDesc);
        if (ret != USB_DDK_NO_ERROR) return;

        ushort vendorId = devDesc.idVendor;
        ushort productId = devDesc.idProduct;

        if (filter?.VendorId is ushort filterVid && vendorId != filterVid) return;
        if (filter?.ProductId is ushort filterPid && productId != filterPid) return;

        IntPtr configPtr = IntPtr.Zero;
        ret = OH_Usb_GetConfigDescriptor(deviceId, 0, ref configPtr);
        if (ret != USB_DDK_NO_ERROR || configPtr == IntPtr.Zero) return;

        try
        {
            var configDescriptor = Marshal.PtrToStructure<UsbDdkConfigDescriptor>(configPtr);
            byte numInterfaces = configDescriptor.numIface;

            for (byte ifcIndex = 0; ifcIndex < numInterfaces; ifcIndex++)
            {
                IntPtr ifaceArrayPtr = configDescriptor.iface;
                if (ifaceArrayPtr == IntPtr.Zero) continue;

                int ifaceSize = Marshal.SizeOf<UsbDdkInterface>();
                IntPtr ifacePtr = new IntPtr(ifaceArrayPtr.ToInt64() + ifcIndex * ifaceSize);

                var iface = Marshal.PtrToStructure<UsbDdkInterface>(ifacePtr);
                var ifaceDesc = iface.altsetting;

                byte ifcClass = ifaceDesc.bInterfaceClass;
                byte ifcSubClass = ifaceDesc.bInterfaceSubClass;
                byte ifcProtocol = ifaceDesc.bInterfaceProtocol;

                if (filter?.InterfaceClass is byte c && ifcClass != c) continue;
                if (filter?.InterfaceSubClass is byte s && ifcSubClass != s) continue;
                if (filter?.InterfaceProtocol is byte p && ifcProtocol != p) continue;

                byte numEndpoints = ifaceDesc.bNumEndpoints;
                byte epIn = 0, epOut = 0;

                IntPtr endpointArrayPtr = iface.extra;
                if (endpointArrayPtr != IntPtr.Zero)
                {
                    int epSize = Marshal.SizeOf<UsbEndpointDescriptor>();
                    for (int epIdx = 0; epIdx < numEndpoints; epIdx++)
                    {
                        IntPtr epPtr = new IntPtr(endpointArrayPtr.ToInt64() + epIdx * epSize);
                        var epDesc = Marshal.PtrToStructure<UsbEndpointDescriptor>(epPtr);

                        byte epAddr = epDesc.bEndpointAddress;
                        byte epAttr = epDesc.bmAttributes;

                        if ((epAttr & 0x03) == 0x02)
                        {
                            if ((epAddr & 0x80) != 0) epIn = epAddr;
                            else epOut = epAddr;
                        }
                    }
                }

                if (epIn != 0 && epOut != 0)
                {
                    var dev = new HarmonyOSUsbDevice();
                    dev.Initialize(
                        deviceId,
                        ifcIndex,
                        epIn,
                        epOut,
                        vendorId,
                        productId,
                        ifcClass,
                        ifcSubClass,
                        ifcProtocol,
                        devDesc.iSerialNumber == 0 ? null : "UNKNOWN"
                    );

                    if (dev.CreateHandle() == 0)
                    {
                        devices.Add(dev);
                    }
                    else
                    {
                        UsbTrace.Log($"HarmonyOSUsbFinder: CreateHandle failed for device {deviceId}");
                        dev.Dispose();
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            UsbTrace.Log($"HarmonyOSUsbFinder: ProbeDevice failed for device {deviceId}: {ex.Message}");
        }
        finally
        {
            OH_Usb_FreeConfigDescriptor(configPtr);
        }
    }
}
