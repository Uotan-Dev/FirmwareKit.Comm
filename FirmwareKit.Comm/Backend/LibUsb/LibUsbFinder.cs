using System.Runtime.InteropServices;
using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Diagnostics;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;

namespace FirmwareKit.Comm.Backend.LibUsb;

internal static class LibUsbFinder
{
    // LibUsbDotNet 3.0.224 no longer bundles the native libusb runtime. Creating a
    // UsbContext when the native library is absent leaves a half-initialized context
    // whose finalizer NullReferenceExceptions (known upstream issue). Probe for the
    // runtime first so we never create a context without the native library present.
    [DllImport("libusb-1.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern int libusb_init(out IntPtr context);

    [DllImport("libusb-1.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_exit(IntPtr context);

    private static bool IsNativeRuntimePresent()
    {
        try
        {
            // Resolves the entry points without invoking them; throws when the
            // native library (or the entry point) is absent.
            Marshal.PrelinkAll(typeof(LibUsbFinder));
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetBulkInterface(
        LibUsbDotNet.LibUsb.UsbDevice device,
        UsbDeviceFilter? filter,
        out byte interfaceId,
        out byte inEndpoint,
        out byte outEndpoint,
        out byte interfaceClass,
        out byte interfaceSubClass,
        out byte interfaceProtocol,
        out IReadOnlyList<UsbInterfaceInfo> interfaces)
    {
        interfaceId = 0;
        inEndpoint = 0;
        outEndpoint = 0;
        interfaceClass = 0;
        interfaceSubClass = 0;
        interfaceProtocol = 0;
        interfaces = Array.Empty<UsbInterfaceInfo>();

        try
        {
            var collected = new List<UsbInterfaceInfo>();
            foreach (var config in device.Configs)
            {
                foreach (var ifc in config.Interfaces)
                {
                    var endpoints = new List<UsbEndpointInfo>();
                    foreach (var endpoint in ifc.Endpoints)
                    {
                        endpoints.Add(new UsbEndpointInfo
                        {
                            EndpointAddress = (byte)endpoint.EndpointAddress,
                            Attributes = (byte)endpoint.Attributes,
                            MaxPacketSize = (ushort)endpoint.MaxPacketSize,
                            Interval = (byte)endpoint.Interval
                        });
                    }
                    collected.Add(new UsbInterfaceInfo
                    {
                        InterfaceNumber = (byte)ifc.Number,
                        Class = (byte)ifc.Class,
                        SubClass = (byte)ifc.SubClass,
                        Protocol = (byte)ifc.Protocol,
                        Endpoints = endpoints
                    });

                    if (filter?.InterfaceClass is byte c && (byte)ifc.Class != c) continue;
                    if (filter?.InterfaceSubClass is byte s && (byte)ifc.SubClass != s) continue;
                    if (filter?.InterfaceProtocol is byte p && (byte)ifc.Protocol != p) continue;
                    if (filter?.InterfaceNumber is byte n && (byte)ifc.Number != n) continue;

                    bool hasIn = false;
                    bool hasOut = false;
                    byte candidateIn = 0;
                    byte candidateOut = 0;
                    foreach (var endpoint in ifc.Endpoints)
                    {
                        // Collect bulk endpoints first (the session I/O path), then interrupt
                        // endpoints as fallback candidates so devices with only interrupt pipes
                        // (e.g. HID) can still be opened when the filter requests explicit
                        // endpoint addresses (interrupt-test).
                        int epType = endpoint.Attributes & 0x03;
                        if (epType != 0x02 && epType != 0x03) continue;

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
                        // Honor an explicit endpoint requirement: the interface must contain
                        // the requested addresses (e.g. Rockchip loader on 0x82/0x02), and the
                        // requested endpoints win over the first bulk pair when both exist.
                        bool inOk = filter?.EndpointAddressIn is not byte reqIn ||
                            ifc.Endpoints.Any(e => (e.EndpointAddress & 0x80) != 0 && e.EndpointAddress == reqIn);
                        bool outOk = filter?.EndpointAddressOut is not byte reqOut ||
                            ifc.Endpoints.Any(e => (e.EndpointAddress & 0x80) == 0 && e.EndpointAddress == reqOut);
                        if (inOk && outOk)
                        {
                            interfaceId = (byte)ifc.Number;
                            inEndpoint = filter?.EndpointAddressIn ?? candidateIn;
                            outEndpoint = filter?.EndpointAddressOut ?? candidateOut;
                            interfaceClass = (byte)ifc.Class;
                            interfaceSubClass = (byte)ifc.SubClass;
                            interfaceProtocol = (byte)ifc.Protocol;
                            interfaces = collected;
                            return true;
                        }
                    }
                    else if (hasIn && filter?.EndpointAddressIn is byte && filter?.EndpointAddressOut == null)
                    {
                        // IN-only match: interrupt-test on HID devices that expose no OUT pipe.
                        interfaceId = (byte)ifc.Number;
                        inEndpoint = filter.EndpointAddressIn.Value;
                        outEndpoint = 0;
                        interfaceClass = (byte)ifc.Class;
                        interfaceSubClass = (byte)ifc.SubClass;
                        interfaceProtocol = (byte)ifc.Protocol;
                        interfaces = collected;
                        return true;
                    }
                }
            }
        }
        catch
        {
            UsbTrace.Log("LibUsbFinder: failed to inspect interface descriptors.");
            return false;
        }

        return false;
    }

    public static List<global::FirmwareKit.Comm.Backend.UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
    {
        List<global::FirmwareKit.Comm.Backend.UsbDevice> devices = new List<global::FirmwareKit.Comm.Backend.UsbDevice>();
        if (!IsNativeRuntimePresent())
        {
            UsbTrace.Log("LibUsb backend unavailable: native libusb runtime not found.");
            return devices;
        }

        using (var context = new UsbContext())
        {
            var deviceList = context.List();

            foreach (var device in deviceList)
            {
                var libUsbDevice = device as LibUsbDotNet.LibUsb.UsbDevice;
                if (libUsbDevice == null) continue;
                if (filter?.VendorId is ushort filterVid && (ushort)device.VendorId != filterVid) continue;
                if (filter?.ProductId is ushort filterPid && (ushort)device.ProductId != filterPid) continue;

                if (!TryGetBulkInterface(
                    libUsbDevice,
                    filter,
                    out byte interfaceId,
                    out byte readEndpoint,
                    out byte writeEndpoint,
                    out byte interfaceClass,
                    out byte interfaceSubClass,
                    out byte interfaceProtocol,
                    out IReadOnlyList<UsbInterfaceInfo> interfaces)) continue;

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
                    InterfaceClass = interfaceClass,
                    InterfaceSubClass = interfaceSubClass,
                    InterfaceProtocol = interfaceProtocol,
                    InterfaceMetadataObserved = true,
                    Interfaces = interfaces,
                    Speed = MapSpeed(libUsbDevice?.Speed ?? Speed.Unknown),
                    DevicePath = $"Bus {busNumber} Device {address}: {device.VendorId:X4}:{device.ProductId:X4}",
                    UsbDeviceType = global::FirmwareKit.Comm.Backend.UsbDeviceType.LibUSB
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

    /// <summary>
    /// Maps LibUsbDotNet's <see cref="Speed"/> to the library's <see cref="UsbDeviceSpeed"/>.
    /// <para>将 LibUsbDotNet 的 <see cref="Speed"/> 映射到本库的 <see cref="UsbDeviceSpeed"/>。</para>
    /// LibUsbDotNet does not distinguish Super vs SuperPlus, so both report as Super.
    /// <para>LibUsbDotNet 不区分 Super 与 SuperPlus，两者都报告为 Super。</para>
    /// </summary>
    private static UsbDeviceSpeed MapSpeed(Speed speed)
    {
        return speed switch
        {
            Speed.Low => UsbDeviceSpeed.Low,
            Speed.Full => UsbDeviceSpeed.Full,
            Speed.High => UsbDeviceSpeed.High,
            Speed.Super => UsbDeviceSpeed.Super,
            _ => UsbDeviceSpeed.Unknown
        };
    }

    public static bool IsRuntimeAvailable(out string? reason)
    {
        reason = null;
        if (!IsNativeRuntimePresent())
        {
            reason = "native libusb runtime not found";
            UsbTrace.Log($"LibUsb runtime probe failed: {reason}");
            return false;
        }

        // Verify with the raw native API instead of constructing a LibUsbDotNet UsbContext:
        // when libusb_init fails, LibUsbDotNet leaves a half-initialized context whose
        // finalizer throws NullReferenceException (known upstream issue) - on CI this fails
        // the whole test run even when every test passes. libusb_init/libusb_exit probe the
        // runtime without creating any LibUsbDotNet object.
        if (!ProbeNativeInit())
        {
            reason = "libusb_init failed";
            UsbTrace.Log($"LibUsb runtime probe failed: {reason}");
            return false;
        }

        return true;
    }

    private static bool ProbeNativeInit()
    {
        try
        {
            if (libusb_init(out IntPtr context) == 0 && context != IntPtr.Zero)
            {
                libusb_exit(context);
                return true;
            }

            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }


}



