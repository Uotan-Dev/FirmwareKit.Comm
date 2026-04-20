namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Describes a USB device change kind.
/// 描述 USB 设备变化类型。
/// </summary>
public enum UsbDeviceChangeKind
{
    /// <summary>
    /// The device has been discovered.
    /// 设备被发现。
    /// </summary>
    Added = 0,

    /// <summary>
    /// The device is no longer present.
    /// 设备已不再存在。
    /// </summary>
    Removed = 1
}

/// <summary>
/// Represents a single device change entry.
/// 表示单个设备变化条目。
/// </summary>
public sealed class UsbDeviceChange
{
    /// <summary>
    /// Gets or sets the change kind.
    /// 获取或设置变化类型。
    /// </summary>
    public UsbDeviceChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the device metadata.
    /// 获取或设置设备元数据。
    /// </summary>
    public UsbDeviceInfo Device { get; set; } = new();
}
