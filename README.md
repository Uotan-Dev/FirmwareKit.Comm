# FirmwareKit.Comm

[![NuGet version](https://img.shields.io/nuget/v/FirmwareKit.Comm.svg)](https://www.nuget.org/packages/FirmwareKit.Comm)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

English | [简体中文](README.zh-CN.md)

A cross-platform USB communication library for FirmwareKit. It provides a unified USB abstraction over native platform backends (Windows / Linux / macOS / HarmonyOS) and LibUsbDotNet, with device discovery, filtering, session management, and structured transfer diagnostics. **Transport primitives only** — protocol layers (Sahara, Firehose, Fastboot, and so on) are out of scope and are implemented by callers on top of the unified session interfaces.

## Features

- **Unified session API**: synchronous (`IUsbDeviceSession`) and asynchronous (`IAsyncUsbDeviceSession`) read/write/control-transfer over one abstraction, with per-direction serialization for full-duplex protocol threads.
- **Four native backends + libusb**: Windows WinUSB (and legacy driver), Linux usbfs, macOS IOUSBHost.framework, HarmonyOS USBManager DDK, and LibUsbDotNet.
- **Device discovery & filtering**: by `VendorId`, `ProductId`, `SerialNumber`, `DevicePath`, interface class/subclass/protocol, interface number(s), and explicit endpoint addresses.
- **Packet semantics**: `ReadPacket` / `ReadPacketAsync` return a `UsbReadResult` that distinguishes a short packet (USB message boundary) from a timeout — what fastboot/EDL/bootrom framing needs. `ReadExact` / `ReadExactAsync` read a fixed length within a total deadline.
- **Zero-length packet (ZLP) control**: `WriteZlp` / `WriteZlpAsync` terminate transfers whose payload is an exact multiple of the endpoint max packet size.
- **Progress reporting**: `ReadPacketAsync` / `WriteAsync` overloads accept `IProgress<long>` and report cumulative bytes after each chunk (flashing large images).
- **Safety guardrails**: a read-length cap (`UsbTransferPolicies.MaxReadLength`) prevents OOM from untrusted protocol lengths; session disposal is idempotent; `ReadInto(Span<byte>)` is available on net8.0+.
- **Observable enumeration diagnostics**: `UsbCommunicationLayer.GetEnumerationDiagnostics()` and per-finder counters distinguish "mechanism did not run" from "ran but found no devices" on device-less CI.
- **Mode-switch workflows**: `WaitForUsbDeviceAppearAsync` / `WaitForUsbDeviceDisappearAsync` / `WaitForUsbDeviceModeSwitchAsync`, plus re-opening by `DeviceKey` via `OpenDeviceSessionByKey`.
- **Hot-plug monitoring**: event-driven where available (`WM_DEVICECHANGE` on Windows native, libusb hotplug), polling fallback otherwise; `Added` / `Removed` / `Changed` change events.
- **Backend capability model**: `UsbApiCapabilities` (and per-backend `UsbBackendCapability`) exposes async support, hot-plug support, external-runtime requirements, and `ResetReenumeratesDevice`.
- **Diagnostics**: `UsbTrace` structured transfer events (`TransferObserved`) and opt-in frame capture; `LogFormatted` defers string interpolation until logging is enabled.
- **Extensibility**: register custom providers via `RegisterUsbApi` / `UsbApiRegistry`.
- **Built-in CLI**: `FirmwareKit.Comm.CLI` with `apis`, `devices`, `all-devices`, `monitor`, `selftest`, `io-test`, and `interrupt-test` commands.

## Design Boundary

FirmwareKit.Comm focuses on cross-platform USB transport primitives:

- Device discovery and filtering
- Session management (open / read / write / control / interrupt / reset)
- Unified read/write with timeout control
- Transport-level reset

Discovery prefers metadata-first paths so simple enumeration does not require long-lived read/write sessions. Actual payload I/O starts after calling `OpenUsbDeviceSessions`.

Backend matrix:

| Backend | Platform | Transport | Notes |
|---------|----------|-----------|-------|
| `native` (WinUSB) | Windows | WinUSB API | Overlapped (true async) I/O; requires a WinUSB-bound interface (Zadig etc.) |
| `native` (legacy) | Windows | DeviceIoControl | Fallback for legacy USB drivers; no ZLP, no native async |
| `native` (usbfs) | Linux | usbfs ioctl / URB | True async via URB + poll; multi-interface claim supported |
| `native` (IOUSBLib) | macOS | IOUSBHost.framework | Requires macOS 10.15+; pipe-level reset |
| `native` (HarmonyOS) | HarmonyOS | USBManager DDK | Opt-in via `FIRMWAREKIT_USB_ENABLE_HARMONY=1`; requires `OH_Usb_Init()` to succeed |
| `libusb` | all | LibUsbDotNet | Native async transfers; needs the native libusb runtime (bundled per-RID in the package); degrades gracefully when absent |

HarmonyOS is hidden from `GetAvailableApis()` by default because it cannot be detected reliably with file probes. On macOS below 10.15, use the `libusb` backend.

## Transfer Timeout Semantics

- The timeout passed to `Read` / `Write` (and their `ReadInto` variants) applies **per chunk**, not to the whole operation: a transfer larger than the backend chunk size is split into multiple chunks, so the total time can reach `chunkCount × timeoutMs`.
- When an exact number of bytes must be read within a total budget (e.g. fixed-size fastboot/EDL responses), use `ReadExact(length, timeoutMs)` / `ReadExactAsync` — they loop over short reads with a total deadline and return the bytes actually received on timeout.
- Short reads/writes stop the transfer and return the partial byte count; a disconnected device throws `UsbDeviceDisconnectedException` (distinct from ordinary `IOException`/`UsbTransferException`).

**Default timeouts differ per backend.** Omit `timeoutMs` only when the default is acceptable for your use case:

| Backend | Default timeout |
|---------|-----------------|
| Windows WinUSB | 60 000 ms |
| Windows legacy / Linux usbfs / libusb / macOS / HarmonyOS | 5 000 ms |

Pass an explicit `timeoutMs` (or `UsbTransferPolicies.InfiniteTimeoutMs` = -1 for an unbounded wait) when the operation must not depend on the backend default.

The async extension overloads that omit the timeout (`ReadAsync(length)`, `ReadIntoAsync(...)`, `ReadPacketAsync(...)`, `WriteAsync(...)`, `WriteZlpAsync()`, `ControlTransferAsync(...)`, `ReadInterruptAsync(...)`, `WriteInterruptAsync(...)`) use the session's `DefaultTimeoutMs`.

On the Linux usbfs backend, interrupt-endpoint reads/writes (`ReadInterrupt`/`WriteInterrupt`) wait on `poll()` from a thread-pool thread; with `InfiniteTimeoutMs` and an unresponsive device the waiting thread is held until the device responds or disconnects.

Retries for recoverable errors are configurable process-wide via `UsbTransferPolicies.DefaultRetryPolicy` (a `UsbTransferRetryPolicy` with `MaxRetries` and `RetryDelayMs`).

## Zero-Length Packet (ZLP) Handling

Bulk transfers whose payload length is an **exact multiple** of the endpoint's max packet size (typically 512 or 1024 bytes) must be terminated with a zero-length packet so the device knows the transfer ended. This matters for protocol downloads (adb push, fastboot `download:`, EDL firehose) and for bootrom loaders.

- Check whether a ZLP is needed: `payloadLength % maxPacketSize == 0` (read `MaxPacketSize` from the device's `Interfaces[i].Endpoints` metadata).
- After such a write, call `session.WriteZlp(timeoutMs)` (or `WriteZlpAsync`) to send the terminating zero-length packet.
- Backends that cannot perform an explicit ZLP write (legacy Windows drivers) throw `NotSupportedException`; check `UsbApiCapabilities` / backend capabilities first.

```csharp
// Example: writing a 512 KiB block to a device whose OUT max packet size is 512.
long written = session.Write(block, 0, block.Length, timeoutMs);
if (block.Length % 512 == 0)
    session.WriteZlp(timeoutMs);
```

## Reset Semantics

`IUsbDeviceSession.Reset()` semantics differ per backend. Query `UsbApiCapabilities.ResetReenumeratesDevice` (or `UsbBackendCapability.ResetReenumeratesDevice` for the concrete backend) before relying on the session after a reset:

| Backend | Reset effect | Session after Reset | `ResetReenumeratesDevice` |
|---------|-------------|---------------------|---------------------------|
| Windows WinUSB | `WinUsb_ResetPipe` (pipe-level) | still usable | `false` |
| Windows legacy | no-op | still usable | `false` |
| macOS IOUSBHost | pipe abort + clear stall | still usable | `false` |
| Linux usbfs | `USBDEVFS_RESET` (device reset) | **invalid — re-enumerate and re-open** | `true` |
| libusb | `libusb_reset_device` (device reset) | **invalid — re-enumerate and re-open** | `true` |
| HarmonyOS DDK | re-init DDK session + re-claim | **invalid — re-open** | `true` |

For device-level resets, use `WaitForUsbDeviceDisappearAsync` / `WaitForUsbDeviceAppearAsync` (or `WaitForUsbDeviceModeSwitchAsync`) and then `OpenUsbDeviceSession` / `OpenUsbDeviceSessionByKey` to obtain a fresh session.

## Async I/O Semantics

True asynchronous (non-blocking) I/O is implemented natively by **WinUSB** (overlapped I/O), **Linux usbfs** (URB + poll) and **libusb** (LibUsbDotNet async transfers). All other backends — macOS IOUSBHost, HarmonyOS DDK, and the `AsAsync()` adapter — execute the underlying synchronous transfer on a thread-pool thread (`UsbAsyncExecution.Run`).

Check `UsbApiCapabilities.SupportsNativeAsyncIo` (or `UsbBackendCapability.SupportsNativeAsyncIo` per backend) to know whether `ReadAsync`/`WriteAsync` are truly non-blocking or just offloaded. Protocol layers that must not block the caller's thread should treat `SupportsNativeAsyncIo == false` backends as synchronous-with-offload.

## Enumeration Diagnostics (device-less CI)

When no device is attached (hosted CI runners), an empty enumeration can mean either "no devices" or "the enumeration mechanism failed". Each finder exposes observability state, and `UsbCommunicationLayer.GetEnumerationDiagnostics()` summarizes it in a `key=value; ...` string:

| Backend | Mechanism diagnostic | Scan proof | Counters |
|---------|----------------------|------------|----------|
| Linux usbfs | `LastUsbfsRootExists` (usbfs mounted?) | `LastScannedNodes` | `LastMatchedDeviceCount`, `PermissionDeniedCount`, `BusyCount` |
| Windows | `LastSetupDiSucceeded` (SetupDi handle opened?) | `LastScannedNodeCount` | `LastMatchedDeviceCount` |
| macOS | `LastCopyDevicesSucceeded` (IOUSBLib copy returned?) | `LastScannedDeviceCount` | `LastMatchedDeviceCount` |
| libusb | `IsRuntimeAvailable(out reason)` | device list | device list |

The CLI prints `[enum-diagnostics] <summary>` on every `all-devices` run; CI asserts on it instead of only checking "exit 0". The library also ships unit tests that feed constructed USB descriptor bytes into the pure parser (`LinuxUsbFinder.TryParseDescriptor`) to verify enumeration correctness without hardware.

## Hot-Plug Monitoring

`MonitorUsbDevices` prefers event-driven notifications where available: `WM_DEVICECHANGE` (Windows native backend) and libusb hotplug (Linux/macOS, `UsbApiKind.LibUsbDotNet`); unsupported platforms fall back to polling (`pollInterval`, default 1 s). Change events include `Added`, `Removed` and `Changed` (metadata changed while keeping the same physical identity). Pass a `CancellationToken` to auto-dispose the monitor handle, or use the `WaitForUsbDeviceAppearAsync` / `WaitForUsbDeviceDisappearAsync` / `WaitForUsbDeviceModeSwitchAsync` helpers for mode-switch workflows.

## Installation

Install via NuGet:

```powershell
dotnet add package FirmwareKit.Comm
```

The package targets `net10.0`, `net8.0` and `netstandard2.0`, and bundles the native libusb runtime for each supported RID (win-x64 / win-arm64 / osx-x64 / osx-arm64 / linux-x64 / linux-arm64 / linux-riscv64 / linux-loong64). Requires the **.NET 10 SDK** to build the solution (`.slnx`).

## Quick Start

Use the `FirmwareKitComm` facade to enumerate APIs and devices:

```csharp
using FirmwareKit.Comm;
using FirmwareKit.Comm.Abstractions;

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

// Multi-interface claim (e.g. CDC-ACM control + data): every listed interface must exist
var multiIf = comm.EnumerateUsbDevices(UsbApiKind.Auto, new UsbDeviceFilter
{
    InterfaceNumber = 0,               // primary data interface
    InterfaceNumbers = new byte[] { 1 } // additional interface to claim (Linux/libusb)
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

    // Packet-aware read: short packet vs timeout (fastboot/EDL message boundary)
    var buf = new byte[512];
    var result = session.ReadPacket(buf, 0, buf.Length, 3000);
    Console.WriteLine($"packet bytes={result.Count} timeout={result.IsTimeout} short={result.IsShortPacket}");

    // Progress reporting while flashing a large image
    var progress = new Progress<long>(total => Console.WriteLine($"written: {total}"));
    await asyncSession.WriteAsync(image, 0, image.Length, 3000, progress);
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
- `all-devices`: list all USB devices recognized by the current platform (native backend by default); prints `[enum-diagnostics]` even when empty.
- `monitor`: run a device-change monitor (options: `--api`, `--vid`, `--pid`, `--interval <seconds>`).
- `selftest`: read-only smoke test — open session, GET_DESCRIPTOR control transfer, short `ReadExact` (no writes/resets); `--duration <seconds>` loops it (0 = single pass).
- `io-test`: write/reset smoke test (bulk OUT write, reset, endpoint-open, concurrent-io, offset-write); options include `--ep-in`, `--ep-out`, `--pattern-size`, `--concurrent`, `--offset-write`.
- `interrupt-test`: interrupt-IN endpoint read test (options: `--ep <addr>`).

Examples:

```powershell
# List APIs
dotnet run --project FirmwareKit.Comm.CLI -- apis

# List devices (libusb backend, filtered by VID/PID)
dotnet run --project FirmwareKit.Comm.CLI -- devices --api libusb --vid 0x18D1 --pid 0x4E11

# List all USB devices recognized by current platform (JSON output)
dotnet run --project FirmwareKit.Comm.CLI -- all-devices --json

# Read-only selftest against a specific device
dotnet run --project FirmwareKit.Comm.CLI -- selftest --api native --vid 0x18D1 --duration 10
```

Supported `devices` / `all-devices` options:

- `--api auto|native|libusb`: select backend API.
- `--vid <hex>`: vendor ID (hex or decimal).
- `--pid <hex>`: product ID (hex or decimal).
- `--serial <text>`: device serial number.
- `--path-contains <text>`: substring filter on device path.
- `--if-class <hex|dec>`: interface class filter.
- `--if-subclass <hex|dec>`: interface subclass filter.
- `--if-protocol <hex|dec>`: interface protocol filter.
- `--json`: JSON output (devices / all-devices).

Diagnostics are opt-in via environment variables: `FIRMWAREKIT_USB_DEBUG=1` (plain-text logs), `FIRMWAREKIT_USB_CAPTURE_FRAMES=1` (raw frame capture into transfer events, capped at 256 bytes per event).

## Build, Test and CI

```bash
dotnet restore
dotnet build -c Release      # CI validates Release
dotnet test  -c Release      # xunit v3; empty enumeration is the expected CI outcome
```

- The solution is `.slnx` and requires the .NET 10 SDK.
- `GeneratePackageOnBuild=true` emits nupkg/snupkg on every build — never treat `bin/`/`obj/` artifacts as sources of truth.
- `tools/fetch-libusb.sh` downloads native libusb runtimes into `native/<target>/` (mirror flags: `--mirror tuna|ustc`, `--github-mirror`, `--ghcr-mirror`).
- `tools/test-windows.ps1` / `tools/test-linux.sh` / `tools/test-macos.sh` are real-device automated test scripts (build + unit tests + CLI smoke + read-only selftest + 3 s monitor hot-plug smoke); they exit 0 with `SKIP` when no device matches.
- CI workflows: `dotnet-ci.yml` (build/test/behavior observation on win/ubuntu/macos, including enumeration-mechanism assertions), `cross-publish.yml` (RID publish graph check), `fetch-libusb.yml` (native runtime fetch), `qemu-linux-guest.yml` (real emulated USB inside a QEMU Linux guest).

## License

MIT
