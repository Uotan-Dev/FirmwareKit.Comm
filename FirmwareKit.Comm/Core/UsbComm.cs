using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Core;

/// <summary>
/// Static facade over the default USB communication layer.
/// <para>默认 USB 通信层的静态门面。</para>
/// </summary>
public static class UsbComm
{
    private static readonly UsbCommunicationLayer DefaultLayer = new();

    /// <summary>
    /// Gets the names of the APIs that are supported on the current platform.
    /// <para>获取当前平台上受支持的 API 名称列表。</para>
    /// Only providers whose <see cref="IUsbApiProvider.IsSupportedOnCurrentPlatform"/>
    /// is <c>true</c> are listed; unavailable providers (e.g. the opt-in HarmonyOS
    /// DDK backend) stay hidden.
    /// <para>仅列出 <see cref="IUsbApiProvider.IsSupportedOnCurrentPlatform"/> 为 <c>true</c>
    /// 的提供器；不可用的提供器（例如需显式开启的 HarmonyOS DDK 后端）不会出现。</para>
    /// </summary>
    /// <returns>A read-only list of names. <para>名称只读列表。</para></returns>
    public static IReadOnlyList<string> GetAvailableApis() => DefaultLayer.GetAvailableApis();

    /// <summary>
    /// Gets capability summaries for the currently registered USB APIs.
    /// <para>获取当前已注册 USB API 的能力摘要。</para>
    /// </summary>
    /// <returns>A read-only list of capability summaries. <para>能力摘要只读列表。</para></returns>
    public static IReadOnlyList<UsbApiCapabilities> GetAvailableApiCapabilities() => DefaultLayer.GetAvailableApiCapabilities();

    /// <summary>
    /// Enumerates devices for the selected backend.
    /// <para>按选定后端枚举设备。</para>
    /// </summary>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <returns>A read-only list of matched devices. <para>匹配设备只读列表。</para></returns>
    public static IReadOnlyList<UsbDeviceInfo> EnumerateDevices(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null) =>
        DefaultLayer.EnumerateDevices(apiKind, filter);

    /// <summary>
    /// Enumerates devices asynchronously for the selected backend.
    /// <para>按选定后端异步枚举设备。</para>
    /// </summary>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the matched devices. <para>返回匹配设备列表的任务。</para></returns>
    public static Task<IReadOnlyList<UsbDeviceInfo>> EnumerateDevicesAsync(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null,
        CancellationToken cancellationToken = default) =>
        DefaultLayer.EnumerateDevicesAsync(apiKind, filter, cancellationToken);

    /// <summary>
    /// Monitors USB device additions and removals by polling snapshots.
    /// <para>通过轮询快照监视 USB 设备新增与移除。</para>
    /// </summary>
    /// <param name="onChanged">Change callback. <para>设备变化回调。</para></param>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <param name="pollInterval">Polling interval. <para>轮询间隔。</para></param>
    /// <param name="fireInitialSnapshot">Whether to emit initial Added events. <para>是否触发初始 Added 事件。</para></param>
    /// <param name="onError">Optional error callback invoked when enumeration or callback handling fails. <para>枚举或回调失败时触发的可选错误回调。</para></param>
    /// <returns>A disposable monitor handle. <para>可释放的监视句柄。</para></returns>
    public static IDisposable MonitorDevices(
        Action<IReadOnlyList<UsbDeviceChange>> onChanged,
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null,
        TimeSpan? pollInterval = null,
        bool fireInitialSnapshot = false,
        Action<Exception>? onError = null) =>
        DefaultLayer.MonitorDevices(onChanged, apiKind, filter, pollInterval, fireInitialSnapshot, onError);

    /// <summary>
    /// Opens matching device sessions for direct read/write operations.
    /// <para>打开匹配设备会话，用于直接读写操作。</para>
    /// </summary>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <returns>A wrapped collection of opened sessions. <para>封装后的已打开会话集合。</para></returns>
    public static UsbSessionCollection OpenDeviceSessions(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null) =>
        DefaultLayer.OpenDeviceSessions(apiKind, filter);

    /// <summary>
    /// Opens the session whose <see cref="UsbDeviceInfo.DeviceKey"/> matches the specified key.
    /// <para>打开 <see cref="UsbDeviceInfo.DeviceKey"/> 与指定键匹配的设备会话。</para>
    /// Protocol layers that stash a device's key (e.g. after a mode switch like adb →
    /// fastboot → EDL where VID/PID change) can reopen the same physical device by key.
    /// <para>协议层保存设备键后（例如 adb → fastboot → EDL 模式切换导致 VID/PID 变化），
    /// 可按键重开同一物理设备。</para>
    /// </summary>
    /// <param name="deviceKey">The stable device key from <see cref="UsbDeviceInfo.DeviceKey"/>. <para>来自 <see cref="UsbDeviceInfo.DeviceKey"/> 的稳定设备键。</para></param>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <returns>The matching session, or <c>null</c> when no device matches. <para>匹配的会话；无匹配设备时返回 <c>null</c>。</para></returns>
    public static IUsbDeviceSession? OpenDeviceSessionByKey(
        string deviceKey,
        UsbApiKind apiKind = UsbApiKind.Auto) =>
        DefaultLayer.OpenDeviceSessionByKey(deviceKey, apiKind);

    /// <summary>
    /// Registers a custom USB API provider.
    /// <para>注册自定义 USB API 提供器。</para>
    /// </summary>
    /// <param name="apiName">The API name. <para>API 名称。</para></param>
    /// <param name="providerFactory">The provider factory. <para>提供器工厂方法。</para></param>
    /// <returns><c>true</c> when the provider is registered. <para>注册成功时返回 <c>true</c>。</para></returns>
    public static bool RegisterApi(string apiName, Func<IUsbApiProvider> providerFactory) =>
        DefaultLayer.RegisterApi(apiName, providerFactory);
}
