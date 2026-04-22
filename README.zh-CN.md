# FirmwareKit.Comm

[![NuGet version](https://img.shields.io/nuget/v/FirmwareKit.Comm.svg)](https://www.nuget.org/packages/FirmwareKit.Comm)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

[English](README.md) | 简体中文

跨平台的 USB 通信库，为 FirmwareKit 提供统一的 USB 抽象层、设备会话管理，以及多种平台后端（Windows / Linux / macOS / LibUsbDotNet）。

## 特性

- 跨平台 USB 抽象：支持系统原生实现与 `LibUsbDotNet` 后端。
- 设备发现与过滤：按 `VendorId`、`ProductId`、`SerialNumber`、`DevicePath` 等过滤设备。
- 可选接口级过滤：可按 `InterfaceClass`、`InterfaceSubClass`、`InterfaceProtocol` 约束底层接口匹配。
- 异步会话能力：支持 `IAsyncUsbDeviceSession`，并提供 `AsAsync()` 适配器用于自定义同步会话。
- 接口元数据可追踪来源：`UsbDeviceInfo.InterfaceMetadataObserved` 可区分“真实观测”与“由过滤条件推断”的接口信息。
- 设备变化监视：支持 `MonitorUsbDevices` / `MonitorDevices` 轮询监视新增与移除事件，并可通过 `onError` 捕获监视阶段异常。
- 后端能力摘要：可通过 `GetAvailableUsbApiCapabilities` 查看各后端的发现、会话、异步与热插拔能力轮廓。
- 控制传输：统一暴露 `ControlTransfer` / `ControlTransferAsync`，支持标准 USB setup packet 请求。
- 结构化传输诊断：支持 `UsbTrace.TransferObserved`，统一输出读写操作的耗时、错误码、重试次数和结果。
- 可扩展：可通过 `RegisterUsbApi` 注册自定义 USB API 提供器。
- 简洁的门面 API：`IFirmwareKitComm` / `FirmwareKitComm`，便于在上层工具或应用中使用。
- 自带轻量 CLI：项目 `FirmwareKit.Comm.CLI` 提供 `apis` 与 `devices` 命令。

## 设计边界

FirmwareKit.Comm 专注于跨平台原生 USB 传输能力：

- 设备发现与筛选
- 会话管理
- 统一读写与超时控制
- 传输层复位

设备发现默认优先走“元数据发现”路径，避免为仅枚举场景建立长期读写会话；
真正的数据收发会在调用 `OpenUsbDeviceSessions` 后进行。

协议层不在本库中实现，由调用程序基于统一会话接口自行实现。

## 安装

使用 NuGet 安装包：

```powershell
dotnet add package FirmwareKit.Comm
```

## 快速开始

如何使用门面类 `FirmwareKitComm` 枚举可用的 USB API 与设备：

```csharp
using FirmwareKit.Comm;
using FirmwareKit.Comm.Usb;

var comm = new FirmwareKitComm();

// 列出已注册的 USB API
foreach (var api in comm.GetAvailableUsbApis())
 Console.WriteLine(api);

// 查看后端能力摘要
foreach (var capability in comm.GetAvailableUsbApiCapabilities())
{
 Console.WriteLine($"api={capability.ApiName} nativeDiscovery={capability.SupportsNativeDiscovery} nativeAsync={capability.SupportsNativeAsyncIo} hotplug={capability.SupportsNativeHotPlugMonitoring} externalRuntime={capability.RequiresExternalRuntime}");
}

// 同步枚举设备并过滤 VendorId（示例：0x18D1）
var devices = comm.EnumerateUsbDevices(UsbApiKind.Auto, new UsbDeviceFilter { VendorId = 0x18D1 });
foreach (var d in devices)
{
 var ifClass = d.InterfaceClass.HasValue ? $"0x{d.InterfaceClass.Value:X2}" : "--";
 var ifSubClass = d.InterfaceSubClass.HasValue ? $"0x{d.InterfaceSubClass.Value:X2}" : "--";
 var ifProto = d.InterfaceProtocol.HasValue ? $"0x{d.InterfaceProtocol.Value:X2}" : "--";
 Console.WriteLine($"api={d.ApiName} vid=0x{d.VendorId:X4} pid=0x{d.ProductId:X4} if={ifClass}/{ifSubClass}/{ifProto} serial={d.SerialNumber ?? "<null>"} path={d.DevicePath}");
}

// 可选：按USB接口类过滤（例如 Qualcomm EDL 常见为 0xFF/0xFF/0xFF）
var edlLikeDevices = comm.EnumerateUsbDevices(UsbApiKind.Auto, new UsbDeviceFilter
{
 VendorId = 0x05C6,
 InterfaceClass = 0xFF,
 InterfaceSubClass = 0xFF,
 InterfaceProtocol = 0xFF
});

// 异步枚举
var asyncDevices = await comm.EnumerateUsbDevicesAsync(UsbApiKind.LibUsbDotNet);

// 打开会话并执行统一读写（协议解析由调用方实现）
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
 // 仅示例：具体命令/协议包由上层自己定义
 _ = session.Write(new byte[] { 0x7E, 0x00 }, 2, 3000);
 var response = session.Read(512, 3000);
 Console.WriteLine($"response bytes: {response.Length}");

 // 控制传输示例：读取接口当前备用设置
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

 // 异步会话（若后端不直接实现异步接口，可用 AsAsync() 适配）
 var asyncSession = session.AsAsync();
 var asyncResponse = await asyncSession.ReadAsync(512, 3000);
 Console.WriteLine($"async response bytes: {asyncResponse.Length}");
}

// 设备变化监视（记得在适当时机释放）
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

// 结构化诊断事件（可用于指标上报/日志聚合）
UsbTrace.TransferObserved += evt =>
{
 Console.WriteLine($"usb {evt.Operation} backend={evt.Backend} outcome={evt.Outcome} bytes={evt.TransferredBytes}/{evt.RequestedBytes} retry={evt.RetryCount} err={evt.NativeErrorCode}");
};
```

注册自定义 USB API 的示例：

```csharp
comm.RegisterUsbApi("my-custom", () => new MyCustomUsbApiProvider());
```

## 命令行工具

项目 `FirmwareKit.Comm.CLI` 提供两个主要命令：

- `apis`：列出可用的 USB API。
- `devices`：枚举设备并可用下列参数过滤。
- `all-devices`：列出当前平台可识别的全部 USB 设备（默认使用 native 后端）。

用法示例：

```powershell
# 列出 API
dotnet run --project FirmwareKit.Comm.CLI -- apis

# 列出设备（使用 libusb、按 VID/PID 过滤）
dotnet run --project FirmwareKit.Comm.CLI -- devices --api libusb --vid 0x18D1 --pid 0x4E11

# 列出当前平台可识别的全部 USB 设备
dotnet run --project FirmwareKit.Comm.CLI -- all-devices
```

支持的 `devices` 参数：

- `--api auto|native|libusb`：选择后端 API。
- `--vid <hex>`：供应商 ID（十六进制或十进制）。
- `--pid <hex>`：产品 ID（十六进制或十进制）。
- `--serial <text>`：设备序列号。
- `--path-contains <text>`：设备路径包含文本。
- `--if-class <hex|dec>`：接口类代码过滤。
- `--if-subclass <hex|dec>`：接口子类代码过滤。
- `--if-protocol <hex|dec>`：接口协议代码过滤。

## 许可

MIT
