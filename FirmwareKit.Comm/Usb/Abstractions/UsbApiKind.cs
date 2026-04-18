namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Selects the USB backend family.
/// 选择 USB 后端类型。
/// </summary>
public enum UsbApiKind
{
    /// <summary>
    /// Automatically selects the available backend(s).
    /// 自动选择可用后端。
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Uses the native platform backend.
    /// 使用平台原生后端。
    /// </summary>
    Native = 1,

    /// <summary>
    /// Uses the LibUsbDotNet backend.
    /// 使用 LibUsbDotNet 后端。
    /// </summary>
    LibUsbDotNet = 2,

    /// <summary>
    /// Represents a custom backend registration.
    /// 表示自定义后端注册类型。
    /// </summary>
    Custom = 3
}
