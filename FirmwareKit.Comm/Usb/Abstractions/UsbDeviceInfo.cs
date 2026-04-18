namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Describes a discovered USB device.
/// 描述已发现的 USB 设备。
/// </summary>
public sealed class UsbDeviceInfo
{
    /// <summary>
    /// Gets or sets the public API name.
    /// 获取或设置对外 API 名称。
    /// </summary>
    public string ApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backend family.
    /// 获取或设置后端类型。
    /// </summary>
    public UsbApiKind SourceApiKind { get; set; }

    /// <summary>
    /// Gets or sets the concrete source device type.
    /// 获取或设置具体来源设备类型。
    /// </summary>
    public string SourceDeviceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device path.
    /// 获取或设置设备路径。
    /// </summary>
    public string DevicePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serial number.
    /// 获取或设置设备序列号。
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Gets or sets the vendor identifier.
    /// 获取或设置厂商 ID。
    /// </summary>
    public ushort VendorId { get; set; }

    /// <summary>
    /// Gets or sets the product identifier.
    /// 获取或设置产品 ID。
    /// </summary>
    public ushort ProductId { get; set; }
}
