using System.Globalization;
using System.Text.Json;
using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Core;

var argsList = args.ToList();
if (argsList.Count == 0)
{
    ShowHelp();
    return;
}

var layer = new UsbCommunicationLayer();
var command = argsList[0].ToLowerInvariant();

switch (command)
{
    case "apis":
        foreach (var api in layer.GetAvailableApis())
        {
            Console.WriteLine(api);
        }
        break;

    case "devices":
        ExecuteDevices(layer, argsList.Skip(1).ToArray());
        break;

    case "all-devices":
        ExecuteAllDevices(layer, argsList.Skip(1).ToArray());
        break;

    case "monitor":
        ExecuteMonitor(layer, argsList.Skip(1).ToArray());
        break;

    case "selftest":
        ExecuteSelftest(layer, argsList.Skip(1).ToArray());
        break;

    case "io-test":
        ExecuteIoTest(layer, argsList.Skip(1).ToArray());
        break;

    case "interrupt-test":
        ExecuteInterruptTest(layer, argsList.Skip(1).ToArray());
        break;

    default:
        ShowHelp();
        break;
}

static void ExecuteDevices(UsbCommunicationLayer layer, string[] args)
{
    UsbApiKind apiKind = UsbApiKind.Auto;
    var filter = new UsbDeviceFilter();
    bool json = false;

    ushort? vid = null;
    ushort? pid = null;
    string? serial = null;
    string? pathContains = null;
    byte? interfaceClass = null;
    byte? interfaceSubClass = null;
    byte? interfaceProtocol = null;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg == "--api" && i + 1 < args.Length)
        {
            var value = args[++i].ToLowerInvariant();
            apiKind = value switch
            {
                "native" => UsbApiKind.Native,
                "libusb" => UsbApiKind.LibUsbDotNet,
                "auto" => UsbApiKind.Auto,
                _ => throw new ArgumentException($"Unknown api: {value}")
            };
            continue;
        }

        if (arg == "--vid" && i + 1 < args.Length)
        {
            vid = ParseUShort(args[++i]);
            continue;
        }

        if (arg == "--pid" && i + 1 < args.Length)
        {
            pid = ParseUShort(args[++i]);
            continue;
        }

        if (arg == "--serial" && i + 1 < args.Length)
        {
            serial = args[++i];
            continue;
        }

        if (arg == "--path-contains" && i + 1 < args.Length)
        {
            pathContains = args[++i];
            continue;
        }

        if (arg == "--if-class" && i + 1 < args.Length)
        {
            interfaceClass = ParseByte(args[++i]);
            continue;
        }

        if (arg == "--if-subclass" && i + 1 < args.Length)
        {
            interfaceSubClass = ParseByte(args[++i]);
            continue;
        }

        if (arg == "--if-protocol" && i + 1 < args.Length)
        {
            interfaceProtocol = ParseByte(args[++i]);
            continue;
        }

        if (arg is "-h" or "--help")
        {
            ShowHelp();
            return;
        }

        if (arg == "--json")
        {
            json = true;
            continue;
        }

        throw new ArgumentException($"Unknown argument: {arg}");
    }

    filter = new UsbDeviceFilter
    {
        VendorId = vid,
        ProductId = pid,
        SerialNumber = serial,
        DevicePathContains = pathContains,
        InterfaceClass = interfaceClass,
        InterfaceSubClass = interfaceSubClass,
        InterfaceProtocol = interfaceProtocol
    };

    var devices = layer.EnumerateDevices(apiKind, filter);
    PrintDevices(devices, json);

    if (devices.Count == 0)
    {
        Console.Error.WriteLine("No devices matched the selected API and filters.");
    }
}

static void ExecuteAllDevices(UsbCommunicationLayer layer, string[] args)
{
    // Default to current platform backend; caller can override with --api.
    UsbApiKind apiKind = UsbApiKind.Native;
    bool json = false;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg == "--api" && i + 1 < args.Length)
        {
            var value = args[++i].ToLowerInvariant();
            apiKind = value switch
            {
                "native" => UsbApiKind.Native,
                "libusb" => UsbApiKind.LibUsbDotNet,
                "auto" => UsbApiKind.Auto,
                _ => throw new ArgumentException($"Unknown api: {value}")
            };
            continue;
        }

        if (arg is "-h" or "--help")
        {
            ShowHelp();
            return;
        }

        if (arg == "--json")
        {
            json = true;
            continue;
        }

        throw new ArgumentException($"Unknown argument: {arg}");
    }

    var devices = layer.EnumerateDevices(apiKind, filter: null);
    PrintDevices(devices, json);

    // Always print the enumeration diagnostics so CI can assert the mechanism ran
    // even when zero devices are present (device-less runners).
    // <para>始终打印枚举诊断，使 CI 在零设备（无设备 runner）时也能断言枚举机制确实运行。</para>
    Console.Error.WriteLine($"[enum-diagnostics] {layer.GetEnumerationDiagnostics()}");

    if (devices.Count == 0)
    {
        Console.Error.WriteLine("No USB devices were discovered for the selected API on this platform.");
        Console.Error.WriteLine("Hint: set FIRMWAREKIT_USB_DEBUG=1 to see per-backend enumeration diagnostics (usbfs root present, nodes scanned, runtime availability, permission/busy counters).");
    }
}

static void PrintDevices(IReadOnlyList<UsbDeviceInfo> devices, bool json = false)
{
    if (json)
    {
        var payload = devices.Select(d => new
        {
            api = d.ApiName,
            kind = d.SourceApiKind.ToString(),
            vid = $"0x{d.VendorId:X4}",
            pid = $"0x{d.ProductId:X4}",
            interfaceClass = FormatByte(d.InterfaceClass),
            interfaceSubClass = FormatByte(d.InterfaceSubClass),
            interfaceProtocol = FormatByte(d.InterfaceProtocol),
            serial = d.SerialNumber,
            path = d.DevicePath,
            speed = d.Speed.ToString(),
            interfaces = d.Interfaces.Select(i => new
            {
                number = i.InterfaceNumber,
                @class = $"0x{i.Class:X2}",
                subClass = $"0x{i.SubClass:X2}",
                protocol = $"0x{i.Protocol:X2}",
                endpoints = i.Endpoints.Select(e => $"0x{e.EndpointAddress:X2}").ToList()
            }).ToList()
        });
        Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return;
    }

    foreach (var device in devices)
    {
        var ifClass = FormatByte(device.InterfaceClass);
        var ifSubClass = FormatByte(device.InterfaceSubClass);
        var ifProtocol = FormatByte(device.InterfaceProtocol);
        Console.WriteLine(
            $"api={device.ApiName} kind={device.SourceApiKind} speed={device.Speed} vid=0x{device.VendorId:X4} pid=0x{device.ProductId:X4} if={ifClass}/{ifSubClass}/{ifProtocol} serial={device.SerialNumber ?? "<null>"} path={device.DevicePath}");
    }
}

static string FormatByte(byte? value)
{
    return value.HasValue ? $"0x{value.Value:X2}" : "--";
}

static ushort ParseUShort(string value)
{
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        return ushort.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    if (value.All(Uri.IsHexDigit) && value.Length <= 4)
    {
        return ushort.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    return ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}

static byte ParseByte(string value)
{
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        return byte.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    if (value.All(Uri.IsHexDigit) && value.Length <= 2)
    {
        return byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    return byte.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}

static void ExecuteMonitor(UsbCommunicationLayer layer, string[] args)
{
    UsbApiKind apiKind = UsbApiKind.Auto;
    ushort? vid = null;
    ushort? pid = null;
    byte? ifClass = null, ifSubClass = null, ifProtocol = null;
    double intervalSeconds = 1.0;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--api" when i + 1 < args.Length:
                apiKind = ParseApi(args[++i]);
                continue;
            case "--vid" when i + 1 < args.Length:
                vid = ParseUShort(args[++i]);
                continue;
            case "--pid" when i + 1 < args.Length:
                pid = ParseUShort(args[++i]);
                continue;
            case "--if-class" when i + 1 < args.Length:
                ifClass = ParseByte(args[++i]);
                continue;
            case "--if-subclass" when i + 1 < args.Length:
                ifSubClass = ParseByte(args[++i]);
                continue;
            case "--if-protocol" when i + 1 < args.Length:
                ifProtocol = ParseByte(args[++i]);
                continue;
            case "--interval" when i + 1 < args.Length:
                intervalSeconds = double.Parse(args[++i], CultureInfo.InvariantCulture);
                continue;
            case "-h" or "--help":
                ShowHelp();
                return;
            default:
                throw new ArgumentException($"Unknown argument: {arg}");
        }
    }

    var filter = new UsbDeviceFilter
    {
        VendorId = vid,
        ProductId = pid,
        InterfaceClass = ifClass,
        InterfaceSubClass = ifSubClass,
        InterfaceProtocol = ifProtocol
    };

    using var monitor = layer.MonitorDevices(
        changes =>
        {
            foreach (var change in changes)
            {
                var d = change.Device;
                Console.WriteLine(
                    $"{DateTime.Now:HH:mm:ss.fff} {Symbol(change.Kind)} api={d.ApiName} vid=0x{d.VendorId:X4} pid=0x{d.ProductId:X4} serial={d.SerialNumber ?? "<null>"} path={d.DevicePath}");
            }
        },
        apiKind,
        filter,
        TimeSpan.FromSeconds(intervalSeconds),
        fireInitialSnapshot: true,
        onError: ex => Console.Error.WriteLine($"monitor error: {ex.GetType().Name}: {ex.Message}"));

    Console.WriteLine("Monitoring USB devices (Ctrl+C to stop)...");
    var done = new ManualResetEventSlim(false);
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        done.Set();
    };
    done.Wait();
}

static string Symbol(UsbDeviceChangeKind kind)
{
    return kind switch
    {
        UsbDeviceChangeKind.Added => "+Added",
        UsbDeviceChangeKind.Removed => "-Removed",
        UsbDeviceChangeKind.Changed => "~Changed",
        _ => "?"
    };
}

/// <summary>
/// Read-only device-level smoke test for real hardware: enumerate, open a session,
/// issue a GET_DESCRIPTOR control transfer and a short ReadExact, and report PASS/FAIL.
/// <para>面向真实硬件的只读设备级冒烟测试：枚举、打开会话、发起 GET_DESCRIPTOR 控制传输
/// 与一次短 ReadExact，并报告 PASS/FAIL。</para>
/// Deliberately performs NO write transfers and NO reset, so it is safe to run against
/// devices in fastboot/EDL mode. Exit code 0 with "SKIP" means no matching device.
/// <para>刻意不执行任何写传输与重置，可安全地对处于 fastboot/EDL 模式的设备运行。
/// 输出 "SKIP" 且退出码 0 表示没有匹配设备。</para>
/// </summary>
static void ExecuteSelftest(UsbCommunicationLayer layer, string[] args)
{
    UsbApiKind apiKind = UsbApiKind.Auto;
    ushort? vid = null;
    ushort? pid = null;
    int durationSeconds = 0;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--api" when i + 1 < args.Length:
                apiKind = ParseApi(args[++i]);
                continue;
            case "--vid" when i + 1 < args.Length:
                vid = ParseUShort(args[++i]);
                continue;
            case "--pid" when i + 1 < args.Length:
                pid = ParseUShort(args[++i]);
                continue;
            case "--duration" when i + 1 < args.Length:
                durationSeconds = int.Parse(args[++i], CultureInfo.InvariantCulture);
                continue;
            case "-h" or "--help":
                ShowHelp();
                return;
            default:
                throw new ArgumentException($"Unknown argument: {arg}");
        }
    }

    var filter = new UsbDeviceFilter { VendorId = vid, ProductId = pid };
    int pass = 0;
    int failures = 0;
    var deadline = durationSeconds > 0 ? DateTime.UtcNow.AddSeconds(durationSeconds) : DateTime.MaxValue;

    do
    {
        pass++;
        Console.WriteLine($"--- selftest pass {pass} ---");

        // 1) Enumeration
        var devices = layer.EnumerateDevices(apiKind, filter);
        if (devices.Count == 0)
        {
            Console.WriteLine("SKIP enumeration: no matching device present (attach hardware and retry).");
            Environment.ExitCode = 0;
            return;
        }

        Console.WriteLine($"PASS enumeration: {devices.Count} device(s)");
        foreach (var d in devices)
        {
            Console.WriteLine($"  api={d.ApiName} vid=0x{d.VendorId:X4} pid=0x{d.ProductId:X4} speed={d.Speed} if=0x{d.InterfaceClass:X2}/0x{d.InterfaceSubClass:X2}/0x{d.InterfaceProtocol:X2} serial={d.SerialNumber ?? "<null>"}");
        }

        // 2) Open session
        using var session = layer.OpenDeviceSession(apiKind, filter);
        if (session == null)
        {
            Console.WriteLine("FAIL open session: no session could be opened (device busy? permissions?).");
            failures++;
            continue;
        }

        Console.WriteLine("PASS open session");

        // 3) Control transfer: GET_DESCRIPTOR(DEVICE) - read-only, 18 bytes.
        try
        {
            var setup = new UsbSetupPacket { RequestType = 0x80, Request = 0x06, Value = 0x0100, Index = 0, Length = 18 };
            byte[] buf = new byte[18];
            int n = session.ControlTransfer(setup, buf, 0, buf.Length, 3000);
            if (n >= 18)
            {
                Console.WriteLine($"PASS control transfer (GET_DESCRIPTOR): {n} bytes, bcdUSB=0x{(ushort)(buf[2] | (buf[3] << 8)):X4}, bcdDevice=0x{(ushort)(buf[12] | (buf[13] << 8)):X4}");
            }
            else
            {
                Console.WriteLine($"FAIL control transfer: only {n} bytes returned.");
                failures++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL control transfer: {ex.GetType().Name}: {ex.Message}");
            failures++;
        }

        // 4) ReadExact short read (timeout-safe; a short/zero result is expected unless the
        //    device is streaming data - this exercises the read path without side effects).
        try
        {
            byte[] data = session.ReadExact(16, 1000);
            Console.WriteLine($"INFO ReadExact: {data.Length} bytes received (short/zero is normal for idle devices).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"INFO ReadExact: {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine($"INFO session default timeout: {session.DefaultTimeoutMs} ms");

        if (durationSeconds > 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(1000);
        }
    } while (durationSeconds > 0 && DateTime.UtcNow < deadline);

    Console.WriteLine($"=== selftest result: {pass} pass(es), {failures} failure(s) ===");
    Environment.ExitCode = failures == 0 ? 0 : 1;
}

/// <summary>
/// Runs a write/reset smoke test against a device. Unlike the read-only
/// <c>selftest</c>, this exercises the bulk OUT write path and the device reset -
/// intended for emulated devices (QEMU usb-serial with a file chardev the host
/// can verify) or hardware the user explicitly accepts writes to.
/// <para>对设备执行写入/重置冒烟测试。与只读 <c>selftest</c> 不同，本命令覆盖
/// bulk OUT 写入路径与设备重置——适用于 QEMU 模拟设备（file chardev 时宿主可
/// 校验写入内容）或用户明确接受写入的硬件。</para>
/// </summary>
static void ExecuteIoTest(UsbCommunicationLayer layer, string[] args)
{
    UsbApiKind apiKind = UsbApiKind.Auto;
    ushort? vid = null;
    ushort? pid = null;
    byte? epIn = null;
    byte? epOut = null;
    int patternSize = 64;
    bool concurrent = false;
    bool offsetWrite = false;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--api" when i + 1 < args.Length:
                apiKind = ParseApi(args[++i]);
                continue;
            case "--vid" when i + 1 < args.Length:
                vid = ParseUShort(args[++i]);
                continue;
            case "--pid" when i + 1 < args.Length:
                pid = ParseUShort(args[++i]);
                continue;
            case "--ep-in" when i + 1 < args.Length:
                epIn = ParseByte(args[++i]);
                continue;
            case "--ep-out" when i + 1 < args.Length:
                epOut = ParseByte(args[++i]);
                continue;
            case "--pattern-size" when i + 1 < args.Length:
                patternSize = int.Parse(args[++i], CultureInfo.InvariantCulture);
                continue;
            case "--concurrent":
                concurrent = true;
                continue;
            case "--offset-write":
                offsetWrite = true;
                continue;
            case "-h" or "--help":
                ShowHelp();
                return;
            default:
                throw new ArgumentException($"Unknown argument: {arg}");
        }
    }

    if (patternSize <= 0)
    {
        throw new ArgumentOutOfRangeException(nameof(patternSize), "Pattern size must be positive.");
    }

    var filter = new UsbDeviceFilter { VendorId = vid, ProductId = pid, EndpointAddressIn = epIn, EndpointAddressOut = epOut };
    int failures = 0;

    // 1) Enumeration - same SKIP contract as selftest.
    var devices = layer.EnumerateDevices(apiKind, filter);
    if (devices.Count == 0)
    {
        Console.WriteLine("SKIP enumeration: no matching device present (attach hardware and retry).");
        Environment.ExitCode = 0;
        return;
    }

    Console.WriteLine($"PASS enumeration: {devices.Count} device(s)");

    // 2) Open session
    using var session = layer.OpenDeviceSession(apiKind, filter);
    if (session == null)
    {
        Console.WriteLine("FAIL open session: no session could be opened (device busy? permissions?).");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine("PASS open session");

    // 2b) Report the endpoints actually bound. When an explicit pair was requested,
    //     verify the session honored it (QEMU usb-serial exposes 0x81/0x01).
    Console.WriteLine($"PASS endpoint-open 0x{session.EndpointIn:X2}/0x{session.EndpointOut:X2}");
    if ((epIn.HasValue && session.EndpointIn != epIn.Value) ||
        (epOut.HasValue && session.EndpointOut != epOut.Value))
    {
        Console.WriteLine($"FAIL endpoint-open: requested 0x{epIn ?? 0:X2}/0x{epOut ?? 0:X2} but session bound 0x{session.EndpointIn:X2}/0x{session.EndpointOut:X2}.");
        failures++;
    }

    // 2c) Full-duplex observation (T4): a reader thread must not stall the writer thread.
    if (concurrent)
    {
        try
        {
            using var readerCts = new CancellationTokenSource(2000);
            var readerTask = Task.Run(() =>
            {
                var buf = new byte[16];
                _ = session.ReadPacket(buf, 0, buf.Length, 500);
            }, readerCts.Token);
            Thread.Sleep(100);
            long written = session.Write(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 4, 2000);
            Console.WriteLine(written == 4 ? "PASS concurrent-io: write completed while read in flight" : $"FAIL concurrent-io: write got {written}.");
            if (written != 4) failures++;
            readerCts.Cancel();
            _ = readerTask.Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL concurrent-io: {ex.GetType().Name}: {ex.Message}");
            failures++;
        }
    }

    // 3) Write a deterministic pattern. On QEMU's file chardev the bytes land on the
    //    host side, so the workflow can verify the write really reached the device.
    try
    {
        byte[] pattern = new byte[patternSize];
        for (var i = 0; i < pattern.Length; i++)
        {
            pattern[i] = (byte)(i & 0x3F);
        }

        long written = offsetWrite
            ? session.Write(pattern, 8, pattern.Length - 8, 3000) // offset write (T5): start at byte 8
            : session.Write(pattern, pattern.Length, 3000);
        if (written == pattern.Length - (offsetWrite ? 8 : 0))
        {
            Console.WriteLine($"PASS write: {written} bytes pattern transferred" + (offsetWrite ? " via offset overload" : ""));
        }
        else
        {
            Console.WriteLine($"FAIL write: expected {pattern.Length - (offsetWrite ? 8 : 0)} bytes, got {written}.");
            failures++;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL write: {ex.GetType().Name}: {ex.Message}");
        failures++;
    }

    // 4) Short read - informational only: QEMU's file/null chardev never returns data,
    //    a read error is not treated as a failure here.
    try
    {
        byte[] data = session.ReadExact(16, 1000);
        Console.WriteLine($"INFO read: {data.Length} bytes received (short/zero is normal for idle devices).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"INFO read: {ex.GetType().Name}: {ex.Message}");
    }

    // 5) Reset - the key "beyond read" operation. On QEMU the emulated device
    //    re-enumerates; we only verify the reset call itself succeeds.
    try
    {
        session.Reset();
        Console.WriteLine("PASS reset");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL reset: {ex.GetType().Name}: {ex.Message}");
        failures++;
    }

    Console.WriteLine($"=== io-test result: {(failures == 0 ? "PASS" : "FAIL")}, {failures} failure(s) ===");
    Environment.ExitCode = failures == 0 ? 0 : 1;
}

/// <summary>
/// Runs a read-only interrupt endpoint smoke test against a device (T7): enumerates,
/// opens a session and performs one short-timeout interrupt IN read on the requested
/// endpoint. On emulated HID devices (QEMU usb-tablet) the interrupt endpoint never
/// delivers data, so a zero-byte timeout result is the expected PASS outcome.
/// <para>对设备执行只读中断端点冒烟测试（T7）：枚举、打开会话并在请求的端点上执行一次
/// 短超时中断 IN 读取。对模拟 HID 设备（QEMU usb-tablet），中断端点不会投递数据，
/// 因此零字节超时结果是预期的 PASS 结果。</para>
/// Exit code 0 with "SKIP" means no matching device.
/// <para>输出 "SKIP" 且退出码 0 表示没有匹配设备。</para>
/// </summary>
static void ExecuteInterruptTest(UsbCommunicationLayer layer, string[] args)
{
    UsbApiKind apiKind = UsbApiKind.Auto;
    ushort? vid = null;
    ushort? pid = null;
    byte endpoint = 0x81;
    int timeoutMs = 500;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--api" when i + 1 < args.Length:
                apiKind = ParseApi(args[++i]);
                continue;
            case "--vid" when i + 1 < args.Length:
                vid = ParseUShort(args[++i]);
                continue;
            case "--pid" when i + 1 < args.Length:
                pid = ParseUShort(args[++i]);
                continue;
            case "--ep" when i + 1 < args.Length:
                endpoint = ParseByte(args[++i]);
                continue;
            case "--timeout" when i + 1 < args.Length:
                timeoutMs = int.Parse(args[++i], CultureInfo.InvariantCulture);
                continue;
            case "-h" or "--help":
                ShowHelp();
                return;
            default:
                throw new ArgumentException($"Unknown argument: {arg}");
        }
    }

    // The endpoint address must flow into the filter so IN-only devices (HID with no
    // OUT pipe) match via the finder's explicit-endpoint path.
    var filter = new UsbDeviceFilter { VendorId = vid, ProductId = pid, EndpointAddressIn = endpoint, EndpointAddressOut = null };
    var devices = layer.EnumerateDevices(apiKind, filter);
    if (devices.Count == 0)
    {
        Console.WriteLine("SKIP enumeration: no matching device present (attach hardware and retry).");
        Environment.ExitCode = 0;
        return;
    }

    Console.WriteLine($"PASS enumeration: {devices.Count} device(s)");

    using var session = layer.OpenDeviceSession(apiKind, filter);
    if (session == null)
    {
        Console.WriteLine("FAIL open session: no session could be opened (device busy? permissions?).");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine("PASS open session");

    try
    {
        var buffer = new byte[8];
        var result = session.ReadInterrupt(endpoint, buffer, 0, buffer.Length, timeoutMs);
        if (result.IsTimeout && result.Count == 0)
        {
            // Emulated HID / idle device: no data within the deadline is the expected outcome.
            Console.WriteLine($"PASS interrupt-read timeout count=0 (endpoint 0x{endpoint:X2})");
        }
        else if (result.Count > 0)
        {
            Console.WriteLine($"PASS interrupt-read {result.Count} bytes (endpoint 0x{endpoint:X2})");
        }
        else
        {
            Console.WriteLine($"FAIL interrupt-read: unexpected outcome count={result.Count} timeout={result.IsTimeout}.");
            Environment.ExitCode = 1;
            return;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL interrupt-read: {ex.GetType().Name}: {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine("=== interrupt-test result: PASS ===");
    Environment.ExitCode = 0;
}

static UsbApiKind ParseApi(string value)
{
    return value.ToLowerInvariant() switch
    {
        "native" => UsbApiKind.Native,
        "libusb" => UsbApiKind.LibUsbDotNet,
        "auto" => UsbApiKind.Auto,
        _ => throw new ArgumentException($"Unknown api: {value}")
    };
}

static void ShowHelp()
{
    Console.WriteLine("FirmwareKit.Comm.CLI");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  apis");
    Console.WriteLine("    List all registered USB APIs.");
    Console.WriteLine();
    Console.WriteLine("  devices [--api auto|native|libusb] [--vid <hex>] [--pid <hex>] [--serial <text>] [--path-contains <text>] [--json]");
    Console.WriteLine("    List devices discovered from the selected API and filter set.");
    Console.WriteLine();
    Console.WriteLine("  all-devices [--api native|libusb|auto] [--json]");
    Console.WriteLine("    List all USB devices discovered on the current platform.");
    Console.WriteLine();
    Console.WriteLine("  monitor [--api auto|native|libusb] [--vid <hex>] [--pid <hex>] [--if-class <hex>] [--if-subclass <hex>] [--if-protocol <hex>] [--interval <seconds>]");
    Console.WriteLine("    Print USB device change events (Added/Removed/Changed) until Ctrl+C.");
    Console.WriteLine();
    Console.WriteLine("  selftest [--api auto|native|libusb] [--vid <hex>] [--pid <hex>] [--duration <seconds>]");
    Console.WriteLine("    Read-only device smoke test for real hardware: enumerate, open a session,");
    Console.WriteLine("    GET_DESCRIPTOR control transfer and a short ReadExact (no writes/reset).");
    Console.WriteLine("    Exit 0 with 'SKIP' means no matching device is attached.");
    Console.WriteLine();
    Console.WriteLine("  io-test [--api auto|native|libusb] [--vid <hex>] [--pid <hex>] [--ep-in <hex>] [--ep-out <hex>] [--pattern-size <bytes>] [--concurrent] [--offset-write]");
    Console.WriteLine("    Write/reset smoke test for emulated or accepted hardware: enumerate, open a");
    Console.WriteLine("    session, write a deterministic pattern, short read and reset the device.");
    Console.WriteLine("    --ep-in/--ep-out verify the session bound the requested endpoints;");
    Console.WriteLine("    --concurrent checks a blocked read does not stall a write (full-duplex);");
    Console.WriteLine("    --offset-write exercises the offset-aware Write overload.");
    Console.WriteLine("    Exit 0 with 'SKIP' means no matching device is attached.");
    Console.WriteLine();
    Console.WriteLine("  interrupt-test [--api auto|native|libusb] [--vid <hex>] [--pid <hex>] [--ep <hex>] [--timeout <ms>]");
    Console.WriteLine("    Read-only interrupt IN endpoint smoke test: open a session and perform one");
    Console.WriteLine("    short-timeout interrupt read. On idle/emulated HID devices a zero-byte");
    Console.WriteLine("    timeout result is the expected PASS outcome.");
    Console.WriteLine("    Exit 0 with 'SKIP' means no matching device is attached.");
    Console.WriteLine();
    Console.WriteLine("devices filters:");
    Console.WriteLine("  --if-class <hex|dec>");
    Console.WriteLine("  --if-subclass <hex|dec>");
    Console.WriteLine("  --if-protocol <hex|dec>");
}
