namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Describes optional device matching criteria.
/// 描述可选的设备匹配条件。
/// </summary>
public sealed class UsbDeviceFilter
{
    /// <summary>
    /// Gets or sets the vendor identifier filter.
    /// 获取或设置厂商 ID 过滤条件。
    /// </summary>
    public ushort? VendorId { get; set; }

    /// <summary>
    /// Gets or sets the product identifier filter.
    /// 获取或设置产品 ID 过滤条件。
    /// </summary>
    public ushort? ProductId { get; set; }

    /// <summary>
    /// Gets or sets the serial number filter.
    /// 获取或设置序列号过滤条件。
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Gets or sets a substring that must appear in the device path.
    /// 获取或设置设备路径必须包含的子串。
    /// </summary>
    public string? DevicePathContains { get; set; }

    /// <summary>
    /// Gets or sets the backend family filter.
    /// 获取或设置后端类型过滤条件。
    /// </summary>
    public UsbApiKind? SourceApiKind { get; set; }

    /// <summary>
    /// Determines whether the supplied metadata matches this filter.
    /// 判断给定元数据是否匹配当前过滤器。
    /// </summary>
    /// <param name="info">The device metadata to test. 待匹配的设备元数据。</param>
    /// <returns><c>true</c> if the device matches; otherwise, <c>false</c>. 匹配返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public bool Matches(UsbDeviceInfo info)
    {
        if (VendorId.HasValue && info.VendorId != VendorId.Value) return false;
        if (ProductId.HasValue && info.ProductId != ProductId.Value) return false;
        if (!string.IsNullOrWhiteSpace(SerialNumber) &&
            !string.Equals(info.SerialNumber, SerialNumber, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(DevicePathContains) &&
            (string.IsNullOrWhiteSpace(info.DevicePath) ||
             info.DevicePath.IndexOf(DevicePathContains, StringComparison.OrdinalIgnoreCase) < 0)) return false;
        if (SourceApiKind.HasValue && info.SourceApiKind != SourceApiKind.Value) return false;

        return true;
    }
}
