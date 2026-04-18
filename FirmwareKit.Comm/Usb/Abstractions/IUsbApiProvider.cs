namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Provides enumeration support for a USB backend.
/// 为 USB 后端提供设备枚举能力。
/// </summary>
public interface IUsbApiProvider
{
    /// <summary>
    /// Gets the public API name.
    /// 获取对外公开的 API 名称。
    /// </summary>
    string ApiName { get; }

    /// <summary>
    /// Gets the backend family.
    /// 获取后端类型。
    /// </summary>
    UsbApiKind ApiKind { get; }

    /// <summary>
    /// Gets whether this backend is supported on the current platform.
    /// 获取当前平台是否支持该后端。
    /// </summary>
    bool IsSupportedOnCurrentPlatform { get; }

    /// <summary>
    /// Enumerates device sessions using the optional filter.
    /// 使用可选过滤器枚举设备会话。
    /// </summary>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>A list of matching device sessions. 匹配设备会话列表。</returns>
    IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null);
}
