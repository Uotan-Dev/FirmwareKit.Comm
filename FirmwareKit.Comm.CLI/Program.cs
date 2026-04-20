using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Core;
using System.Globalization;

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

    default:
        ShowHelp();
        break;
}

static void ExecuteDevices(UsbCommunicationLayer layer, string[] args)
{
    UsbApiKind apiKind = UsbApiKind.Auto;
    var filter = new UsbDeviceFilter();

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
    PrintDevices(devices);

    if (devices.Count == 0)
    {
        Console.Error.WriteLine("No devices matched the selected API and filters.");
    }
}

static void ExecuteAllDevices(UsbCommunicationLayer layer, string[] args)
{
    // Default to current platform backend; caller can override with --api.
    UsbApiKind apiKind = UsbApiKind.Native;

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

        throw new ArgumentException($"Unknown argument: {arg}");
    }

    var devices = layer.EnumerateDevices(apiKind, filter: null);
    PrintDevices(devices);

    if (devices.Count == 0)
    {
        Console.Error.WriteLine("No USB devices were discovered for the selected API on this platform.");
    }
}

static void PrintDevices(IReadOnlyList<UsbDeviceInfo> devices)
{
    foreach (var device in devices)
    {
        var ifClass = FormatByte(device.InterfaceClass);
        var ifSubClass = FormatByte(device.InterfaceSubClass);
        var ifProtocol = FormatByte(device.InterfaceProtocol);
        Console.WriteLine(
            $"api={device.ApiName} kind={device.SourceApiKind} vid=0x{device.VendorId:X4} pid=0x{device.ProductId:X4} if={ifClass}/{ifSubClass}/{ifProtocol} serial={device.SerialNumber ?? "<null>"} path={device.DevicePath}");
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

static void ShowHelp()
{
    Console.WriteLine("FirmwareKit.Comm.CLI");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  apis");
    Console.WriteLine("    List all registered USB APIs.");
    Console.WriteLine();
    Console.WriteLine("  devices [--api auto|native|libusb] [--vid <hex>] [--pid <hex>] [--serial <text>] [--path-contains <text>]");
    Console.WriteLine("    List devices discovered from the selected API and filter set.");
    Console.WriteLine();
    Console.WriteLine("  all-devices [--api native|libusb|auto]");
    Console.WriteLine("    List all USB devices discovered on the current platform.");
    Console.WriteLine();
    Console.WriteLine("devices filters:");
    Console.WriteLine("  --if-class <hex|dec>");
    Console.WriteLine("  --if-subclass <hex|dec>");
    Console.WriteLine("  --if-protocol <hex|dec>");
}
