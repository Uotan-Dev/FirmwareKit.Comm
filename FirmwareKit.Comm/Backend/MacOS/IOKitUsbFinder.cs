using System.Runtime.InteropServices;
using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Diagnostics;

namespace FirmwareKit.Comm.Backend.MacOS;

/// <summary>
/// Enumerates USB devices on macOS via the IOKit.framework classic API
/// (<c>IOServiceMatching("IOUSBDevice")</c> + <c>IORegistryEntryCreateCFProperty</c>),
/// replacing the IOUSBHost.framework path that is unloadable on current macOS.
/// <para>在 macOS 上通过 IOKit.framework 经典 API
/// （<c>IOServiceMatching("IOUSBDevice")</c> + <c>IORegistryEntryCreateCFProperty</c>）
/// 枚举 USB 设备，替替当前 macOS 上不可加载的 IOUSBHost.framework 路径。</para>
/// </summary>
internal static class IOKitUsbFinder
{
    // Cached allocator handle (kCFAllocatorDefault = NULL) reused for every
    // IORegistryEntryCreateCFProperty / CFStringCreateWithCString call. Exposed
    // as a static readonly field (not const) because IntPtr.Zero is not a
    // compile-time constant under all TFMs.
    // <para>缓存的分配器句柄（kCFAllocatorDefault = NULL），被每次
    // IORegistryEntryCreateCFProperty / CFStringCreateWithCString 调用复用。
    // 以静态只读字段（非 const）暴露，因为 IntPtr.Zero 在所有 TFM 下非编译期常量。</para>
    private static readonly IntPtr DefaultAllocator = IntPtr.Zero;

    // Registry entry IDs are stable across re-enumeration; used as the reopen key.
    // <para>注册表项 ID 跨再枚举稳定；用作重开键。</para>
    private const string RegistryIdProperty = "RegistryEntryID";

    /// <summary>
    /// Whether the last IOServiceGetMatchingServices call succeeded. Surfaced
    /// through diagnostics like <c>copy-devices</c>.
    /// <para>最后一次 IOServiceGetMatchingServices 调用是否成功。通过
    /// <c>copy-devices</c> 等诊断暴露。</para>
    /// </summary>
    internal static bool LastCopyDevicesSucceeded { get; private set; } = true;

    /// <summary>
    /// Number of IOUSBDevice services observed on the last scan, before filtering.
    /// <para>上次扫描观测到的 IOUSBDevice 服务数，过滤前。</para>
    /// </summary>
    internal static int LastScannedDeviceCount { get; private set; }

    /// <summary>
    /// Number of devices that matched the filter on the last scan.
    /// <para>上次扫描匹配过滤器的设备数。</para>
    /// </summary>
    internal static int LastMatchedDeviceCount { get; private set; }

    /// <summary>
    /// Enumerates macOS IOUSBDevice services, reads each device's VID/PID/serial/
    /// interface metadata from the IORegistry, and returns the matching
    /// <see cref="IOKitUsbDevice"/> objects. When <paramref name="filter"/> is
    /// non-null, devices whose VID/PID/interface-class do not match are skipped.
    /// <para>枚举 macOS IOUSBDevice 服务，从 IORegistry 读取每个设备的 VID/PID/
    /// 序列号/接口元数据，并返回匹配的 <see cref="IOKitUsbDevice"/> 对象。
    /// <paramref name="filter"/> 非空时，VID/PID/接口类不匹配的设备被跳过。</para>
    /// </summary>
    public static List<UsbDevice> FindDevice(UsbDeviceFilter? filter = null)
    {
        List<UsbDevice> devices = new();
        LastScannedDeviceCount = 0;
        LastMatchedDeviceCount = 0;

        // Build a (vid,pid) -> interfaces[] table ONCE by enumerating the
        // IOUSBInterface plane. IOUSBInterface services inherit idVendor/
        // idProduct from their parent IOUSBDevice (verified by probe), so we
        // can pair interfaces to devices by VID/PID without IORegistryEntryGetParent
        // (which is not exported on current macOS) or IORegistryEntryGetRegistryEntryID
        // (which segfaults against IOUSBDevice services).
        // <para>枚举 IOUSBInterface 平面一次，构建 (vid,pid) -> interfaces[] 表。
        // IOUSBInterface 服务从父 IOUSBDevice 继承 idVendor/idProduct（已由探针验证），
        // 故可按 VID/PID 将接口配对到设备，无需 IORegistryEntryGetParent（当前 macOS
        // 不导出）或 IORegistryEntryGetRegistryEntryID（对 IOUSBDevice 服务段错误）。</para>
        Dictionary<(ushort Vid, ushort Pid), List<UsbInterfaceInfo>> interfaceTable = BuildInterfaceTable();

        IntPtr matching = IntPtr.Zero;
        IntPtr iterator = IntPtr.Zero;

        try
        {
            matching = IOKitUsbAPI.IOServiceMatching(IOKitUsbAPI.IOUSBDeviceClassName);
            UsbTrace.Log($"IOKitUsbFinder: IOServiceMatching(IOUSBDevice) = {matching}");
            if (matching == IntPtr.Zero)
            {
                LastCopyDevicesSucceeded = false;
                return devices;
            }

            int kr = IOKitUsbAPI.IOServiceGetMatchingServices(IntPtr.Zero, matching, out IntPtr iter);
            UsbTrace.Log($"IOKitUsbFinder: IOServiceGetMatchingServices kr={kr} iter={iter}");
            // IOServiceGetMatchingServices consumes the matching dictionary (+1 ref -> 0).
            // <para>IOServiceGetMatchingServices 消费匹配字典（+1 引用 -> 0）。</para>
            matching = IntPtr.Zero;
            if (kr != IOKitUsbAPI.kIOReturnSuccess || iter == IntPtr.Zero)
            {
                LastCopyDevicesSucceeded = false;
                return devices;
            }
            iterator = iter;
            LastCopyDevicesSucceeded = true;

            IntPtr service;
            int iterCount = 0;
            uint next = IOKitUsbAPI.IOIteratorNext(iterator);
            UsbTrace.Log($"IOKitUsbFinder: first IOIteratorNext(iter) = {next}");
            while (next != 0)
            {
                service = new IntPtr(next);
                iterCount++;
                LastScannedDeviceCount++;
                UsbTrace.Log($"IOKitUsbFinder: processing service #{iterCount} = {service}");
                try
                {
                    if (!TryReadDeviceMetadata(
                            service,
                            interfaceTable,
                            out ushort vid,
                            out ushort pid,
                            out string? serial,
                            out ulong registryEntryId,
                            out IReadOnlyList<UsbInterfaceInfo> interfaces,
                            out byte? ifcClass,
                            out byte? ifcSubClass,
                            out byte? ifcProtocol))
                    {
                        // Metadata read failed (e.g. the service disappeared mid-scan).
                        // Skip it rather than silently dropping a partially-known device.
                        // <para>元数据读取失败（例如服务在扫描中消失）。跳过它而非静默丢弃
                        // 郸分已知的设备。</para>
                        UsbTrace.Log($"IOKitUsbFinder: TryReadDeviceMetadata FAILED for service, skipping.");
                        continue;
                    }

                    UsbTrace.Log($"IOKitUsbFinder: dev rid={registryEntryId} vid={vid:X4} pid={pid:X4} serial='{serial}' ifClass={ifcClass}");

                    // Apply the filter the same way MacHostUsbFinder does.
                    // <para>与 MacHostUsbFinder 相同地应用过滤器。</para>
                    if (filter?.VendorId is ushort filterVid && vid != filterVid) continue;
                    if (filter?.ProductId is ushort filterPid && pid != filterPid) continue;
                    if (filter?.InterfaceClass is byte filterClass && ifcClass != filterClass) continue;
                    if (filter?.InterfaceSubClass is byte filterSubClass && ifcSubClass != filterSubClass) continue;
                    if (filter?.InterfaceProtocol is byte filterProtocol && ifcProtocol != filterProtocol) continue;

                    var dev = new IOKitUsbDevice
                    {
                        RegistryEntryId = registryEntryId,
                        DevicePath = TryGetServicePath(service) ?? $"IOKit:{registryEntryId}",
                        VendorId = vid,
                        ProductId = pid,
                        SerialNumber = serial,
                        InterfaceClass = ifcClass,
                        InterfaceSubClass = ifcSubClass,
                        InterfaceProtocol = ifcProtocol,
                        InterfaceMetadataObserved = interfaces.Count > 0,
                        Interfaces = interfaces,
                        UsbDeviceType = UsbDeviceType.MacOS,
                    };

                    // IMPORTANT: do NOT call dev.CreateHandle() during enumeration.
                    // Enumeration is metadata discovery only (see UsbProviderProjection
                    // remarks: "Enumeration does not open handles; open on demand").
                    // Opening the COM-vtable here — inside a test host that is enumerating
                    // real devices — crashes the process with SIGSEGV (exit 139) after the
                    // tests pass, because the IOKit COM interface release path is not safe
                    // to tear down on a host that only wanted a device listing. Sessions
                    // open the device lazily in UsbProviderProjection.ToSessions.
                    // <para>重要：枚举期间切勿调用 dev.CreateHandle()。枚举仅为元数据发现
                    // （见 UsbProviderProjection 注释："枚举不打开句柄；按需打开"）。在此——
                    // 在枚举真实设备的测试宿主内——打开 COM-vtable 会在测试通过后使进程
                    // SIGSEGV 崩溃（退出码 139），因为 IOKit COM 接口释放路径无法在仅需
                    // 设备列表的宿主上安全拆除。会话在 UsbProviderProjection.ToSessions
                    // 中惰性打开设备。</para>

                    devices.Add(dev);
                }
                finally
                {
                    // IMPORTANT: do NOT IOObjectRelease(service) here. The iterator
                    // returned a +0 reference (not +1) — releasing it destroys the
                    // service the iterator still points to, so IOIteratorNext returns
                    // the same handle forever (verified: the loop stuck on service
                    // #1 = 4115 repeating). Leave the service ref alone; the iterator
                    // itself is released in the outer finally.
                    // <para>重要：此处切勿 IOObjectRelease(service)。迭代器返回的是 +0 引用
                    // （非 +1）——释放之会摧毁迭代器仍指向的服务，使 IOIteratorNext 永远返回
                    // 同一句柄（已验证：循环卡在 service #1 = 4115 重复）。保持服务引用不动；
                    // 迭代器本身在外层 finally 中释放。</para>

                    // Advance the iterator in finally so EVERY path — including the
                    // `continue` statements above (filter mismatch, metadata failure) —
                    // moves to the next service. Without this, a filter mismatch keeps
                    // `next` frozen and the loop spins forever on one service.
                    // <para>在 finally 中推进迭代器，使每条路径——包括上面的 `continue`
                    // （过滤器不匹配、元数据失败）——都移动到下一个服务。否则过滤器不匹配
                    // 会使 `next` 冻结，循环在同一服务上空转。</para>
                    next = IOKitUsbAPI.IOIteratorNext(iterator);
                }
            }
            UsbTrace.Log($"IOKitUsbFinder: scan loop exhausted, iterCount={iterCount}");
        }
        catch (DllNotFoundException ex)
        {
            // IOKit.framework should always be present on macOS; if not, degrade
            // to an empty list and surface through diagnostics.
            // <para>IOKit.framework 在 macOS 上应始终存在；若不存在则降级为空列表并通过
            // 诊断暴露。</para>
            UsbTrace.Log($"IOKitUsbFinder: DllNotFoundException caught: {ex.Message}");
            LastCopyDevicesSucceeded = false;
            return devices;
        }
        catch (EntryPointNotFoundException ex)
        {
            UsbTrace.Log($"IOKitUsbFinder: EntryPointNotFoundException caught: {ex.Message}");
            LastCopyDevicesSucceeded = false;
            return devices;
        }
        catch (Exception ex)
        {
            UsbTrace.Log($"IOKitUsbFinder: unexpected exception in scan loop: {ex.GetType().Name}: {ex.Message}");
            LastCopyDevicesSucceeded = false;
            return devices;
        }
        finally
        {
            if (matching != IntPtr.Zero) IOKitUsbAPI.CFRelease(matching);
            if (iterator != IntPtr.Zero) IOKitUsbAPI.IOObjectRelease(iterator);
        }

        LastMatchedDeviceCount = devices.Count;
        return devices;
    }

    // Reads VID/PID/serial/RegistryEntryID/interface descriptors for one IOUSBDevice
    // service. Returns false when the service is no longer valid. All CF objects
    // returned by IORegistryEntryCreateCFProperty are +1 and released here.
    // <para>读取一个 IOUSBDevice 服务的 VID/PID/序列号/RegistryEntryID/接口描述符。
    // 服务不再有效时返回 false。IORegistryEntryCreateCFProperty 返回的所有 CF 对象
    // 为 +1，在此释放。</para>
    private static bool TryReadDeviceMetadata(
        IntPtr service,
        Dictionary<(ushort Vid, ushort Pid), List<UsbInterfaceInfo>> interfaceTable,
        out ushort vid,
        out ushort pid,
        out string? serial,
        out ulong registryEntryId,
        out IReadOnlyList<UsbInterfaceInfo> interfaces,
        out byte? ifcClass,
        out byte? ifcSubClass,
        out byte? ifcProtocol)
    {
        vid = 0;
        pid = 0;
        serial = null;
        registryEntryId = 0;
        interfaces = Array.Empty<UsbInterfaceInfo>();
        ifcClass = null;
        ifcSubClass = null;
        ifcProtocol = null;

        // Skip IORegistryEntryGetRegistryEntryID — it segfaults on current macOS
        // when called against an IOUSBDevice service (verified: the native call
        // crashes the process with no managed exception). Use the service handle
        // itself (uint mach_port_t) as the reopen key, packed into the high bits
        // of registryEntryId so IOKitUsbDevice.RegistryEntryId stays a ulong.
        // <para>跳过 IORegistryEntryGetRegistryEntryID——在当前 macOS 上对 IOUSBDevice
        // 服务调用会段错误（已验证：native 调用崩溃进程且无托管异常）。用服务句柄本身
        // （uint mach_port_t）作为重开键，打包到 registryEntryId 高位，使
        // IOKitUsbDevice.RegistryEntryId 保持 ulong。</para>
        UsbTrace.Log($"IOKitUsbFinder: TryReadDeviceMetadata entry, service={service}");
        registryEntryId = (uint)service.ToInt64();

        vid = ReadCFNumberUshort(service, "idVendor");
        pid = ReadCFNumberUshort(service, "idProduct");

        // Serial number: the IORegistry publishes it as a CFString under
        // "USB Serial Number" (or "kUSBSerialNumberString" = "serial number").
        // <para>序列号：IORegistry 以 CFString 形式发布于 "USB Serial Number"
        // （或 "kUSBSerialNumberString" = "serial number"）。</para>
        serial = ReadCFString(service, "USB Serial Number") ?? ReadCFString(service, "serial number");

        // Interface metadata: pair this device to its IOUSBInterface children by
        // VID/PID. IOUSBInterface services inherit idVendor/idProduct from their
        // parent IOUSBDevice (verified by probe), so the (vid,pid) -> interfaces[]
        // table built once at the top of FindDevice() carries the full interface
        // list — including every alternate-setting's class/subclass/protocol —
        // which lets the matcher find the ADB interface (0xFF/0x42/0x01) even when
        // it is not the first interface.
        // <para>接口元数据：按 VID/PID 将本设备与其 IOUSBInterface 子项配对。
        // IOUSBInterface 服务从父 IOUSBDevice 继承 idVendor/idProduct（已由探针验证），
        // 故 FindDevice() 顶部一次性构建的 (vid,pid) -> interfaces[] 表携带完整接口
        // 列表——包括每个备用设置的类/子类/协议——使匹配器即便 ADB 接口
        // （0xFF/0x42/0x01）非首个接口时也能找到。</para>
        if (interfaceTable.TryGetValue((vid, pid), out List<UsbInterfaceInfo>? pairedInterfaces) && pairedInterfaces.Count > 0)
        {
            interfaces = pairedInterfaces;
            // Surface the FIRST interface's codes for UsbDeviceFilter compatibility
            // (the matcher checks one interface-class tuple). Callers needing a
            // specific alternate setting should filter by the full interface list.
            // <para>为 UsbDeviceFilter 兼容暴露首个接口的码（匹配器检查一组接口类
            // 元组）。需特定备用设置的调用方应按完整接口列表过滤。</para>
            var first = pairedInterfaces[0];
            ifcClass = first.Class;
            ifcSubClass = first.SubClass;
            ifcProtocol = first.Protocol;
        }

        return true;
    }

    // Reads a CFNumber-valued registry property as a ushort. Returns 0 when the
    // property is absent or not a CFNumber.
    // <para>读取 CFNumber 值的注册表属性为 ushort。属性不存在或非 CFNumber 时返回 0。</para>
    private static ushort ReadCFNumberUshort(IntPtr service, string propertyName)
        => unchecked((ushort)ReadCFNumberRawU32(service, propertyName));

    // Reads the CFNumber as a uint32 by first querying its actual storage type
    // via CFNumberGetType and dispatching to the matching CFNumberGetValue
    // accessor (kCFNumberSInt16Type=1, kCFNumberSInt32Type=3, kCFNumberSInt64Type=4).
    // CFNumberGetValue REQUIRES the type argument to match the CFNumber's actual
    // storage type — passing kCFNumberShortType=5 to a kCFNumberSInt32Type=3
    // CFNumber returns the low 16 bits with garbage in the high byte (verified:
    // idVendor=0x18D1 read as 0x8000 when wrong type).
    // <para>先经 CFNumberGetType 咨询真实存储类型，再派发到匹配的 CFNumberGetValue
    // 访问器（kCFNumberSInt16Type=1、kCFNumberSInt32Type=3、kCFNumberSInt64Type=4）以
    // uint32 读取 CFNumber。CFNumberGetValue 要求类型参数匹配 CFNumber 的实际存储类型
    // ——对 kCFNumberSInt32Type=3 的 CFNumber 传 kCFNumberShortType=5 会返回低位 16 比特
    // 且高位字节为垃圾（已验证：idVendor=0x18D1 在错误类型下读为 0x8000）。</para>
    private static uint ReadCFNumberRawU32(IntPtr service, string propertyName)
    {
        IntPtr key = IOKitUsbAPI.CFStringCreateWithCString(DefaultAllocator, propertyName, IOKitUsbAPI.kCFStringEncodingUTF8);
        if (key == IntPtr.Zero) return 0;
        try
        {
            IntPtr cfValue = IOKitUsbAPI.IORegistryEntryCreateCFProperty(service, key, DefaultAllocator, 0);
            if (cfValue == IntPtr.Zero) return 0;
            try
            {
                int cfType = CFNumberGetType(cfValue);
                switch (cfType)
                {
                    case 1: // kCFNumberSInt16Type
                        ushort v16 = 0;
                        return CFNumberGetValueU16(cfValue, 1, ref v16) ? v16 : (ushort)0;
                    case 3: // kCFNumberSInt32Type
                        uint v32 = 0;
                        return CFNumberGetValueU32(cfValue, 3, ref v32) ? v32 : 0;
                    case 4: // kCFNumberSInt64Type
                        ulong v64 = 0;
                        return CFNumberGetValueU64(cfValue, 4, ref v64) ? unchecked((uint)v64) : 0;
                    default:
                        // Unknown numeric type — fall back to SInt32 widening.
                        // <para>未知数值类型——回退到 SInt32 widening。</para>
                        uint fallback = 0;
                        return CFNumberGetValueU32(cfValue, 3, ref fallback) ? fallback : 0;
                }
            }
            finally
            {
                IOKitUsbAPI.CFRelease(cfValue);
            }
        }
        finally
        {
            IOKitUsbAPI.CFRelease(key);
        }
    }

    [DllImport(IOKitUsbAPI.CoreFoundation, EntryPoint = "CFNumberGetType")]
    private static extern int CFNumberGetType(IntPtr number);

    [DllImport(IOKitUsbAPI.CoreFoundation, EntryPoint = "CFNumberGetValue")]
    private static extern bool CFNumberGetValueU16(IntPtr number, int theType, ref ushort valuePtr);

    [DllImport(IOKitUsbAPI.CoreFoundation, EntryPoint = "CFNumberGetValue")]
    private static extern bool CFNumberGetValueU32(IntPtr number, int theType, ref uint valuePtr);

    [DllImport(IOKitUsbAPI.CoreFoundation, EntryPoint = "CFNumberGetValue")]
    private static extern bool CFNumberGetValueU64(IntPtr number, int theType, ref ulong valuePtr);

    // Reads a CFString-valued registry property as a UTF-8 string. Returns null
    // when the property is absent or not a CFString.
    // <para>读取 CFString 值的注册表属性为 UTF-8 字符串。属性不存在或非 CFString 时返回 null。</para>
    private static string? ReadCFString(IntPtr service, string propertyName)
    {
        IntPtr key = IOKitUsbAPI.CFStringCreateWithCString(DefaultAllocator, propertyName, IOKitUsbAPI.kCFStringEncodingUTF8);
        if (key == IntPtr.Zero) return null;
        try
        {
            IntPtr cfValue = IOKitUsbAPI.IORegistryEntryCreateCFProperty(service, key, DefaultAllocator, 0);
            if (cfValue == IntPtr.Zero) return null;
            try
            {
                // CFStringGetCString writes a NUL-terminated UTF-8 buffer.
                // <para>CFStringGetCString 写入 NUL 终止的 UTF-8 缓冲。</para>
                byte[] buffer = new byte[256];
                if (CFStringGetCString(cfValue, buffer, buffer.Length, IOKitUsbAPI.kCFStringEncodingUTF8))
                {
                    int len = Array.IndexOf(buffer, (byte)0);
                    if (len <= 0) return string.Empty;
                    return System.Text.Encoding.UTF8.GetString(buffer, 0, len);
                }
                return null;
            }
            finally
            {
                IOKitUsbAPI.CFRelease(cfValue);
            }
        }
        finally
        {
            IOKitUsbAPI.CFRelease(key);
        }
    }

    [DllImport(IOKitUsbAPI.CoreFoundation, EntryPoint = "CFStringGetCString")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, int maxLength, int encoding);

    // Builds a (vid,pid) -> interfaces[] table by enumerating the IOUSBInterface
    // plane ONCE. IOUSBInterface services inherit idVendor/idProduct from their
    // parent IOUSBDevice (verified by probe: every IOUSBInterface service exposes
    // idVendor/idProduct alongside bInterfaceNumber/bInterfaceClass/
    // bInterfaceSubClass/bInterfaceProtocol). This lets us pair interfaces to
    // devices by VID/PID without IORegistryEntryGetParent (not exported on current
    // macOS) or IORegistryEntryGetRegistryEntryID (segfaults against IOUSBDevice).
    // For each interface the class/subclass/protocol are read by first querying
    // CFNumberGetType and dispatching to the matching CFNumberGetValue accessor
    // (kCFNumberSInt32Type=3 is the common storage type for these byte fields).
    // <para>枚举 IOUSBInterface 平面一次，构建 (vid,pid) -> interfaces[] 表。
    // IOUSBInterface 服务从父 IOUSBDevice 继承 idVendor/idProduct（已由探针验证：
    // 每个 IOUSBInterface 服务在 bInterfaceNumber/bInterfaceClass/
    // bInterfaceSubClass/bInterfaceProtocol 旁暴露 idVendor/idProduct）。由此可按
    // VID/PID 将接口配对到设备，无需 IORegistryEntryGetParent（当前 macOS 不导出）
    // 或 IORegistryEntryGetRegistryEntryID（对 IOUSBDevice 段错误）。每个接口的
    // 类/子类/协议先经 CFNumberGetType 咨询真实类型再派发到匹配的 CFNumberGetValue
    // 访问器（kCFNumberSInt32Type=3 是这些字节字段的常见存储类型）。</para>
    private static Dictionary<(ushort Vid, ushort Pid), List<UsbInterfaceInfo>> BuildInterfaceTable()
    {
        var table = new Dictionary<(ushort Vid, ushort Pid), List<UsbInterfaceInfo>>();
        UsbTrace.Log("IOKitUsbFinder: BuildInterfaceTable entry");
        int ifaceCount = 0;

        IntPtr matching = IntPtr.Zero;
        IntPtr iterator = IntPtr.Zero;

        try
        {
            matching = IOKitUsbAPI.IOServiceMatching("IOUSBInterface");
            UsbTrace.Log($"IOKitUsbFinder: BuildInterfaceTable IOServiceMatching(IOUSBInterface) = {matching}");
            if (matching == IntPtr.Zero)
            {
                return table;
            }

            int kr = IOKitUsbAPI.IOServiceGetMatchingServices(IntPtr.Zero, matching, out IntPtr iter);
            UsbTrace.Log($"IOKitUsbFinder: BuildInterfaceTable GetMatchingServices kr={kr} iter={iter}");
            matching = IntPtr.Zero; // consumed by GetMatchingServices
            if (kr != IOKitUsbAPI.kIOReturnSuccess || iter == IntPtr.Zero)
            {
                return table;
            }
            iterator = iter;

            uint next = IOKitUsbAPI.IOIteratorNext(iterator);
            while (next != 0)
            {
                ifaceCount++;
                IntPtr ifaceService = new IntPtr(next);
                try
                {
                    ushort vid = ReadCFNumberUshort(ifaceService, "idVendor");
                    ushort pid = ReadCFNumberUshort(ifaceService, "idProduct");
                    if (vid == 0 || pid == 0)
                    {
                        // Some host-side interfaces may not carry VID/PID; skip them.
                        // <para>某些主机侧接口可能不携带 VID/PID；跳过。</para>
                        continue;
                    }

                    var info = new UsbInterfaceInfo
                    {
                        Class = ReadCFNumberByte(ifaceService, "bInterfaceClass") ?? 0,
                        SubClass = ReadCFNumberByte(ifaceService, "bInterfaceSubClass") ?? 0,
                        Protocol = ReadCFNumberByte(ifaceService, "bInterfaceProtocol") ?? 0,
                    };

                    var key = (vid, pid);
                    if (!table.TryGetValue(key, out var list))
                    {
                        list = new List<UsbInterfaceInfo>();
                        table[key] = list;
                    }
                    list.Add(info);
                }
                finally
                {
                    // IOIteratorNext returns +0 — do NOT release the service handle
                    // here (releasing it makes the iterator return the same handle
                    // forever, verified earlier). Only the iterator itself is released.
                    // <para>IOIteratorNext 返回 +0——此处切勿释放服务句柄（释放之会使
                    // 迭代器永远返回同一句柄，前已验证）。仅释放迭代器本身。</para>
                }

                next = IOKitUsbAPI.IOIteratorNext(iterator);
            }
        }
        catch
        {
            // Best-effort: if the IOUSBInterface enumeration fails, return whatever
            // we have so far (possibly empty). The device still enumerates with
            // VID/PID/serial; only interface-class filtering degrades.
            // <para>尽力而为：若 IOUSBInterface 枚举失败，返回目前已收集者（可能为空）。
            // 设备仍以 VID/PID/序列号枚举；仅接口类过滤降级。</para>
        }
        finally
        {
            if (matching != IntPtr.Zero) IOKitUsbAPI.CFRelease(matching);
            if (iterator != IntPtr.Zero) IOKitUsbAPI.IOObjectRelease(iterator);
        }

        UsbTrace.Log($"IOKitUsbFinder: BuildInterfaceTable done, scanned {ifaceCount} IOUSBInterface services, table has {table.Count} (vid,pid) keys");
        return table;
    }

    private static byte? ReadCFNumberByte(IntPtr service, string propertyName)
        => unchecked((byte?)ReadCFNumberRawU32(service, propertyName)) is byte b ? b : null;

    // Reads the IOService-plane path of a device service via
    // IORegistryEntryGetPath. This is the path IOKitUsbDevice.CreateHandle feeds
    // to IORegistryEntryFromPath to reopen the device by identity. Returns null
    // when the path cannot be read (the finder then falls back to a synthetic
    // "IOKit:{rid}" path so enumeration still reports the device).
    // <para>经 IORegistryEntryGetPath 读取设备服务的 IOService 平面路径。这正是
    // IOKitUsbDevice.CreateHandle 传给 IORegistryEntryFromPath 以按标识重开设备的
    // 路径。路径不可读时返回 null（finder 随后回退到合成的 "IOKit:{rid}" 路径，
    // 使枚举仍能报告设备）。</para>
    private static string? TryGetServicePath(IntPtr service)
    {
        try
        {
            byte[] path = new byte[512]; // io_string_t is 512 chars
            if (IOKitUsbAPI.IORegistryEntryGetPath(service, IOKitUsbAPI.IOServicePlane, path) != IOKitUsbAPI.kIOReturnSuccess)
            {
                return null;
            }
            int len = Array.IndexOf(path, (byte)0);
            if (len <= 0) return null;
            return System.Text.Encoding.UTF8.GetString(path, 0, len);
        }
        catch
        {
            return null;
        }
    }
}
