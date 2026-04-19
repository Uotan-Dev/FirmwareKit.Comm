# FirmwareKit.Comm vs edl-ng 底层交互需求分析

## 目标

评估 FirmwareKit.Comm 是否可以作为 edl-ng 一类工具的底层跨平台传输层，并保持协议实现由上层程序负责。

## 参考输入

- 仓库: strongtz/edl-ng
- 关键点: QualcommSerial 负责设备发现与统一收发；Sahara/Firehose 在上层协议模块实现。

## 需求对照

1. 跨平台原生后端

- 需求: Windows / Linux / macOS 可用，支持 libusb 作为通用后端。
- 现状: 已具备 Native + LibUsbDotNet Provider。
- 结论: 满足。

1. 统一读写接口

- 需求: 上层协议可复用统一 Read/Write/Timeout/Reset。
- 现状: IUsbDeviceSession 提供 Read/ReadInto/Write/Reset 及超时重载。
- 结论: 满足。

1. 协议层与传输层边界清晰

- 需求: 类库不内置 Sahara/Firehose/Fastboot 协议逻辑。
- 原问题: 历史实现中存在 Fastboot 命名与接口特征硬编码 (0xFF/0x42/0x03)。
- 已整改:
  - 会话实现改为通用命名 UsbDeviceSession。
  - 各平台查找器改为通用 Bulk IN/OUT 匹配，不再绑定 Fastboot 特征。
  - 新增可选 InterfaceClass/InterfaceSubClass/InterfaceProtocol 过滤，由调用方决定协议设备特征。
- 结论: 经过整改后满足。

1. 对 edl-ng 的适配能力

- 需求: 上层可按 VID/PID 与接口特征筛选目标设备，并自行实现 Sahara/Firehose。
- 现状: UsbDeviceFilter 支持 VendorId/ProductId + InterfaceClass/SubClass/Protocol + DevicePath/Serial。
- 结论: 满足，且边界清晰。

## 仍由调用方负责

- 模式探测 (Sahara hello / Firehose XML probe)
- 协议状态机
- 分区读写语义、LUN 扫描策略
- 大文件分块策略与协议级重试

## 结论

FirmwareKit.Comm 经过本次调整后，已可满足 edl-ng 类项目的底层交互需求定位：

- 库本身负责跨平台原生传输统一化。
- 协议实现完全留给调用程序。
