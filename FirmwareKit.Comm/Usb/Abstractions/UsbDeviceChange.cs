namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Describes a USB device change kind.
/// <para>描述 USB 设备变化类型。</para>
/// </summary>
public enum UsbDeviceChangeKind
{
    /// <summary>
    /// The device has been discovered.
    /// <para>设备被发现。</para>
    /// </summary>
    Added = 0,

    /// <summary>
    /// The device is no longer present.
    /// <para>设备已不再存在。</para>
    /// </summary>
    Removed = 1
}

/// <summary>
/// Represents a single device change entry.
/// <para>表示单个设备变化条目。</para>
/// </summary>
public sealed class UsbDeviceChange
{
    /// <summary>
    /// Gets or sets the change kind.
    /// <para>获取或设置变化类型。</para>
    /// </summary>
    public UsbDeviceChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the device metadata.
    /// <para>获取或设置设备元数据。</para>
    /// </summary>
    public UsbDeviceInfo Device { get; set; } = new();
}
