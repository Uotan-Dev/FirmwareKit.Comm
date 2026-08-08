namespace FirmwareKit.Comm.Abstractions;

/// <summary>
/// Provides metadata-only USB device discovery for a backend.
/// <para>为后端提供仅元数据的 USB 设备发现能力。</para>
/// </summary>
public interface IUsbApiDiscoveryProvider
{
    /// <summary>
    /// Enumerates device metadata using the optional filter without opening long-lived sessions.
    /// <para>使用可选过滤器枚举设备元数据，不建立长期会话。</para>
    /// </summary>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <returns>A list of matching device metadata. <para>匹配设备元数据列表。</para></returns>
    IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null);
}
