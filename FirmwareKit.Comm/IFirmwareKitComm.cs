using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Core;

namespace FirmwareKit.Comm;

/// <summary>
/// Exposes the public FirmwareKit.Comm entry points for USB discovery and API registration.
/// 暴露 FirmwareKit.Comm 的公共入口，用于 USB 发现与 API 注册。
/// </summary>
public interface IFirmwareKitComm
{
    /// <summary>
    /// Gets the names of the registered USB APIs.
    /// 获取已注册的 USB API 名称列表。
    /// </summary>
    /// <returns>A read-only list of available API names. 可用 API 名称只读列表。</returns>
    IReadOnlyList<string> GetAvailableUsbApis();

    /// <summary>
    /// Enumerates USB devices for the specified API and filter.
    /// 按指定 API 与过滤条件枚举 USB 设备。
    /// </summary>
    /// <param name="apiKind">The USB API selection mode. USB API 选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>A read-only list of matched USB device descriptors. 匹配到的 USB 设备描述信息只读列表。</returns>
    IReadOnlyList<UsbDeviceInfo> EnumerateUsbDevices(UsbApiKind apiKind = UsbApiKind.Auto, UsbDeviceFilter? filter = null);

    /// <summary>
    /// Enumerates USB devices asynchronously for the specified API and filter.
    /// 按指定 API 与过滤条件异步枚举 USB 设备。
    /// </summary>
    /// <param name="apiKind">The USB API selection mode. USB API 选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <param name="cancellationToken">A cancellation token. 取消令牌。</param>
    /// <returns>A task that resolves to the matched USB device descriptors. 返回匹配 USB 设备描述信息的任务。</returns>
    Task<IReadOnlyList<UsbDeviceInfo>> EnumerateUsbDevicesAsync(UsbApiKind apiKind = UsbApiKind.Auto, UsbDeviceFilter? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Monitors USB device additions and removals by polling snapshots.
    /// 通过轮询快照监视 USB 设备新增与移除。
    /// </summary>
    /// <param name="onChanged">Change callback. 设备变化回调。</param>
    /// <param name="apiKind">The USB API selection mode. USB API 选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <param name="pollInterval">Polling interval. 轮询间隔。</param>
    /// <param name="fireInitialSnapshot">Whether to emit initial Added events. 是否触发初始 Added 事件。</param>
    /// <returns>A disposable monitor handle. 可释放的监视句柄。</returns>
    IDisposable MonitorUsbDevices(
        Action<IReadOnlyList<UsbDeviceChange>> onChanged,
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null,
        TimeSpan? pollInterval = null,
        bool fireInitialSnapshot = false);

    /// <summary>
    /// Opens matching USB device sessions for direct read/write operations.
    /// 打开匹配的 USB 设备会话，用于直接读写操作。
    /// </summary>
    /// <param name="apiKind">The USB API selection mode. USB API 选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>A disposable collection of opened sessions. 已打开会话的可释放集合。</returns>
    UsbSessionCollection OpenUsbDeviceSessions(UsbApiKind apiKind = UsbApiKind.Auto, UsbDeviceFilter? filter = null);

    /// <summary>
    /// Registers a custom USB API provider.
    /// 注册自定义 USB API 提供器。
    /// </summary>
    /// <param name="apiName">The API name to register. 要注册的 API 名称。</param>
    /// <param name="providerFactory">Factory that creates the provider. 创建提供器实例的工厂方法。</param>
    /// <returns><c>true</c> when the provider is registered. 注册成功时返回 <c>true</c>。</returns>
    bool RegisterUsbApi(string apiName, Func<IUsbApiProvider> providerFactory);
}
