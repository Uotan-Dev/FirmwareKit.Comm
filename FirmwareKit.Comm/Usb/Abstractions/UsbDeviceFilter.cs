namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Describes optional device matching criteria.
/// <para>描述可选的设备匹配条件。</para>
/// </summary>
public sealed class UsbDeviceFilter
{
    /// <summary>
    /// Gets or sets the vendor identifier filter.
    /// <para>获取或设置厂商 ID 过滤条件。</para>
    /// </summary>
    public ushort? VendorId { get; set; }

    /// <summary>
    /// Gets or sets the product identifier filter.
    /// <para>获取或设置产品 ID 过滤条件。</para>
    /// </summary>
    public ushort? ProductId { get; set; }

    /// <summary>
    /// Gets or sets the serial number filter.
    /// <para>获取或设置序列号过滤条件。</para>
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Gets or sets a substring that must appear in the device path.
    /// <para>获取或设置设备路径必须包含的子串。</para>
    /// </summary>
    public string? DevicePathContains { get; set; }

    /// <summary>
    /// Gets or sets the backend family filter.
    /// <para>获取或设置后端类型过滤条件。</para>
    /// </summary>
    public UsbApiKind? SourceApiKind { get; set; }

    /// <summary>
    /// Gets or sets the required USB interface class code.
    /// <para>获取或设置要求的 USB 接口类代码。</para>
    /// </summary>
    public byte? InterfaceClass { get; set; }

    /// <summary>
    /// Gets or sets the required USB interface subclass code.
    /// <para>获取或设置要求的 USB 接口子类代码。</para>
    /// </summary>
    public byte? InterfaceSubClass { get; set; }

    /// <summary>
    /// Gets or sets the required USB interface protocol code.
    /// <para>获取或设置要求的 USB 接口协议代码。</para>
    /// </summary>
    public byte? InterfaceProtocol { get; set; }

    /// <summary>
    /// Gets or sets the required USB interface number.
    /// <para>获取或设置要求的 USB 接口编号。</para>
    /// Backends that can select an interface (Linux/macOS/libusb/HarmonyOS) prefer this
    /// interface when opening a session; on WinUSB the bound interface is determined by the
    /// driver, so mismatches are filtered out during projection.
    /// <para>可选择接口的后端（Linux/macOS/libusb/HarmonyOS）打开会话时优先使用该接口；
    /// WinUSB 的绑定接口由驱动决定，不匹配的设备会在投影阶段被过滤。</para>
    /// </summary>
    public byte? InterfaceNumber { get; set; }

    /// <summary>
    /// Determines whether the supplied metadata matches this filter.
    /// <para>判断给定元数据是否匹配当前过滤器。</para>
    /// </summary>
    /// <param name="info">The device metadata to test. <para>待匹配的设备元数据。</para></param>
    /// <returns><c>true</c> if the device matches; otherwise, <c>false</c>. <para>匹配返回 <c>true</c>，否则返回 <c>false</c>。</para></returns>
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
        if (InterfaceClass.HasValue && info.InterfaceClass != InterfaceClass.Value) return false;
        if (InterfaceSubClass.HasValue && info.InterfaceSubClass != InterfaceSubClass.Value) return false;
        if (InterfaceProtocol.HasValue && info.InterfaceProtocol != InterfaceProtocol.Value) return false;
        if (InterfaceNumber.HasValue &&
            !info.Interfaces.Any(i => i.InterfaceNumber == InterfaceNumber.Value)) return false;

        return true;
    }
}
