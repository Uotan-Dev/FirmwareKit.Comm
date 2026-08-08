namespace FirmwareKit.Comm.Abstractions;

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

    /// <summary>
    /// Builds a physical identity key that is independent of the backend and the device
    /// path, so the same physical device is deduplicated across backends (native + libusb)
    /// in monitoring. Based on VID/PID/interface triple/serial only.
    /// <para>构建与后端和设备路径无关的物理身份键，使同一物理设备在监控中可跨后端
    /// （native + libusb）去重。仅基于 VID/PID/接口三元组/序列号。</para>
    /// Devices without a serial number fall back to VID/PID/interface, which collapses
    /// identical unlabeled devices onto one key (documented limitation).
    /// <para>无序列号的设备退化为 VID/PID/接口，多个相同无标签设备会合并到同一个键
    /// （已知限制）。</para>
    /// </summary>
    /// <param name="info">The device metadata. <para>设备元数据。</para></param>
    /// <returns>A backend-independent physical identity string. <para>与后端无关的物理标识字符串。</para></returns>
    public static string BuildPhysicalKey(UsbDeviceInfo info)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        var serial = info.SerialNumber ?? string.Empty;
        var interfaceClass = info.InterfaceClass?.ToString("X2") ?? string.Empty;
        var interfaceSubClass = info.InterfaceSubClass?.ToString("X2") ?? string.Empty;
        var interfaceProtocol = info.InterfaceProtocol?.ToString("X2") ?? string.Empty;

        return string.Join("|", new[]
        {
            info.VendorId.ToString("X4"),
            info.ProductId.ToString("X4"),
            interfaceClass,
            interfaceSubClass,
            interfaceProtocol,
            serial
        });
    }
}