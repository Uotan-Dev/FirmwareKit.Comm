# FirmwareKit.Comm

[![NuGet version](https://img.shields.io/nuget/v/FirmwareKit.Comm.svg)](https://www.nuget.org/packages/FirmwareKit.Comm)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

English | [简体中文](README.zh-CN.md)

A cross-platform USB communication library for FirmwareKit that provides a unified USB abstraction, device session management, and multiple backend implementations (Windows / Linux / macOS / LibUsbDotNet).

## Features

- Cross-platform USB abstraction: supports native platform backends and `LibUsbDotNet`.
- Device discovery and filtering by `VendorId`, `ProductId`, `SerialNumber`, `DevicePath`, and more.
- Optional interface-level filtering by `InterfaceClass`, `InterfaceSubClass`, and `InterfaceProtocol`.
- Async session support via `IAsyncUsbDeviceSession`, plus `AsAsync()` adapter for custom sync sessions.
- Traceable interface metadata origin through `UsbDeviceInfo.InterfaceMetadataObserved`.
- Device change monitoring with `MonitorUsbDevices` / `MonitorDevices`, with `onError` hook for monitor-stage exceptions.
- Backend capability snapshot through `GetAvailableUsbApiCapabilities`.
- Unified control transfer APIs: `ControlTransfer` / `ControlTransferAsync`.
- Structured transfer diagnostics through `UsbTrace.TransferObserved`.
- Extensible provider registration with `RegisterUsbApi`.
- Simple facade API: `IFirmwareKitComm` / `FirmwareKitComm`.
- Lightweight built-in CLI in `FirmwareKit.Comm.CLI` (`apis`, `devices`, `all-devices`).

## Design Boundary

FirmwareKit.Comm focuses on cross-platform native USB transport primitives:

- Device discovery and filtering
- Session management
- Unified read/write with timeout control
- Transport-level reset

By default, discovery prefers metadata-first paths so simple enumeration does not require long-lived read/write sessions.
Actual payload I/O starts after calling `OpenUsbDeviceSessions`.

Protocol layers (for example Sahara, Firehose, Fastboot, or custom binary protocols) are intentionally out of scope and should be implemented by callers on top of the unified session interfaces.

## Installation

Install via NuGet:

```powershell
dotnet add package FirmwareKit.Comm
```

## Quick Start

Use the `FirmwareKitComm` facade to enumerate APIs and devices:

```csharp
using FirmwareKit.Comm;
using FirmwareKit.Comm.Usb;

var comm = new FirmwareKitComm();

// List registered USB APIs
foreach (var api in comm.GetAvailableUsbApis())
    Console.WriteLine(api);

// Print backend capability summary
foreach (var capability in comm.GetAvailableUsbApiCapabilities())
{
    Console.WriteLine($"api={capability.ApiName} nativeDiscovery={capability.SupportsNativeDiscovery} nativeAsync={capability.SupportsNativeAsyncIo} hotplug={capability.SupportsNativeHotPlugMonitoring} externalRuntime={capability.RequiresExternalRuntime}");
}

// Sync device enumeration with VendorId filter (example: 0x18D1)
var devices = comm.EnumerateUsbDevices(UsbApiKind.Auto, new UsbDeviceFilter { VendorId = 0x18D1 });
foreach (var d in devices)
{
    var ifClass = d.InterfaceClass.HasValue ? $"0x{d.InterfaceClass.Value:X2}" : "--";
    var ifSubClass = d.InterfaceSubClass.HasValue ? $"0x{d.InterfaceSubClass.Value:X2}" : "--";
    var ifProto = d.InterfaceProtocol.HasValue ? $"0x{d.InterfaceProtocol.Value:X2}" : "--";
    Console.WriteLine($"api={d.ApiName} vid=0x{d.VendorId:X4} pid=0x{d.ProductId:X4} if={ifClass}/{ifSubClass}/{ifProto} serial={d.SerialNumber ?? "<null>"} path={d.DevicePath}");
}

// Optional: filter by USB interface class (for example Qualcomm EDL often uses 0xFF/0xFF/0xFF)
var edlLikeDevices = comm.EnumerateUsbDevices(UsbApiKind.Auto, new UsbDeviceFilter
{
    VendorId = 0x05C6,
    InterfaceClass = 0xFF,
    InterfaceSubClass = 0xFF,
    InterfaceProtocol = 0xFF
});

// Async enumeration
var asyncDevices = await comm.EnumerateUsbDevicesAsync(UsbApiKind.LibUsbDotNet);

// Open sessions and do unified read/write (protocol parsing is caller-defined)
using var sessions = comm.OpenUsbDeviceSessions(UsbApiKind.Auto, new UsbDeviceFilter
{
    VendorId = 0x05C6,
    ProductId = 0x9008,
    InterfaceClass = 0xFF,
    InterfaceSubClass = 0xFF,
    InterfaceProtocol = 0xFF
});

var session = sessions.Sessions.FirstOrDefault();
if (session != null)
{
    // Example only: command/protocol payload is app-specific
    _ = session.Write(new byte[] { 0x7E, 0x00 }, 2, 3000);
    var response = session.Read(512, 3000);
    Console.WriteLine($"response bytes: {response.Length}");

    // Control transfer example: read current alternate setting
    var setup = new UsbSetupPacket
    {
        RequestType = 0x81,
        Request = 0x0A,
        Value = 0,
        Index = 0,
        Length = 1
    };
    var ctrlBuffer = new byte[1];
    var ctrlCount = session.ControlTransfer(setup, ctrlBuffer, 0, ctrlBuffer.Length, 3000);
    Console.WriteLine($"control bytes: {ctrlCount}, alt={ctrlBuffer[0]}");

    // Async session (if backend does not implement async natively, use AsAsync())
    var asyncSession = session.AsAsync();
    var asyncResponse = await asyncSession.ReadAsync(512, 3000);
    Console.WriteLine($"async response bytes: {asyncResponse.Length}");
}

// Device change monitoring (dispose when appropriate)
using var monitor = comm.MonitorUsbDevices(
    changes =>
    {
        foreach (var change in changes)
        {
            Console.WriteLine($"device {change.Kind}: {change.Device.ApiName} {change.Device.DevicePath}");
        }
    },
    UsbApiKind.Auto,
    pollInterval: TimeSpan.FromSeconds(1),
    fireInitialSnapshot: false,
    onError: ex => Console.WriteLine($"monitor error: {ex.Message}"));

// Structured diagnostics event (for metrics/log aggregation)
UsbTrace.TransferObserved += evt =>
{
    Console.WriteLine($"usb {evt.Operation} backend={evt.Backend} outcome={evt.Outcome} bytes={evt.TransferredBytes}/{evt.RequestedBytes} retry={evt.RetryCount} err={evt.NativeErrorCode}");
};
```

Register a custom USB API provider:

```csharp
comm.RegisterUsbApi("my-custom", () => new MyCustomUsbApiProvider());
```

## CLI

`FirmwareKit.Comm.CLI` provides these commands:

- `apis`: list available USB APIs.
- `devices`: enumerate devices with optional filters.
- `all-devices`: list all USB devices recognized by the current platform (native backend by default).

Examples:

```powershell
# List APIs
dotnet run --project FirmwareKit.Comm.CLI -- apis

# List devices (libusb backend, filtered by VID/PID)
dotnet run --project FirmwareKit.Comm.CLI -- devices --api libusb --vid 0x18D1 --pid 0x4E11

# List all USB devices recognized by current platform
dotnet run --project FirmwareKit.Comm.CLI -- all-devices
```

Supported `devices` options:

- `--api auto|native|libusb`: select backend API.
- `--vid <hex>`: vendor ID (hex or decimal).
- `--pid <hex>`: product ID (hex or decimal).
- `--serial <text>`: device serial number.
- `--path-contains <text>`: substring filter on device path.
- `--if-class <hex|dec>`: interface class filter.
- `--if-subclass <hex|dec>`: interface subclass filter.
- `--if-protocol <hex|dec>`: interface protocol filter.

## License

MIT
