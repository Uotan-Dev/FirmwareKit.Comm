using FirmwareKit.Comm.Usb.Abstractions;

namespace FirmwareKit.Comm.Usb.Core;

/// <summary>
/// Static facade over the default USB communication layer.
/// 默认 USB 通信层的静态门面。
/// </summary>
public static class UsbComm
{
    private static readonly UsbCommunicationLayer DefaultLayer = new();

    /// <summary>
    /// Gets the available API names.
    /// 获取可用 API 名称列表。
    /// </summary>
    /// <returns>A read-only list of names. 名称只读列表。</returns>
    public static IReadOnlyList<string> GetAvailableApis() => DefaultLayer.GetAvailableApis();

    /// <summary>
    /// Enumerates devices for the selected backend.
    /// 按选定后端枚举设备。
    /// </summary>
    /// <param name="apiKind">The backend selection mode. 后端选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>A read-only list of matched devices. 匹配设备只读列表。</returns>
    public static IReadOnlyList<UsbDeviceInfo> EnumerateDevices(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null) =>
        DefaultLayer.EnumerateDevices(apiKind, filter);

    /// <summary>
    /// Enumerates devices asynchronously for the selected backend.
    /// 按选定后端异步枚举设备。
    /// </summary>
    /// <param name="apiKind">The backend selection mode. 后端选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <param name="cancellationToken">A cancellation token. 取消令牌。</param>
    /// <returns>A task that resolves to the matched devices. 返回匹配设备列表的任务。</returns>
    public static Task<IReadOnlyList<UsbDeviceInfo>> EnumerateDevicesAsync(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null,
        CancellationToken cancellationToken = default) =>
        DefaultLayer.EnumerateDevicesAsync(apiKind, filter, cancellationToken);

    /// <summary>
    /// Registers a custom USB API provider.
    /// 注册自定义 USB API 提供器。
    /// </summary>
    /// <param name="apiName">The API name. API 名称。</param>
    /// <param name="providerFactory">The provider factory. 提供器工厂方法。</param>
    /// <returns><c>true</c> when the provider is registered. 注册成功时返回 <c>true</c>。</returns>
    public static bool RegisterApi(string apiName, Func<IUsbApiProvider> providerFactory) =>
        DefaultLayer.RegisterApi(apiName, providerFactory);
}
