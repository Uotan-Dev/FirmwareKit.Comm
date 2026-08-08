using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Core;
using System.Globalization;
using System.Text.Json;

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

    if (devices.Count == 0)
    {
        Console.Error.WriteLine("No USB devices were discovered for the selected API on this platform.");
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
    Console.WriteLine("devices filters:");
    Console.WriteLine("  --if-class <hex|dec>");
    Console.WriteLine("  --if-subclass <hex|dec>");
    Console.WriteLine("  --if-protocol <hex|dec>");
}
