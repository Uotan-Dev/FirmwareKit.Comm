using System.Runtime.InteropServices;
using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Diagnostics;
using static FirmwareKit.Comm.Backend.HarmonyOS.HarmonyOSUsbDDK;

namespace FirmwareKit.Comm.Backend.HarmonyOS;

internal static class HarmonyOSUsbFinder
{
    // The USB DDK exposes no "list devices" API, so discovery probes device IDs 1..N.
    // Device IDs are normally assigned sequentially by the system, but gaps can appear;
    // callers that know their device ID can raise the limit to scan further, or lower it
    // to reduce probing time.
    /// <summary>
    /// Gets or sets the maximum device ID probed during discovery (default 256).
    /// <para>获取或设置发现期间探测的最大设备 ID（默认 256）。</para>
    /// </summary>
    public static int MaxDeviceId { get; set; } = 256;

    public static List<UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
    {
        var devices = new List<UsbDevice>();

        // DDK must be initialized before calling GetDeviceDescriptor/GetConfigDescriptor.
        // OH_Usb_Init uses internal reference counting, so the subsequent Init call in
        // CreateHandle is safe and simply increments the count.
        int ret = OH_Usb_Init();
        if (ret != USB_DDK_NO_ERROR)
        {
            UsbTrace.Log($"HarmonyOSUsbFinder: OH_Usb_Init failed: {GetErrorMessage(ret)}");
            return devices;
        }

        try
        {
            ulong maxProbe = (ulong)Math.Max(0, MaxDeviceId);
            for (ulong deviceId = 1; deviceId <= maxProbe; deviceId++)
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
            var interfaces = new List<UsbInterfaceInfo>();

            for (byte ifcIndex = 0; ifcIndex < numInterfaces; ifcIndex++)
            {
                IntPtr ifaceArrayPtr = configDescriptor.iface;
                if (ifaceArrayPtr == IntPtr.Zero) continue;

                int ifaceSize = Marshal.SizeOf<UsbDdkInterface>();
                IntPtr ifacePtr = new IntPtr(ifaceArrayPtr.ToInt64() + ifcIndex * ifaceSize);

                var iface = Marshal.PtrToStructure<UsbDdkInterface>(ifacePtr);

                if (iface.altsetting == IntPtr.Zero) continue;

                var ifaceDescStruct = Marshal.PtrToStructure<UsbDdkInterfaceDescriptor>(iface.altsetting);
                var ifaceDesc = ifaceDescStruct.interfaceDescriptor;

                byte ifcClass = ifaceDesc.bInterfaceClass;
                byte ifcSubClass = ifaceDesc.bInterfaceSubClass;
                byte ifcProtocol = ifaceDesc.bInterfaceProtocol;

                byte numEndpoints = ifaceDesc.bNumEndpoints;
                var endpoints = new List<UsbEndpointInfo>();
                byte epIn = 0, epOut = 0;

                IntPtr endpointArrayPtr = ifaceDescStruct.endpoint;
                if (endpointArrayPtr != IntPtr.Zero)
                {
                    int epSize = Marshal.SizeOf<UsbEndpointDescriptor>();
                    for (int epIdx = 0; epIdx < numEndpoints; epIdx++)
                    {
                        IntPtr epPtr = new IntPtr(endpointArrayPtr.ToInt64() + epIdx * epSize);
                        var epDesc = Marshal.PtrToStructure<UsbEndpointDescriptor>(epPtr);

                        byte epAddr = epDesc.bEndpointAddress;
                        byte epAttr = epDesc.bmAttributes;
                        endpoints.Add(new UsbEndpointInfo
                        {
                            EndpointAddress = epAddr,
                            Attributes = epAttr,
                            MaxPacketSize = epDesc.wMaxPacketSize,
                            Interval = epDesc.bInterval
                        });

                        if ((epAttr & 0x03) == 0x02)
                        {
                            if ((epAddr & 0x80) != 0) epIn = epAddr;
                            else epOut = epAddr;
                        }
                    }
                }

                interfaces.Add(new UsbInterfaceInfo
                {
                    InterfaceNumber = ifaceDesc.bInterfaceNumber,
                    Class = ifcClass,
                    SubClass = ifcSubClass,
                    Protocol = ifcProtocol,
                    Endpoints = endpoints
                });

                if (filter?.InterfaceClass is byte c && ifcClass != c) continue;
                if (filter?.InterfaceSubClass is byte s && ifcSubClass != s) continue;
                if (filter?.InterfaceProtocol is byte p && ifcProtocol != p) continue;
                if (filter?.InterfaceNumber is byte n && ifaceDesc.bInterfaceNumber != n) continue;

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
                    dev.Interfaces = interfaces;
                    dev.Speed = UsbSpeedInference.FromBcdUsb(devDesc.bcdUSB);

                    // Keep the device even when the handle cannot be opened (e.g. the
                    // interface is claimed by another session/process). Enumeration must
                    // reflect the current device state: the metadata was already populated
                    // by Initialize, and a busy device must not silently disappear from the
                    // list. Sessions over a non-open device are skipped later by
                    // UsbProviderProjection.ToSessions.
                    // <para>即使句柄无法打开（例如接口已被其他会话/进程声明）也保留该设备。
                    // 枚举必须反映当前设备状态：元数据已由 Initialize 填充，被占用的设备
                    // 不应静默地从列表中消失。基于未打开设备的会话稍后由
                    // UsbProviderProjection.ToSessions 跳过。</para>
                    if (dev.CreateHandle() != 0)
                    {
                        UsbTrace.Log($"HarmonyOSUsbFinder: device {deviceId} busy or unopenable - reported with metadata only.");
                    }

                    devices.Add(dev);

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
