# FirmwareKit.Comm

[![NuGet version](https://img.shields.io/nuget/v/FirmwareKit.Comm.svg)](https://www.nuget.org/packages/FirmwareKit.Comm)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

[English](README.md) | 简体中文

跨平台的 USB 通信库，为 FirmwareKit 提供统一的 USB 抽象层。它在系统原生后端（Windows / Linux / macOS / HarmonyOS）与 LibUsbDotNet 之上提供统一的设备发现、过滤、会话管理以及结构化传输诊断能力。**仅提供传输原语**——协议层（Sahara、Firehose、Fastboot 等）不在范围内，由调用方基于统一会话接口自行实现。

## 特性

- **统一会话 API**：一个抽象同时提供同步（`IUsbDeviceSession`）与异步（`IAsyncUsbDeviceSession`）读写/控制传输，并支持按方向串行化，便于全双工协议线程。
- **四类原生后端 + libusb**：Windows WinUSB（及 legacy 驱动）、Linux usbfs、macOS IOKit.framework 经典 API、HarmonyOS USBManager DDK 与 LibUsbDotNet。
- **设备发现与过滤**：按 `VendorId`、`ProductId`、`SerialNumber`、`DevicePath`、接口类/子类/协议、接口编号（列表）以及显式端点地址过滤。
- **数据包语义**：`ReadPacket` / `ReadPacketAsync` 返回 `UsbReadResult`，区分短包（USB 消息边界）与超时——正是 fastboot/EDL/bootrom 分帧所需。`ReadExact` / `ReadExactAsync` 在总期限内精确读取定长字节。
- **零长度包（ZLP）控制**：`WriteZlp` / `WriteZlpAsync` 终止载荷长度恰为端点最大包大小整数倍的传输。
- **进度回调**：`ReadPacketAsync` / `WriteAsync` 重载接受 `IProgress<long>`，在每块完成后报告累计字节数（适用于刷写大镜像）。
- **安全护栏**：读取长度上限（`UsbTransferPolicies.MaxReadLength`）防止不可信协议长度引发 OOM；会话释放幂等；net8.0+ 提供 `ReadInto(Span<byte>)`。
- **可观测的枚举诊断**：`UsbCommunicationLayer.GetEnumerationDiagnostics()` 与各 finder 计数器，区分"枚举机制未运行"与"已运行但未发现设备"（无设备 CI 场景）。
- **模式切换工作流**：`WaitForUsbDeviceAppearAsync` / `WaitForUsbDeviceDisappearAsync` / `WaitForUsbDeviceModeSwitchAsync`，以及通过 `OpenDeviceSessionByKey` 按键重开会话。
- **热插拔监视**：可用时优先事件驱动（Windows 原生用 `WM_DEVICECHANGE`、libusb 热插拔），否则回退轮询；变更事件含 `Added` / `Removed` / `Changed`。
- **后端能力模型**：`UsbApiCapabilities`（及逐后端 `UsbBackendCapability`）暴露异步支持、热插拔支持、外部运行时需求与 `ResetReenumeratesDevice`。
- **诊断**：`UsbTrace` 结构化传输事件（`TransferObserved`）与可选帧捕获；`LogFormatted` 延迟字符串插值，日志关闭时不产生开销。
- **可扩展性**：通过 `RegisterUsbApi` / `UsbApiRegistry` 注册自定义提供器。
- **内置 CLI**：`FirmwareKit.Comm.CLI`，提供 `apis`、`devices`、`all-devices`、`monitor`、`selftest`、`io-test` 与 `interrupt-test` 命令。

## 设计边界

FirmwareKit.Comm 聚焦跨平台 USB 传输原语：

- 设备发现与过滤
- 会话管理（打开 / 读 / 写 / 控制 / 中断 / 重置）
- 带超时控制的统一读写
- 传输层重置

发现优先走元数据路径，简单枚举无需建立长期读写会话；实际载荷 I/O 从调用 `OpenUsbDeviceSessions` 开始。

后端矩阵：

| 后端 | 平台 | 传输方式 | 说明 |
|------|------|---------|------|
| `native`（WinUSB） | Windows | WinUSB API | 重叠（真异步）I/O；需要 WinUSB 绑定的接口（Zadig 等） |
| `native`（legacy） | Windows | DeviceIoControl | 旧式驱动回退；不支持 ZLP、无原生异步 |
| `native`（usbfs） | Linux | usbfs ioctl / URB | 通过 URB + poll 真异步；支持多接口声明 |
| `native`（IOKit） | macOS | IOKit.framework 经典 API | 每个 macOS 发行版均可用；设备打开遵循 adb（仅接口级）；保留 IOUSBHost 回退 |
| `native`（HarmonyOS） | HarmonyOS | USBManager DDK | 需通过 `FIRMWAREKIT_USB_ENABLE_HARMONY=1` 显式开启；要求 `OH_Usb_Init()` 成功 |
| `libusb` | 全平台 | LibUsbDotNet | 原生异步传输；需要原生 libusb 运行时（包内按 RID 附带，或经 `UsbCommunicationLayer.SetLibusbLibraryPath` 传入显式路径）；缺失时优雅降级 |

HarmonyOS 默认从 `GetAvailableApis()` 中隐藏（文件探测无法可靠识别）；需显式开启。macOS 上 IOKit 原生后端在每个发行版均可用；不需要原生路径时使用 `libusb` 后端。

## 传输超时语义

- 传给 `Read` / `Write`（及 `ReadInto` 变体）的超时**按块生效**而非整个操作：超过后端块大小的传输会被拆分，总耗时可达 `块数 × timeoutMs`。
- 需要在总期限内精确读取定长字节（例如 fastboot/EDL 定长响应）时，使用 `ReadExact(length, timeoutMs)` / `ReadExactAsync`——它们在总期限内循环短读，超时时返回实际收到的字节。
- 短读/短写会停止传输并返回部分字节数；设备断开抛出 `UsbDeviceDisconnectedException`（区别于普通 `IOException`/`UsbTransferException`）。

**各后端默认超时不同**。仅在默认值可接受时才省略 `timeoutMs`：

| 后端 | 默认超时 |
|------|---------|
| Windows WinUSB | 60 000 ms |
| Windows legacy / Linux usbfs / libusb / macOS / HarmonyOS | 5 000 ms |

当操作不能依赖后端默认值时，请显式传 `timeoutMs`（或 `UsbTransferPolicies.InfiniteTimeoutMs` = -1 表示无限等待）。

省略超时的异步扩展重载（`ReadAsync(length)`、`ReadIntoAsync(...)`、`ReadPacketAsync(...)`、`WriteAsync(...)`、`WriteZlpAsync()`、`ControlTransferAsync(...)`、`ReadInterruptAsync(...)`、`WriteInterruptAsync(...)`）使用会话的 `DefaultTimeoutMs`。

Linux usbfs 后端的端点读/写（`ReadInterrupt`/`WriteInterrupt`）在线程池线程上等待 `poll()`；使用 `InfiniteTimeoutMs` 且设备无响应时，等待线程会一直占用直到设备响应或断开。

可恢复错误的重试可通过 `UsbTransferPolicies.DefaultRetryPolicy`（`UsbTransferRetryPolicy`，含 `MaxRetries` 与 `RetryDelayMs`）在进程级配置。

## 零长度包（ZLP）处理

载荷长度**恰好为端点最大包大小（通常 512 或 1024 字节）的整数倍**的批量传输，必须以零长度包结束，设备才能判断传输结束。协议下载（adb push、fastboot `download:`、EDL firehose）与 bootrom loader 尤其需要。

- 判断是否需要 ZLP：`payloadLength % maxPacketSize == 0`（从设备 `Interfaces[i].Endpoints` 元数据读取 `MaxPacketSize`）。
- 此类写入之后调用 `session.WriteZlp(timeoutMs)`（或 `WriteZlpAsync`）发送终止零长度包。
- 无法执行显式 ZLP 写入的后端（Windows legacy 驱动）抛出 `NotSupportedException`；请先检查 `UsbApiCapabilities` / 后端能力。

```csharp
// 示例：向 OUT 最大包大小 512 的设备写入 512 KiB 块。
long written = session.Write(block, 0, block.Length, timeoutMs);
if (block.Length % 512 == 0)
    session.WriteZlp(timeoutMs);
```

## Reset 语义

`IUsbDeviceSession.Reset()` 语义因后端而异。依赖重置后的会话前，请查询 `UsbApiCapabilities.ResetReenumeratesDevice`（或具体后端的 `UsbBackendCapability.ResetReenumeratesDevice`）：

| 后端 | Reset 效果 | Reset 后会话 | `ResetReenumeratesDevice` |
|------|-----------|-------------|---------------------------|
| Windows WinUSB | `WinUsb_ResetPipe`（管道级） | 仍可用 | `false` |
| Windows legacy | 无操作 | 仍可用 | `false` |
| macOS IOKit（原生） | 管道中止 + 清除 stall | 仍可用 | `false` |
| Linux usbfs | `USBDEVFS_RESET`（设备级） | **失效——需重新枚举并重开** | `true` |
| libusb | `libusb_reset_device`（设备级） | **失效——需重新枚举并重开** | `true` |
| HarmonyOS DDK | 重新初始化 DDK 会话并重新声明 | **失效——需重开** | `true` |

设备级重置后，请使用 `WaitForUsbDeviceDisappearAsync` / `WaitForUsbDeviceAppearAsync`（或 `WaitForUsbDeviceModeSwitchAsync`）再通过 `OpenUsbDeviceSession` / `OpenUsbDeviceSessionByKey` 获取新会话。

## 异步 I/O 语义

真正的异步（非阻塞）I/O 由 **WinUSB**（重叠 I/O）、**Linux usbfs**（URB + poll）与 **libusb**（LibUsbDotNet 异步传输）原生实现。其余后端——macOS IOKit、HarmonyOS DDK 以及 `AsAsync()` 适配器——在线程池线程上执行底层同步传输（`UsbAsyncExecution.Run`）。

通过 `UsbApiCapabilities.SupportsNativeAsyncIo`（或逐后端 `UsbBackendCapability.SupportsNativeAsyncIo`）判断 `ReadAsync`/`WriteAsync` 是真正非阻塞还是仅卸载。不允许阻塞调用方线程的协议层，应将 `SupportsNativeAsyncIo == false` 的后端视为"同步 + 卸载"。

## 枚举诊断（无设备 CI）

未连接设备时（托管 CI runner），空枚举既可能意味着"没有设备"，也可能意味着"枚举机制失败"。每个 finder 暴露可观测状态，`UsbCommunicationLayer.GetEnumerationDiagnostics()` 以 `key=value; ...` 形式汇总：

| 后端 | 机制诊断 | 扫描证据 | 计数器 |
|------|---------|---------|--------|
| Linux usbfs | `LastUsbfsRootExists`（usbfs 是否挂载） | `LastScannedNodes` | `LastMatchedDeviceCount`、`PermissionDeniedCount`、`BusyCount` |
| Windows | `LastSetupDiSucceeded`（SetupDi 句柄是否打开） | `LastScannedNodeCount` | `LastMatchedDeviceCount` |
| macOS | `LastCopyDevicesSucceeded`（IOKit `IOServiceGetMatchingServices` 是否返回） | `LastScannedDeviceCount` | `LastMatchedDeviceCount` |
| libusb | `IsRuntimeAvailable(out reason)` | 设备列表 | 设备列表 |

CLI 每次 `all-devices` 都会打印 `[enum-diagnostics] <摘要>`；CI 在其上断言，而非只检查"exit 0"。库还附带单元测试：将构造的 USB 描述符字节喂给纯解析器（`LinuxUsbFinder.TryParseDescriptor`），在无硬件条件下验证枚举正确性。

## 热插拔监视

`MonitorUsbDevices` 优先使用事件驱动通知：`WM_DEVICECHANGE`（Windows 原生后端）与 libusb 热插拔（Linux/macOS，`UsbApiKind.LibUsbDotNet`）；不支持的平台回退到轮询（`pollInterval`，默认 1 秒）。变更事件含 `Added`、`Removed` 与 `Changed`（物理身份不变但元数据变化）。传入 `CancellationToken` 可自动释放监视句柄；模式切换工作流可使用 `WaitForUsbDeviceAppearAsync` / `WaitForUsbDeviceDisappearAsync` / `WaitForUsbDeviceModeSwitchAsync`。

## 安装

通过 NuGet 安装：

```powershell
dotnet add package FirmwareKit.Comm
```

包目标为 `net10.0`、`net8.0` 与 `netstandard2.0`，并为每个受支持的 RID（win-x64 / win-arm64 / osx-x64 / osx-arm64 / linux-x64 / linux-arm64 / linux-riscv64 / linux-loong64）附带原生 libusb 运行时。构建解决方案（`.slnx`）需要 **.NET 10 SDK**。

## 快速开始

使用 `FirmwareKitComm` 门面枚举 API 与设备：

```csharp
using FirmwareKit.Comm;
using FirmwareKit.Comm.Abstractions;

var comm = new FirmwareKitComm();

// 列出已注册的 USB API
foreach (var api in comm.GetAvailableUsbApis())
    Console.WriteLine(api);

// 打印后端能力摘要
foreach (var capability in comm.GetAvailableUsbApiCapabilities())
{
    Console.WriteLine($"api={capability.ApiName} nativeDiscovery={capability.SupportsNativeDiscovery} nativeAsync={capability.SupportsNativeAsyncIo} hotplug={capability.SupportsNativeHotPlugMonitoring} externalRuntime={capability.RequiresExternalRuntime}");
}

// 同步枚举设备并按 VendorId 过滤（示例：0x18D1）
var devices = comm.EnumerateUsbDevices(UsbApiKind.Auto, new UsbDeviceFilter { VendorId = 0x18D1 });
foreach (var d in devices)
{
    var ifClass = d.InterfaceClass.HasValue ? $"0x{d.InterfaceClass.Value:X2}" : "--";
    var ifSubClass = d.InterfaceSubClass.HasValue ? $"0x{d.InterfaceSubClass.Value:X2}" : "--";
    var ifProto = d.InterfaceProtocol.HasValue ? $"0x{d.InterfaceProtocol.Value:X2}" : "--";
    Console.WriteLine($"api={d.ApiName} vid=0x{d.VendorId:X4} pid=0x{d.ProductId:X4} if={ifClass}/{ifSubClass}/{ifProto} serial={d.SerialNumber ?? "<null>"} path={d.DevicePath}");
}

// 可选：按 USB 接口类过滤（例如 Qualcomm EDL 常用 0xFF/0xFF/0xFF）
var edlLikeDevices = comm.EnumerateUsbDevices(UsbApiKind.Auto, new UsbDeviceFilter
{
    VendorId = 0x05C6,
    InterfaceClass = 0xFF,
    InterfaceSubClass = 0xFF,
    InterfaceProtocol = 0xFF
});

// 多接口声明（例如 CDC-ACM 控制 + 数据）：列出的每个接口都必须存在
var multiIf = comm.EnumerateUsbDevices(UsbApiKind.Auto, new UsbDeviceFilter
{
    InterfaceNumber = 0,                 // 主数据接口
    InterfaceNumbers = new byte[] { 1 }  // 需要额外声明的接口（Linux/libusb）
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
    // 仅示例：命令/协议载荷与应用相关
    _ = session.Write(new byte[] { 0x7E, 0x00 }, 2, 3000);
    var response = session.Read(512, 3000);
    Console.WriteLine($"response bytes: {response.Length}");

    // 控制传输示例：读取当前备用设置
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

    // 异步会话（后端未原生实现异步时使用 AsAsync()）
    var asyncSession = session.AsAsync();
    var asyncResponse = await asyncSession.ReadAsync(512, 3000);
    Console.WriteLine($"async response bytes: {asyncResponse.Length}");

    // 包感知读取：区分短包与超时（fastboot/EDL 消息边界）
    var buf = new byte[512];
    var result = session.ReadPacket(buf, 0, buf.Length, 3000);
    Console.WriteLine($"packet bytes={result.Count} timeout={result.IsTimeout} short={result.IsShortPacket}");

    // 刷写大镜像时报告进度
    var progress = new Progress<long>(total => Console.WriteLine($"written: {total}"));
    await asyncSession.WriteAsync(image, 0, image.Length, 3000, progress);
}

// 设备变更监视（适当时释放）
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

// 结构化诊断事件（用于指标/日志聚合）
UsbTrace.TransferObserved += evt =>
{
    Console.WriteLine($"usb {evt.Operation} backend={evt.Backend} outcome={evt.Outcome} bytes={evt.TransferredBytes}/{evt.RequestedBytes} retry={evt.RetryCount} err={evt.NativeErrorCode}");
};
```

注册自定义 USB API 提供器：

```csharp
comm.RegisterUsbApi("my-custom", () => new MyCustomUsbApiProvider());
```

## CLI

`FirmwareKit.Comm.CLI` 提供以下命令：

- `apis`：列出可用的 USB API。
- `devices`：按可选过滤器枚举设备。
- `all-devices`：列出当前平台识别到的所有 USB 设备（默认原生后端）；即使为空也打印 `[enum-diagnostics]`。
- `monitor`：运行设备变更监视器（选项：`--api`、`--vid`、`--pid`、`--interval <秒>`）。
- `selftest`：只读冒烟测试——打开会话、GET_DESCRIPTOR 控制传输、短 `ReadExact`（不写不重置）；`--duration <秒>` 循环（0 = 单次）。
- `io-test`：写入/重置冒烟测试（bulk OUT 写入、重置、端点打开、并发 I/O、偏移写入）；选项含 `--ep-in`、`--ep-out`、`--pattern-size`、`--concurrent`、`--offset-write`。
- `interrupt-test`：中断 IN 端点读取测试（选项：`--ep <地址>`）。

示例：

```powershell
# 列出 API
dotnet run --project FirmwareKit.Comm.CLI -- apis

# 列出设备（libusb 后端，按 VID/PID 过滤）
dotnet run --project FirmwareKit.Comm.CLI -- devices --api libusb --vid 0x18D1 --pid 0x4E11

# 列出当前平台识别到的所有 USB 设备（JSON 输出）
dotnet run --project FirmwareKit.Comm.CLI -- all-devices --json

# 针对指定设备运行只读 selftest
dotnet run --project FirmwareKit.Comm.CLI -- selftest --api native --vid 0x18D1 --duration 10
```

`devices` / `all-devices` 支持的选项：

- `--api auto|native|libusb`：选择后端 API。
- `--vid <十六进制>`：厂商 ID（十六进制或十进制）。
- `--pid <十六进制>`：产品 ID（十六进制或十进制）。
- `--serial <文本>`：设备序列号。
- `--path-contains <文本>`：设备路径子串过滤。
- `--if-class <hex|dec>`：接口类过滤。
- `--if-subclass <hex|dec>`：接口子类过滤。
- `--if-protocol <hex|dec>`：接口协议过滤。
- `--json`：JSON 输出（devices / all-devices）。

诊断通过环境变量按需开启：`FIRMWAREKIT_USB_DEBUG=1`（纯文本日志）、`FIRMWAREKIT_USB_CAPTURE_FRAMES=1`（将原始帧捕获进传输事件，每次事件上限 256 字节）。

## 构建、测试与 CI

```bash
dotnet restore
dotnet build -c Release      # CI 验证 Release
dotnet test  -c Release      # xunit v3；CI 上空枚举是预期结果
```

- 解决方案为 `.slnx`，需要 .NET 10 SDK。
- `GeneratePackageOnBuild=true` 使每次构建都会生成 nupkg/snupkg——切勿将 `bin/`/`obj/` 产物当作事实来源。
- `tools/fetch-libusb.sh` 将原生 libusb 运行时下载到 `native/<target>/`（镜像参数：`--mirror tuna|ustc`、`--github-mirror`、`--ghcr-mirror`）。
- `tools/test-windows.ps1` / `tools/test-linux.sh` / `tools/test-macos.sh` 是真机自动化测试脚本（构建 + 单元测试 + CLI 冒烟 + 只读 selftest + 3 秒监视器热插拔冒烟）；无匹配设备时以 `SKIP` 退出 0。
- CI 工作流：`dotnet-ci.yml`（win/ubuntu/macos 的构建/测试/行为观察，含枚举机制断言）、`cross-publish.yml`（RID 发布图检查）、`fetch-libusb.yml`（原生运行时抓取）、`qemu-linux-guest.yml`（QEMU Linux guest 内真实模拟 USB）。

## 许可证

MIT
