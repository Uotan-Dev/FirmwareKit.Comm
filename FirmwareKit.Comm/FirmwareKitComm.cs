using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Core;

namespace FirmwareKit.Comm;

/// <summary>
/// Default FirmwareKit.Comm facade implementation.
/// 默认的 FirmwareKit.Comm 门面实现。
/// </summary>
public sealed class FirmwareKitComm : IFirmwareKitComm
{
    private readonly UsbCommunicationLayer _usb;

    /// <summary>
    /// Initializes a new facade with the default USB communication layer.
    /// 使用默认 USB 通信层初始化门面。
    /// </summary>
    public FirmwareKitComm()
    {
        _usb = new UsbCommunicationLayer();
    }

    /// <summary>
    /// Initializes a new facade with a caller-provided USB communication layer.
    /// 使用调用方提供的 USB 通信层初始化门面。
    /// </summary>
    /// <param name="usb">The USB communication layer. USB 通信层。</param>
    public FirmwareKitComm(UsbCommunicationLayer usb)
    {
        _usb = usb ?? throw new ArgumentNullException(nameof(usb));
    }

    /// <summary>
    /// Gets the names of the registered USB APIs.
    /// 获取已注册的 USB API 名称列表。
    /// </summary>
    /// <returns>A read-only list of available API names. 可用 API 名称只读列表。</returns>
    public IReadOnlyList<string> GetAvailableUsbApis() => _usb.GetAvailableApis();

    /// <summary>
    /// Gets capability summaries for the currently registered USB APIs.
    /// 获取当前已注册 USB API 的能力摘要。
    /// </summary>
    /// <returns>A read-only list of capability summaries. 能力摘要只读列表。</returns>
    public IReadOnlyList<UsbApiCapabilities> GetAvailableUsbApiCapabilities() => _usb.GetAvailableApiCapabilities();

    /// <summary>
    /// Enumerates USB devices for the specified API and filter.
    /// 按指定 API 与过滤条件枚举 USB 设备。
    /// </summary>
    /// <param name="apiKind">The USB API selection mode. USB API 选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>A read-only list of matched USB device descriptors. 匹配到的 USB 设备描述信息只读列表。</returns>
    public IReadOnlyList<UsbDeviceInfo> EnumerateUsbDevices(UsbApiKind apiKind = UsbApiKind.Auto, UsbDeviceFilter? filter = null) => _usb.EnumerateDevices(apiKind, filter);

    /// <summary>
    /// Enumerates USB devices asynchronously for the specified API and filter.
    /// 按指定 API 与过滤条件异步枚举 USB 设备。
    /// </summary>
    /// <param name="apiKind">The USB API selection mode. USB API 选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <param name="cancellationToken">A cancellation token. 取消令牌。</param>
    /// <returns>A task that resolves to the matched USB device descriptors. 返回匹配 USB 设备描述信息的任务。</returns>
    public Task<IReadOnlyList<UsbDeviceInfo>> EnumerateUsbDevicesAsync(UsbApiKind apiKind = UsbApiKind.Auto, UsbDeviceFilter? filter = null, CancellationToken cancellationToken = default) => _usb.EnumerateDevicesAsync(apiKind, filter, cancellationToken);

    /// <summary>
    /// Monitors USB device additions and removals by polling snapshots.
    /// 通过轮询快照监视 USB 设备新增与移除。
    /// </summary>
    /// <param name="onChanged">Change callback. 设备变化回调。</param>
    /// <param name="apiKind">The USB API selection mode. USB API 选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <param name="pollInterval">Polling interval. 轮询间隔。</param>
    /// <param name="fireInitialSnapshot">Whether to emit initial Added events. 是否触发初始 Added 事件。</param>
    /// <param name="onError">Optional error callback invoked when enumeration or callback handling fails. 枚举或回调失败时触发的可选错误回调。</param>
    /// <returns>A disposable monitor handle. 可释放的监视句柄。</returns>
    public IDisposable MonitorUsbDevices(
        Action<IReadOnlyList<UsbDeviceChange>> onChanged,
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null,
        TimeSpan? pollInterval = null,
        bool fireInitialSnapshot = false,
        Action<Exception>? onError = null) =>
        _usb.MonitorDevices(onChanged, apiKind, filter, pollInterval, fireInitialSnapshot, onError);

    /// <summary>
    /// Opens matching USB device sessions for direct read/write operations.
    /// 打开匹配的 USB 设备会话，用于直接读写操作。
    /// </summary>
    /// <param name="apiKind">The USB API selection mode. USB API 选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>A disposable collection of opened sessions. 已打开会话的可释放集合。</returns>
    public UsbSessionCollection OpenUsbDeviceSessions(UsbApiKind apiKind = UsbApiKind.Auto, UsbDeviceFilter? filter = null) => _usb.OpenDeviceSessions(apiKind, filter);

    /// <summary>
    /// Opens the first matching USB device session for direct read/write operations.
    /// 打开第一个匹配的 USB 设备会话，用于直接读写操作。
    /// </summary>
    /// <param name="apiKind">The USB API selection mode. USB API 选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>The first matching session, or <c>null</c> if none was found. 第一个匹配会话；如果没有则返回 <c>null</c>。</returns>
    public IUsbDeviceSession? OpenUsbDeviceSession(UsbApiKind apiKind = UsbApiKind.Auto, UsbDeviceFilter? filter = null) => _usb.OpenDeviceSession(apiKind, filter);

    /// <summary>
    /// Registers a custom USB API provider.
    /// 注册自定义 USB API 提供器。
    /// </summary>
    /// <param name="apiName">The API name to register. 要注册的 API 名称。</param>
    /// <param name="providerFactory">Factory that creates the provider. 创建提供器实例的工厂方法。</param>
    /// <returns><c>true</c> when the provider is registered. 注册成功时返回 <c>true</c>。</returns>
    public bool RegisterUsbApi(string apiName, Func<IUsbApiProvider> providerFactory) => _usb.RegisterApi(apiName, providerFactory);
}
