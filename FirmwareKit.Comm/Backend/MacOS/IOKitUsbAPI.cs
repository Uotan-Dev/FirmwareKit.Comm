using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Backend.MacOS;

/// <summary>
/// P/Invoke surface for the IOKit.framework classic (MIG) API.
/// <para>IOKit.framework 经典（MIG）API 的 P/Invoke 表面。</para>
/// </summary>
/// <remarks>
/// <b>Migration status — INCOMPLETE (documented for follow-up).</b>
/// <para><b>迁移状态——未完成（已文档化以备后续实现）。</b></para>
///
/// The macOS native backend originally targeted the IOUSBHost.framework
/// user-space C API (<c>IOUSBLibCopyDevices</c>, <c>IOUSBHostDeviceOpen</c>,
/// <c>IOUSBHostPipeBulkTransfer</c>, ...). On the current macOS release the
/// IOUSBHost.framework main binary is absent — only a BridgeSupport stub
/// remains — so every <c>[DllImport(IOUSBHost)]</c> throws
/// <c>DllNotFoundException</c>, <c>MacHostUsbFinder.FindDevice</c> silently
/// returns an empty list, and the CLI reports
/// <c>copy-devices=False; scanned=0</c>.
/// <para>macOS 原生后端最初面向 IOUSBHost.framework 用户态 C API
/// （<c>IOUSBLibCopyDevices</c>、<c>IOUSBHostDeviceOpen</c>、
/// <c>IOUSBHostPipeBulkTransfer</c>……）。当前 macOS 发行版中
/// IOUSBHost.framework 主二进制缺失——仅剩 BridgeSupport 桩——因此每个
/// <c>[DllImport(IOUSBHost)]</c> 都抛 <c>DllNotFoundException</c>，
/// <c>MacHostUsbFinder.FindDevice</c> 静默返回空列表，CLI 报告
/// <c>copy-devices=False; scanned=0</c>。</para>
///
/// This file declares the replacement surface: the IOKit classic API that
/// lives in IOKit.framework and is loadable on every macOS release. The
/// declarations here are validated to P/Invoke correctly
/// (<c>IOServiceMatching("IOUSBDevice")</c> returns a +1 dictionary;
/// <c>IOServiceGetMatchingServices</c> returns 0 and yields 10
/// <c>IOUSBDevice</c> services on the test host).
/// <para>本文件声明替代表面：位于 IOKit.framework、在每个 macOS 发行版均可加载
/// 的 IOKit 经典 API。此处的声明已验证 P/Invoke 正确
/// （<c>IOServiceMatching("IOUSBDevice")</c> 返回 +1 字典；
/// <c>IOServiceGetMatchingServices</c> 返回 0，并在测试主机上产生 10 个
/// <c>IOUSBDevice</c> 服务）。</para>
///
/// <b>Remaining work to complete the migration:</b>
/// <para><b>完成迁移所需剩余工作：</b></para>
/// <list type="bullet">
/// <item><term>IOKitUsbFinder</term><description>Replace <c>MacHostUsbFinder.FindDevice</c>:
/// iterate <c>IOServiceGetMatchingServices(IOServiceMatching("IOUSBDevice"))</c>,
/// read <c>idVendor</c>/<c>idProduct</c>/<c>USB Product Name</c> via
/// <c>IORegistryEntryCreateCFProperty</c> (CFNumber for VID/PID — read with
/// <c>CFNumberGetValue</c>, NOT <c>Marshal.Copy</c>; CFString for the name —
/// read with <c>CFStringGetCString</c>). The earlier probe crashed with
/// <c>Bus error: 10 (exit 138)</c> by treating a CFNumber ref as 8 raw bytes;
/// use the CFNumber accessor instead.
/// <para>替换 <c>MacHostUsbFinder.FindDevice</c>：迭代
/// <c>IOServiceGetMatchingServices(IOServiceMatching("IOUSBDevice"))</c>，
/// 通过 <c>IORegistryEntryCreateCFProperty</c> 读取 <c>idVendor</c>/
/// <c>idProduct</c>/<c>USB Product Name</c>（VID/PID 为 CFNumber——用
/// <c>CFNumberGetValue</c> 读取，<b>不是</b> <c>Marshal.Copy</c>；name 为
/// CFString——用 <c>CFStringGetCString</c> 读取）。早先的探针因把 CFNumber 引用
/// 当 8 字节裸数据而以 <c>Bus error: 10 (exit 138)</c> 崩溃；改用 CFNumber
/// 访问器。</description></item>
/// <item><term>IOKitUsbDevice</term><description>Replace <c>MacHostUsbDevice</c>:
/// for each <c>IOUSBDevice</c> service, call
/// <c>IOCreatePlugInInterfaceForService(service, kIOUSBDeviceUserClientTypeID,
/// kIOUSBDeviceInterfaceID, &amp;plugin, &amp;score)</c>, then
/// <c>plugin-&gt;QueryInterface(plugin, CFUUIDGetUUIDBytes(kIOUSBDeviceInterfaceID),
/// &amp;dev)</c> to obtain the <c>IOUSBDeviceInterface320</c> COM-vtable. Use
/// <c>dev-&gt;DeviceOpen</c>, <c>SetConfiguration</c>,
/// <c>GetConfigurationDescriptorPtr</c> to walk descriptors,
/// <c>CreateInterfaceIterator</c> + <c>IOUSBInterfaceInterface</c> for interface
/// claim, and <c>ReadPipe</c>/<c>WritePipe</c> for bulk I/O. The COM-vtable
/// must be declared as a C# interface with <c>[StructLayout(LayoutKind.Sequential)]</c>
/// function-pointer fields at the correct vtable offsets (192/320 vary by SDK).
/// <para>替换 <c>MacHostUsbDevice</c>：对每个 <c>IOUSBDevice</c> 服务调用
/// <c>IOCreatePlugInInterfaceForService(service, kIOUSBDeviceUserClientTypeID,
/// kIOUSBDeviceInterfaceID, &amp;plugin, &amp;score)</c>，再
/// <c>plugin-&gt;QueryInterface(...)</c> 取得 <c>IOUSBDeviceInterface320</c>
/// COM-vtable。用 <c>DeviceOpen</c>、<c>SetConfiguration</c>、
/// <c>GetConfigurationDescriptorPtr</c> 走描述符，<c>CreateInterfaceIterator</c> +
/// <c>IOUSBInterfaceInterface</c> 声明接口，<c>ReadPipe</c>/<c>WritePipe</c> 做
/// bulk I/O。COM-vtable 须声明为 C# 接口，函数指针字段按正确 vtable 偏移布局
/// （192/320 随 SDK 而异）。</description></item>
/// <item><term>Provider wiring</term><description>
/// <c>NativeUsbApiProvider</c> on macOS currently instantiates the legacy
/// <c>MacHostUsbFinder</c>/<c>MacHostUsbDevice</c>. Either (a) route macOS
/// native through <c>IOKitUsbFinder</c>/<c>IOKitUsbDevice</c>, or (b) keep the
/// IOUSBHost path as a fallback and prefer the IOKit classic path when
/// <c>IOUSBLibCopyDevices</c> throws <c>DllNotFoundException</c>. Option (a)
/// is simpler and is recommended once the IOKit device path is validated.
/// <para>macOS 上 <c>NativeUsbApiProvider</c> 当前实例化旧版
/// <c>MacHostUsbFinder</c>/<c>MacHostUsbDevice</c>。要么 (a) 把 macOS native 路由
/// 到 <c>IOKitUsbFinder</c>/<c>IOKitUsbDevice</c>，要么 (b) 保留 IOUSBHost 路径
/// 作回退，并在 <c>IOUSBLibCopyDevices</c> 抛 <c>DllNotFoundException</c> 时优先
/// IOKit 经典路径。一旦 IOKit 设备路径验证通过，方案 (a) 更简单，推荐采用。
/// </description></item>
/// <item><term>Validation</term><description>After migration, run the CLI:
/// <c>dotnet FirmwareKit.Comm.CLI/bin/Release/net10.0/FirmwareKit.Comm.CLI.dll
/// devices --api native --vid 0x18D1</c>. Expected: perseus enumerated
/// (<c>vid=0x18D1 pid=0x4EE7 if=0xFF/0x42/0x01</c>). Compare against the
/// libusb backend (<c>--api libusb</c>) which already works with
/// <c>DYLD_FALLBACK_LIBRARY_PATH=/opt/homebrew/lib</c>.
/// <para>迁移后运行 CLI：
/// <c>dotnet FirmwareKit.Comm.CLI/bin/Release/net10.0/FirmwareKit.Comm.CLI.dll
/// devices --api native --vid 0x18D1</c>。预期：perseus 被枚举
/// （<c>vid=0x18D1 pid=0x4EE7 if=0xFF/0x42/0x01</c>）。与已可在
/// <c>DYLD_FALLBACK_LIBRARY_PATH=/opt/homebrew/lib</c> 下工作的 libusb 后端
/// （<c>--api libusb</c>）对比。</description></item>
/// </list>
///
/// <b>Why not just ship this now?</b> The IOKit classic device path requires a
/// COM-vtable binding (<c>IOUSBDeviceInterface320</c> + <c>IOUSBInterfaceInterface</c>)
/// with ~40 function-pointer slots at SDK-specific offsets, plus CFNumber /
/// CFString accessors. Getting any offset wrong crashes with
/// <c>EXC_BAD_ACCESS</c>. This is a focused, testable follow-up — not a
/// blocker, because the libusb backend already enumerates and talks to
/// perseus on macOS.
/// <para><b>为何不立即发布？</b>IOKit 经典设备路径需要 COM-vtable 绑定
/// （<c>IOUSBDeviceInterface320</c> + <c>IOUSBInterfaceInterface</c>），约 40 个
/// 函数指针槽位、SDK 特定偏移，外加 CFNumber/CFString 访问器。任一偏移错误都会以
/// <c>EXC_BAD_ACCESS</c> 崩溃。这是一项聚焦、可测的后续工作——并非阻塞项，因为
/// libusb 后端已在 macOS 上枚举并与 perseus 通信。</para>
/// </remarks>
/// <remarks>
/// On the current macOS the IOUSBHost.framework user-space C API
/// (<c>IOUSBLibCopyDevices</c>, <c>IOUSBHostDeviceOpen</c>, ...) is no longer
/// loadable — the framework's main binary is absent and only a BridgeSupport
/// stub remains. This surface falls back to the IOKit classic API, which lives
/// in IOKit.framework and is available on every macOS release. Device I/O uses
/// the <c>IOUSBDeviceInterface</c> COM-vtable obtained via
/// <c>IOCreatePlugInInterfaceForService</c>.
/// <para>当前 macOS 上 IOUSBHost.framework 用户态 C API
/// （<c>IOUSBLibCopyDevices</c>、<c>IOUSBHostDeviceOpen</c>……）已不可加载——框架
/// 主二进制缺失，仅剩 BridgeSupport 桩。本表面回退到 IOKit 经典 API，该 API 位于
/// IOKit.framework，在每个 macOS 发行版上均可用。设备 I/O 使用通过
/// <c>IOCreatePlugInInterfaceForService</c> 获取的 <c>IOUSBDeviceInterface</c>
/// COM-vtable。</para>
/// </remarks>
internal static class IOKitUsbAPI
{
    public const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
    public const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    public const string Libc = "libc";

    // ---- Service matching / iteration ----
    // CFMutableDictionaryRef IOServiceMatching(const char *name);
    // Returns a +1 reference that IOServiceGetMatchingServices consumes.
    [DllImport(IOKit)]
    public static extern IntPtr IOServiceMatching(string name);

    // kern_return_t IOServiceGetMatchingServices(mach_port_t masterPort,
    //   CFDictionaryRef matching, io_iterator_t *existing);
    [DllImport(IOKit)]
    public static extern int IOServiceGetMatchingServices(IntPtr masterPort, IntPtr matching, out IntPtr existing);

    // kern_return_t IOServiceGetMatchingService(mach_port_t masterPort,
    //   CFDictionaryRef matching);
    // Consumes the matching dictionary. Returns the first match or 0.
    [DllImport(IOKit)]
    public static extern IntPtr IOServiceGetMatchingService(IntPtr masterPort, IntPtr matching);

    // io_object_t IOIteratorNext(io_iterator_t iterator);
    // io_object_t and io_iterator_t are both mach_port_t (unsigned int, 32-bit).
    // Using IntPtr (8 bytes on 64-bit) would pass garbage in the high 4 bytes
    // and the native function would read a wrong iterator value, returning 0.
    // <para>io_object_t 和 io_iterator_t 均为 mach_port_t（unsigned int，32 位）。
    // 用 IntPtr（64 位上 8 字节）会在高 4 字节传垃圾，native 函数读到错误迭代器值
    // 而返回 0。</para>
    [DllImport(IOKit)]
    public static extern uint IOIteratorNext(IntPtr iterator);

    // kern_return_t IOObjectRelease(io_object_t object);
    [DllImport(IOKit)]
    public static extern int IOObjectRelease(IntPtr obj);

    // kern_return_t IOObjectRetain(io_object_t object);
    [DllImport(IOKit)]
    public static extern int IOObjectRetain(IntPtr obj);

    // ---- Registry properties ----
    // CFTypeRef IORegistryEntryCreateCFProperty(io_registry_entry_t entry,
    //   CFStringRef key, CFAllocatorRef allocator, IOOptionBits options);
    // Returns a +1 reference (caller must CFRelease) or NULL.
    [DllImport(IOKit)]
    public static extern IntPtr IORegistryEntryCreateCFProperty(IntPtr entry, IntPtr key, IntPtr allocator, uint options);

    // kern_return_t IORegistryEntryCreateCFProperties(io_registry_entry_t entry,
    //   CFMutableDictionaryRef *properties, CFAllocatorRef allocator, IOOptionBits options);
    // Fills *properties with a +1 dictionary of all properties.
    [DllImport(IOKit)]
    public static extern int IORegistryEntryCreateCFProperties(IntPtr entry, out IntPtr properties, IntPtr allocator, uint options);

    // kern_return_t IORegistryEntryGetLocationInPlane(io_registry_entry_t entry,
    //   const io_name_t plane, io_name_t location);
    [DllImport(IOKit)]
    public static extern int IORegistryEntryGetLocationInPlane(IntPtr entry, string plane, byte[] location);

    // kern_return_t IORegistryEntryGetPath(io_registry_entry_t entry,
    //   const io_name_t plane, io_string_t path);
    [DllImport(IOKit)]
    public static extern int IORegistryEntryGetPath(IntPtr entry, string plane, byte[] path);

    // kern_return_t IORegistryEntryGetName(io_registry_entry_t entry, io_name_t name);
    [DllImport(IOKit)]
    public static extern int IORegistryEntryGetName(IntPtr entry, byte[] name);

    // uint64_t IORegistryEntryGetRegistryEntryID(io_registry_entry_t entry);
    // io_registry_entry_t is mach_port_t (unsigned int, 32-bit). Passing IntPtr
    // (8 bytes on 64-bit) would feed garbage in the high 4 bytes and the native
    // function would read a wrong entry handle, returning 0.
    // <para>io_registry_entry_t 为 mach_port_t（unsigned int，32 位）。传 IntPtr
    // （64 位上 8 字节）会在高 4 字节传垃圾，native 函数读到错误项句柄而返回 0。</para>
    [DllImport(IOKit)]
    public static extern ulong IORegistryEntryGetRegistryEntryID(uint entry);

    // ---- Plug-in interface (COM-vtable) ----
    // kern_return_t IOCreatePlugInInterfaceForService(io_service_t service,
    //   CFUUIDRef pluginType, CFUUIDRef interfaceType,
    //   IOCFPlugInInterface ***theInterface, SInt32 *theScore);
    // Returns a +1 plug-in interface in *theInterface.
    // The IntPtr form is kept for callers that already hold a CFUUIDRef; the
    // Guid overload below lets .NET marshal a Guid into a CFUUIDRef directly
    // (the exact calling shape SharpFastboot's macOS backend uses).
    // <para>以 Guid 形式调用时，.NET 将 Guid marshal 为 CFUUIDRef（SharpFastboot
    // 的 macOS 后端使用的正是该调用形）。IntPtr 形保留给已持有 CFUUIDRef 的调用方。</para>
    [DllImport(IOKit)]
    public static extern int IOCreatePlugInInterfaceForService(
        IntPtr service,
        IntPtr pluginType,
        IntPtr interfaceType,
        ref IntPtr theInterface,
        ref int theScore);

    [DllImport(IOKit)]
    public static extern int IOCreatePlugInInterfaceForService(
        IntPtr service,
        Guid pluginType,
        Guid interfaceType,
        out IntPtr theInterface,
        out int theScore);

    // kern_return_t IODestroyPlugInInterface(IOCFPlugInInterface **theInterface);
    [DllImport(IOKit)]
    public static extern int IODestroyPlugInInterface(ref IntPtr theInterface);

    // ---- CoreFoundation helpers ----
    [DllImport(CoreFoundation)]
    public static extern void CFRelease(IntPtr obj);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFRetain(IntPtr obj);

    [DllImport(CoreFoundation)]
    public static extern long CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, long index);

    // CFStringRef CFStringCreateWithCString(CFAllocatorRef alloc,
    //   const char *cStr, CFStringEncoding encoding);
    [DllImport(CoreFoundation)]
    public static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, int encoding);

    // CFTypeID CFGetTypeID(CFTypeRef cf);
    [DllImport(CoreFoundation)]
    public static extern IntPtr CFGetTypeID(IntPtr cf);

    // CFNumberRef CFNumberCreate(CFAllocatorRef alloc, CFNumberType theType, const void *valuePtr);
    [DllImport(CoreFoundation)]
    public static extern IntPtr CFNumberCreate(IntPtr alloc, int theType, IntPtr valuePtr);

    // CFPropertyListRef CFPropertyListCreateWithData(CFAllocatorRef alloc,
    //   CFDataRef data, CFOptionFlags options, CFPropertyListFormat *format, CFErrorRef *error);
    [DllImport(CoreFoundation)]
    public static extern IntPtr CFPropertyListCreateWithData(IntPtr alloc, IntPtr data, uint options, out int format, out IntPtr error);

    // void CFShow(CFTypeRef obj); // prints a CF object to stderr for debugging
    [DllImport(CoreFoundation)]
    public static extern void CFShow(IntPtr obj);

    // malloc/free for IORegistryEntryCreateCFProperty results
    [DllImport(Libc, EntryPoint = "free")]
    public static extern void Free(IntPtr ptr);

    // ---- Constants ----
    public const int kCFStringEncodingUTF8 = 0x08000100;
    public const int kCFStringEncodingMacRoman = 0;

    public const int kIOReturnSuccess = 0;
    public const int kIOReturnNoDevice = unchecked((int)0xE00002C0);
    public const int kIOReturnAborted = unchecked((int)0xE00002EB);
    public const int kIOReturnTimeout = unchecked((int)0xE00002D6);
    public const int kIOReturnNotResponding = unchecked((int)0xE00002ED);
    public const int kIOReturnExclusiveAccess = unchecked((int)0xE00002E5);
    public const int kIOReturnError = unchecked((int)0xE0000001);

    // IOOptionBits for IORegistryEntryCreateCFProperty / CreateCFProperties
    public const uint kIORegistryIterateRecursively = 0x00000001;
    public const uint kIORegistryIterateParents = 0x00000002;

    // USB descriptor types
    public const byte USB_DESCRIPTOR_TYPE_CONFIGURATION = 2;
    public const byte USB_DESCRIPTOR_TYPE_INTERFACE = 4;
    public const byte USB_DESCRIPTOR_TYPE_ENDPOINT = 5;

    // The IOUSBDevice service class name used with IOServiceMatching.
    public const string IOUSBDeviceClassName = "IOUSBDevice";

    // The IOService plane name passed to IORegistryEntryGetPath.
    public const string IOServicePlane = "IOService";

    // ---- COM UUIDs (verified in SharpFastboot's macOS backend) ----
    // <para>COM UUID（已在 SharpFastboot 的 macOS 后端验证）。</para>
    public static readonly Guid kIOUSBDeviceUserClientTypeID = new("9d7d2100-ba54-11d4-8113-0005020c020c");
    public static readonly Guid kIOUSBDeviceInterfaceID = new("5c3a030d-27d1-11d4-9d10-0005020c020c");
    public static readonly Guid kIOCFPlugInInterfaceID = new("c244e858-109c-11d4-91d4-0050e4c6426f");
    public static readonly Guid kIOUSBInterfaceUserClientTypeID = new("2d9786c6-9ef3-11d4-ad51-000a27052861");
    public static readonly Guid kIOUSBInterfaceInterfaceID190 = new("d44fd2f8-002d-11d6-8e5e-000a27052861");

    public const int S_OK = 0;

    // IOUSBFindInterfaceRequest b* fields use kIOUSBFindInterfaceDontCare (0xFFFF)
    // to match any value.
    // <para>IOUSBFindInterfaceRequest 的 b* 字段用 kIOUSBFindInterfaceDontCare
    // （0xFFFF）匹配任意值。</para>
    public const ushort kIOUSBFindInterfaceDontCare = 0xFFFF;

    // ---- IOUSBFindInterfaceRequest (used by USBDeviceCreateInterfaceIterator) ----
    [StructLayout(LayoutKind.Sequential)]
    public struct IOUSBFindInterfaceRequest
    {
        public ushort bInterfaceClass;
        public ushort bInterfaceSubClass;
        public ushort bInterfaceProtocol;
        public ushort bAlternateSetting;
    }

    // ---- COM-vtable delegates (IOCFPlugInInterface / IOUSBDeviceInterface /
    // IOUSBInterfaceInterface), verified offsets from SharpFastboot ----
    // <para>COM-vtable 委托（IOCFPlugInInterface / IOUSBDeviceInterface /
    // IOUSBInterfaceInterface），已验证偏移取自 SharpFastboot。</para>

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int QueryInterfaceDelegate(IntPtr self, Guid iid, out IntPtr ppv);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint ReleaseDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBDeviceOpenDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBDeviceCloseDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBDeviceResetDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBGetDeviceVendorDelegate(IntPtr self, out ushort devVendor);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBGetDeviceProductDelegate(IntPtr self, out ushort devProduct);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBGetSerialNumberStringIndexDelegate(IntPtr self, out byte serialIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBDeviceCreateInterfaceIteratorDelegate(IntPtr self, ref IOUSBFindInterfaceRequest request, out IntPtr iterator);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBGetConfigurationDelegate(IntPtr self, out byte configNumber);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBSetConfigurationDelegate(IntPtr self, byte configNumber);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int DeviceRequestDelegate(IntPtr self, ref IOUSBDeviceRequest request);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBInterfaceOpenDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int USBInterfaceCloseDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int GetNumEndpointsDelegate(IntPtr self, out byte numEndpoints);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int GetPipePropertiesDelegate(IntPtr self, byte pipeRef, out byte direction, out byte number, out byte transferType, out ushort maxPacketSize, out byte interval);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int ClearPipeStallBothEndsDelegate(IntPtr self, byte pipeRef);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int ReadPipeTODelegate(IntPtr self, byte pipeRef, IntPtr data, ref uint size, uint noDataTimeout, uint completionTimeout);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int WritePipeTODelegate(IntPtr self, byte pipeRef, IntPtr data, uint size, uint noDataTimeout, uint completionTimeout);

    // VTable offsets (0-based function-pointer slots) for IOCFPlugInInterface.
    public const int Offset_Plugin_QueryInterface = 1;
    public const int Offset_Plugin_Release = 3;

    // VTable offsets for common IUnknown (shared by interface & device COM objects).
    public const int Offset_IUnknown_QueryInterface = 1;
    public const int Offset_IUnknown_Release = 3;

    // VTable offsets for IOUSBDeviceInterface (relative to IUnknown start).
    public const int Offset_DeviceRequest = 7;
    public const int Offset_USBDeviceOpen = 14;
    public const int Offset_USBDeviceClose = 15;
    public const int Offset_USBGetDeviceVendor = 16;
    public const int Offset_USBGetDeviceProduct = 17;
    public const int Offset_USBGetSerialNumberStringIndex = 21;
    public const int Offset_USBGetConfiguration = 24;
    public const int Offset_USBSetConfiguration = 25;
    public const int Offset_USBDeviceCreateInterfaceIterator = 26;
    public const int Offset_USBDeviceReset = 27;

    // VTable offsets for IOUSBInterfaceInterface (190+).
    public const int Offset_USBInterfaceOpen = 8;
    public const int Offset_USBInterfaceClose = 9;
    public const int Offset_GetNumEndpoints = 17;
    public const int Offset_GetPipeProperties = 18;
    public const int Offset_ClearPipeStallBothEnds = 25;
    public const int Offset_ReadPipe = 26;
    public const int Offset_WritePipe = 27;
    public const int Offset_ReadPipeTO = 28;
    public const int Offset_WritePipeTO = 29;

    // Reads a COM vtable method pointer at `offset` (0-based slot) and wraps it
    // into the requested delegate type. The vtable itself is the first pointer
    // pointed to by `self`; each slot is one IntPtr wide.
    // <para>读取 `self` 指向的 COM vtable 在 `offset`（0 基槽位）处的方法指针并包装为
    // 请求的委托类型。vtable 本身是 `self` 指向的首个指针；每槽一个 IntPtr 宽。</para>
    public static T GetDelegate<T>(IntPtr self, int offset) where T : class
    {
        IntPtr vtable = Marshal.ReadIntPtr(self);
        IntPtr methodPtr = Marshal.ReadIntPtr(vtable, offset * IntPtr.Size);
        return (T)(object)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(T));
    }

    // io_service_t IORegistryEntryFromPath(mach_port_t mainPort, const io_string_t path);
    // Re-opens a device service from its IOService-plane path.
    // <para>从 IOService 平面路径重开设备服务。</para>
    [DllImport(IOKit)]
    public static extern IntPtr IORegistryEntryFromPath(IntPtr masterPort, string path);

    // ---- Structs ----
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
}
