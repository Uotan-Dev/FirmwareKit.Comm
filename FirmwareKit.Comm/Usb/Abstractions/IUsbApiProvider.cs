namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Provides enumeration support for a USB backend.
/// <para>为 USB 后端提供设备枚举能力。</para>
/// </summary>
public interface IUsbApiProvider
{
    /// <summary>
    /// Gets the public API name.
    /// <para>获取对外公开的 API 名称。</para>
    /// </summary>
    string ApiName { get; }

    /// <summary>
    /// Gets the backend family.
    /// <para>获取后端类型。</para>
    /// </summary>
    UsbApiKind ApiKind { get; }

    /// <summary>
    /// Gets whether this backend is supported on the current platform.
    /// <para>获取当前平台是否支持该后端。</para>
    /// </summary>
    bool IsSupportedOnCurrentPlatform { get; }

    /// <summary>
    /// Enumerates device sessions using the optional filter.
    /// <para>使用可选过滤器枚举设备会话。</para>
    /// </summary>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <returns>A list of matching device sessions. <para>匹配设备会话列表。</para></returns>
    IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null);
}
