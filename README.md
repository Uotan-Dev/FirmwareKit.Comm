# FirmwareKit.Comm

[![NuGet version](https://img.shields.io/nuget/v/FirmwareKit.Comm.svg)](https://www.nuget.org/packages/FirmwareKit.Comm)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

跨平台的 USB 通信库，为 FirmwareKit 提供统一的 USB 抽象层、设备会话管理，以及多种平台后端（Windows / Linux / macOS / LibUsbDotNet）。

## 特性

- 跨平台 USB 抽象：支持系统原生实现与 `LibUsbDotNet` 后端。
- 设备发现与过滤：按 `VendorId`、`ProductId`、`SerialNumber`、`DevicePath` 等过滤设备。
- 可扩展：可通过 `RegisterUsbApi` 注册自定义 USB API 提供器。
- 简洁的门面 API：`IFirmwareKitComm` / `FirmwareKitComm`，便于在上层工具或应用中使用。
- 自带轻量 CLI：项目 `FirmwareKit.Comm.CLI` 提供 `apis` 与 `devices` 命令。

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

// 同步枚举设备并过滤 VendorId（示例：0x18D1）
var devices = comm.EnumerateUsbDevices(UsbApiKind.Auto, new UsbDeviceFilter { VendorId = 0x18D1 });
foreach (var d in devices)
 Console.WriteLine($"api={d.ApiName} vid=0x{d.VendorId:X4} pid=0x{d.ProductId:X4} serial={d.SerialNumber ?? "<null>"} path={d.DevicePath}");

// 异步枚举
var asyncDevices = await comm.EnumerateUsbDevicesAsync(UsbApiKind.LibUsbDotNet);
```

注册自定义 USB API 的示例：

```csharp
comm.RegisterUsbApi("my-custom", () => new MyCustomUsbApiProvider());
```

## 命令行工具

项目 `FirmwareKit.Comm.CLI` 提供两个主要命令：

- `apis`：列出可用的 USB API。
- `devices`：枚举设备并可用下列参数过滤。

用法示例：

```powershell
# 列出 API
dotnet run --project FirmwareKit.Comm.CLI -- apis

# 列出设备（使用 libusb、按 VID/PID 过滤）
dotnet run --project FirmwareKit.Comm.CLI -- devices --api libusb --vid 0x18D1 --pid 0x4E11
```

支持的 `devices` 参数：

- `--api auto|native|libusb`：选择后端 API。
- `--vid <hex>`：供应商 ID（十六进制或十进制）。
- `--pid <hex>`：产品 ID（十六进制或十进制）。
- `--serial <text>`：设备序列号。
- `--path-contains <text>`：设备路径包含文本。

## 许可

MIT
