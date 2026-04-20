namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Provides metadata-only USB device discovery for a backend.
/// 为后端提供仅元数据的 USB 设备发现能力。
/// </summary>
public interface IUsbApiDiscoveryProvider
{
    /// <summary>
    /// Enumerates device metadata using the optional filter without opening long-lived sessions.
    /// 使用可选过滤器枚举设备元数据，不建立长期会话。
    /// </summary>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>A list of matching device metadata. 匹配设备元数据列表。</returns>
    IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null);
}
