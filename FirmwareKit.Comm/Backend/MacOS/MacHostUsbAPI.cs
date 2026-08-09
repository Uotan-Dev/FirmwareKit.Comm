using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Backend.MacOS;

/// <summary>
/// P/Invoke surface for the IOUSBHost.framework user-space C API, available on macOS 10.15+.
/// Replaces the legacy IOKit COM-vtable backend (IOUSBDeviceInterface197 / IOUSBInterfaceInterface197).
/// Signatures follow IOUSBLib.h from the macOS SDK. Validate against the SDK headers when building on macOS.
/// <para>IOUSBHost.framework 用户态 C API 的 P/Invoke 表面（macOS 10.15+）。
/// 取代旧式 IOKit COM-vtable 后端（IOUSBDeviceInterface197 / IOUSBInterfaceInterface197）。
/// 签名遵循 macOS SDK 的 IOUSBLib.h。</para>
/// </summary>
internal static class MacHostUsbAPI
{
    public const string IOUSBHost = "/System/Library/Frameworks/IOUSBHost.framework/IOUSBHost";
    public const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    public const string Libc = "libc";

    // ---- Object discovery ----
    // kern_return_t IOUSBLibCopyDevices(CFMutableDictionaryRef matching, CFArrayRef *devices);
    // A NULL matching dictionary requests all devices.
    [DllImport(IOUSBHost)]
    public static extern int IOUSBLibCopyDevices(IntPtr matching, out IntPtr devices);

    // ---- Device properties ----
    // kern_return_t IOUSBHostDeviceGetID(IOUSBHostDevice device, uint64_t *id);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostDeviceGetID(IntPtr device, out ulong id);

    // kern_return_t IOUSBHostDeviceGetRegistryEntryID(IOUSBHostDevice device, uint64_t *registryEntryID);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostDeviceGetRegistryEntryID(IntPtr device, out ulong registryEntryID);

    // kern_return_t IOUSBHostDeviceGetVendorID(IOUSBHostDevice device, uint16_t *vendorID);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostDeviceGetVendorID(IntPtr device, out ushort vendorID);

    // kern_return_t IOUSBHostDeviceGetProductID(IOUSBHostDevice device, uint16_t *productID);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostDeviceGetProductID(IntPtr device, out ushort productID);

    // ---- Descriptors ----
    // kern_return_t IOUSBHostDeviceCopyConfigurationDescriptor(IOUSBHostDevice device, IOUSBConfigurationDescriptor **descriptor);
    // Caller owns the returned descriptor memory and must free() it.
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostDeviceCopyConfigurationDescriptor(IntPtr device, out IntPtr descriptor);

    // ---- Open / close ----
    // kern_return_t IOUSBHostDeviceOpen(IOUSBHostDevice device, uint32_t options, uint32_t *deviceParameter);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostDeviceOpen(IntPtr device, uint options, out uint deviceParameter);

    // kern_return_t IOUSBHostDeviceClose(IOUSBHostDevice device);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostDeviceClose(IntPtr device);

    // ---- Control transfers ----
    // kern_return_t IOUSBHostDeviceDeviceRequest(IOUSBHostDevice device, IOUSBDeviceRequest *request, uint32_t completionTimeout);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostDeviceDeviceRequest(IntPtr device, ref IOUSBDeviceRequest request, uint completionTimeout);

    // ---- Interface access ----
    // kern_return_t IOUSBHostDeviceCreateInterfaceIterator(IOUSBHostDevice device, IOUSBFindInterfaceRequest *request, IOUSBHostInterfaceIterator *iterator);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostDeviceCreateInterfaceIterator(IntPtr device, ref IOUSBFindInterfaceRequest request, out IntPtr iterator);

    // IOUSBHostInterface IOUSBHostInterfaceIteratorNext(IOUSBHostInterfaceIterator iterator);
    // Returns a +1 (owned) reference, mirroring IOKit IOIteratorNext semantics.
    [DllImport(IOUSBHost)]
    public static extern IntPtr IOUSBHostInterfaceIteratorNext(IntPtr iterator);

    // kern_return_t IOUSBHostInterfaceOpen(IOUSBHostInterface interface, uint32_t options);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostInterfaceOpen(IntPtr interfacePtr, uint options);

    // kern_return_t IOUSBHostInterfaceClose(IOUSBHostInterface interface);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostInterfaceClose(IntPtr interfacePtr);

    // kern_return_t IOUSBHostInterfaceCopyPipe(IOUSBHostInterface interface, uint8_t portType, uint8_t pipeID, IOUSBHostPipe *pipe);
    // "Copy" semantics: caller owns the returned pipe reference.
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostInterfaceCopyPipe(IntPtr interfacePtr, byte portType, byte pipeID, out IntPtr pipe);

    // ---- Bulk transfers ----
    // kern_return_t IOUSBHostPipeBulkTransfer(IOUSBHostPipe pipe, uint8_t *data, uint32_t dataLength, uint32_t *bytesTransferred, uint32_t completionTimeout);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostPipeBulkTransfer(IntPtr pipe, IntPtr data, uint dataLength, out uint bytesTransferred, uint completionTimeout);

    // kern_return_t IOUSBHostPipeWriteBulkData(IOUSBHostPipe pipe, uint8_t *data, uint32_t dataLength, uint32_t *bytesTransferred, uint32_t completionTimeout);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostPipeWriteBulkData(IntPtr pipe, IntPtr data, uint dataLength, out uint bytesTransferred, uint completionTimeout);

    // ---- Pipe control ----
    // kern_return_t IOUSBHostPipeAbort(IOUSBHostPipe pipe);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostPipeAbort(IntPtr pipe);

    // kern_return_t IOUSBHostPipeClearStall(IOUSBHostPipe pipe);
    [DllImport(IOUSBHost)]
    public static extern int IOUSBHostPipeClearStall(IntPtr pipe);

    // ---- CoreFoundation helpers ----
    [DllImport(CoreFoundation)]
    public static extern void CFRelease(IntPtr obj);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFRetain(IntPtr obj);

    // CFIndex is a long on macOS (64-bit only for .NET).
    [DllImport(CoreFoundation)]
    public static extern long CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, long index);

    // IOUSBLib descriptor memory is allocated with malloc() and must be released
    // with free() - Marshal.FreeHGlobal (GlobalFree) does not match on macOS.
    [DllImport(Libc, EntryPoint = "free")]
    public static extern void Free(IntPtr ptr);

    // ---- Structs (IOUSBLib.h) ----
    [StructLayout(LayoutKind.Sequential)]
    public struct IOUSBDeviceRequest
    {
        public byte bmRequestType;
        public byte bRequest;
        public ushort wValue;
        public ushort wIndex;
        public ushort wLength;
        public IntPtr pData;
        public uint wLenDone;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IOUSBFindInterfaceRequest
    {
        public ushort bInterfaceClass;
        public ushort bInterfaceSubClass;
        public ushort bInterfaceProtocol;
        public ushort bAlternateSetting;
    }

    // USB descriptor headers (USB 2.0 spec), used to walk the configuration descriptor.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UsbConfigurationDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public ushort wTotalLength;
        public byte bNumInterfaces;
        public byte bConfigurationValue;
        public byte iConfiguration;
        public byte bmAttributes;
        public byte MaxPower;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UsbInterfaceDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public byte bInterfaceNumber;
        public byte bAlternateSetting;
        public byte bNumEndpoints;
        public byte bInterfaceClass;
        public byte bInterfaceSubClass;
        public byte bInterfaceProtocol;
        public byte iInterface;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UsbEndpointDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public byte bEndpointAddress;
        public byte bmAttributes;
        public ushort wMaxPacketSize;
        public byte bInterval;
    }

    // Descriptor types
    public const byte USB_DESCRIPTOR_TYPE_CONFIGURATION = 2;
    public const byte USB_DESCRIPTOR_TYPE_INTERFACE = 4;
    public const byte USB_DESCRIPTOR_TYPE_ENDPOINT = 5;

    // Port types (IOUSBLib)
    public const byte kIOUSBHostPortTypeControl = 0;
    public const byte kIOUSBHostPortTypeIsochronous = 1;
    public const byte kIOUSBHostPortTypeBulk = 2;
    public const byte kIOUSBHostPortTypeInterrupt = 3;

    // Pipe directions (IOUSBLib)
    public const byte kIOUSBHostPipeDirectionOut = 0;
    public const byte kIOUSBHostPipeDirectionIn = 1;

    public const ushort kIOUSBFindInterfaceDontCare = 0xFF;

    // IOReturn error codes
    public const int kIOReturnSuccess = 0;
    public const int kIOReturnNoDevice = unchecked((int)0xE00002C0);
    public const int kIOReturnAborted = unchecked((int)0xE00002EB);
    public const int kIOReturnTimeout = unchecked((int)0xE00002D6);
    public const int kIOReturnNotResponding = unchecked((int)0xE00002ED);
}
