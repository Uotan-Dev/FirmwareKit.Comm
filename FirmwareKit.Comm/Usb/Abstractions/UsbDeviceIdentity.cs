namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Builds stable identity keys for USB devices.
/// <para>为 USB 设备构建稳定标识键。</para>
/// </summary>
internal static class UsbDeviceIdentity
{
    /// <summary>
    /// Creates a stable key that can be used for reopen, monitoring, or deduplication.
    /// <para>创建可用于重连、监视或去重的稳定键。</para>
    /// </summary>
    /// <param name="info">The device metadata. <para>设备元数据。</para></param>
    /// <returns>A stable identity string. <para>稳定标识字符串。</para></returns>
    public static string BuildKey(UsbDeviceInfo info)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        var serial = info.SerialNumber ?? string.Empty;
        var devicePath = info.DevicePath ?? string.Empty;
        var interfaceClass = info.InterfaceClass?.ToString("X2") ?? string.Empty;
        var interfaceSubClass = info.InterfaceSubClass?.ToString("X2") ?? string.Empty;
        var interfaceProtocol = info.InterfaceProtocol?.ToString("X2") ?? string.Empty;

        return string.Join("|", new[]
        {
            info.ApiName,
            info.SourceApiKind.ToString(),
            info.VendorId.ToString("X4"),
            info.ProductId.ToString("X4"),
            interfaceClass,
            interfaceSubClass,
            interfaceProtocol,
            serial,
            devicePath
        });
    }
}