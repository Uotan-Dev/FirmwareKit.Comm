using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Core;

namespace FirmwareKit.Comm;

/// <summary>
/// Default FirmwareKit.Comm facade implementation.
/// 默认的 FirmwareKit.Comm 门面实现。
/// </summary>
public sealed class FirmwareKitComm : IFirmwareKitComm
{
    private readonly UsbCommunicationLayer _usb = new();

    /// <summary>
    /// Gets the names of the registered USB APIs.
    /// 获取已注册的 USB API 名称列表。
    /// </summary>
    /// <returns>A read-only list of available API names. 可用 API 名称只读列表。</returns>
    public IReadOnlyList<string> GetAvailableUsbApis() => _usb.GetAvailableApis();

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
    /// Registers a custom USB API provider.
    /// 注册自定义 USB API 提供器。
    /// </summary>
    /// <param name="apiName">The API name to register. 要注册的 API 名称。</param>
    /// <param name="providerFactory">Factory that creates the provider. 创建提供器实例的工厂方法。</param>
    /// <returns><c>true</c> when the provider is registered. 注册成功时返回 <c>true</c>。</returns>
    public bool RegisterUsbApi(string apiName, Func<IUsbApiProvider> providerFactory) => _usb.RegisterApi(apiName, providerFactory);
}
