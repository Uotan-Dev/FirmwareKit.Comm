using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;
using FirmwareKit.Comm.Usb.Providers;

namespace FirmwareKit.Comm.Usb.Core;

/// <summary>
/// Provides USB discovery and API registration operations.
/// 提供 USB 发现与 API 注册能力。
/// </summary>
public sealed class UsbCommunicationLayer
{
    private readonly UsbApiRegistry _registry;

    /// <summary>
    /// Initializes a new communication layer.
    /// 初始化新的通信层。
    /// </summary>
    /// <param name="registry">Optional registry to use. 可选注册表实例。</param>
    public UsbCommunicationLayer(UsbApiRegistry? registry = null)
    {
        _registry = registry ?? UsbApiRegistry.CreateDefault();
    }

    /// <summary>
    /// Gets the available API names.
    /// 获取可用 API 名称列表。
    /// </summary>
    /// <returns>A read-only list of names. 名称只读列表。</returns>
    public IReadOnlyList<string> GetAvailableApis() => _registry.GetApiNames();

    /// <summary>
    /// Enumerates devices for the selected backend.
    /// 按选定后端枚举设备。
    /// </summary>
    /// <param name="apiKind">The backend selection mode. 后端选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>A read-only list of matched devices. 匹配设备只读列表。</returns>
    public IReadOnlyList<UsbDeviceInfo> EnumerateDevices(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null)
    {
        return EnumerateDevicesCore(apiKind, filter, cancellationToken: default);
    }

    /// <summary>
    /// Enumerates devices for the selected backend with cancellation support.
    /// 按选定后端枚举设备，并支持取消。
    /// </summary>
    /// <param name="apiKind">The backend selection mode. 后端选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <param name="cancellationToken">A cancellation token. 取消令牌。</param>
    /// <returns>A read-only list of matched devices. 匹配设备只读列表。</returns>
    public IReadOnlyList<UsbDeviceInfo> EnumerateDevices(
        UsbApiKind apiKind,
        UsbDeviceFilter? filter,
        CancellationToken cancellationToken)
    {
        return EnumerateDevicesCore(apiKind, filter, cancellationToken);
    }

    private IReadOnlyList<UsbDeviceInfo> EnumerateDevicesCore(
        UsbApiKind apiKind,
        UsbDeviceFilter? filter,
        CancellationToken cancellationToken)
    {
        var providers = ResolveProviders(apiKind);
        var devices = new List<UsbDeviceInfo>();

        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!provider.IsSupportedOnCurrentPlatform)
            {
                continue;
            }

            if (provider is IUsbApiDiscoveryProvider discoveryProvider)
            {
                devices.AddRange(discoveryProvider.EnumerateDeviceInfos(filter));
                continue;
            }

            using var sessions = new UsbSessionCollection(provider.EnumerateDeviceSessions(filter));
            devices.AddRange(sessions.Sessions.Select(session => session.DeviceInfo));
        }

        return devices;
    }

    /// <summary>
    /// Enumerates devices asynchronously for the selected backend.
    /// 按选定后端异步枚举设备。
    /// </summary>
    /// <param name="apiKind">The backend selection mode. 后端选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <param name="cancellationToken">A cancellation token. 取消令牌。</param>
    /// <returns>A task that resolves to the matched devices. 返回匹配设备列表的任务。</returns>
    public async Task<IReadOnlyList<UsbDeviceInfo>> EnumerateDevicesAsync(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return EnumerateDevicesCore(apiKind, filter, cancellationToken);
    }

    /// <summary>
    /// Enumerates devices and invokes a callback for each match.
    /// 枚举设备并对每个匹配项执行回调。
    /// </summary>
    /// <param name="onDeviceFound">Callback invoked per device. 每个设备匹配时触发的回调。</param>
    /// <param name="apiKind">The backend selection mode. 后端选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    public void EnumerateDevices(
        Action<UsbDeviceInfo> onDeviceFound,
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null)
    {
        if (onDeviceFound == null)
        {
            throw new ArgumentNullException(nameof(onDeviceFound));
        }

        var devices = EnumerateDevices(apiKind, filter);
        foreach (var device in devices)
        {
            onDeviceFound(device);
        }
    }

    /// <summary>
    /// Monitors USB device additions and removals by polling snapshots.
    /// 通过轮询快照监视 USB 设备新增与移除。
    /// </summary>
    /// <param name="onChanged">Change callback. 设备变化回调。</param>
    /// <param name="apiKind">The backend selection mode. 后端选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <param name="pollInterval">Polling interval. 轮询间隔。</param>
    /// <param name="fireInitialSnapshot">Whether to emit initial Added events. 是否触发初始 Added 事件。</param>
    /// <returns>A disposable monitor handle. 可释放的监视句柄。</returns>
    public IDisposable MonitorDevices(
        Action<IReadOnlyList<UsbDeviceChange>> onChanged,
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null,
        TimeSpan? pollInterval = null,
        bool fireInitialSnapshot = false)
    {
        if (onChanged == null)
        {
            throw new ArgumentNullException(nameof(onChanged));
        }

        var interval = pollInterval ?? TimeSpan.FromSeconds(1);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }

        return new UsbDeviceMonitor(
            () =>
            {
                try
                {
                    return EnumerateDevicesCore(apiKind, filter, cancellationToken: default);
                }
                catch (Exception ex)
                {
                    UsbTrace.Log($"MonitorDevices enumerate failed: {ex.GetType().Name}: {ex.Message}");
                    return Array.Empty<UsbDeviceInfo>();
                }
            },
            onChanged,
            interval,
            fireInitialSnapshot);
    }

    /// <summary>
    /// Opens matching device sessions for the selected backend.
    /// 为选定后端打开匹配的设备会话。
    /// </summary>
    /// <param name="apiKind">The backend selection mode. 后端选择模式。</param>
    /// <param name="filter">Optional device filter. 可选设备过滤器。</param>
    /// <returns>A wrapped collection of sessions. 封装后的会话集合。</returns>
    public UsbSessionCollection OpenDeviceSessions(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null)
    {
        var providers = ResolveProviders(apiKind);
        var sessions = new List<IUsbDeviceSession>();

        foreach (var provider in providers)
        {
            if (!provider.IsSupportedOnCurrentPlatform) continue;
            sessions.AddRange(provider.EnumerateDeviceSessions(filter));
        }

        return new UsbSessionCollection(sessions);
    }

    /// <summary>
    /// Registers a custom USB API provider.
    /// 注册自定义 USB API 提供器。
    /// </summary>
    /// <param name="apiName">The API name. API 名称。</param>
    /// <param name="providerFactory">The provider factory. 提供器工厂方法。</param>
    /// <returns><c>true</c> when the provider is registered. 注册成功时返回 <c>true</c>。</returns>
    public bool RegisterApi(string apiName, Func<IUsbApiProvider> providerFactory)
    {
        if (string.IsNullOrWhiteSpace(apiName))
        {
            throw new ArgumentException("API name cannot be null or whitespace.", nameof(apiName));
        }

        if (providerFactory == null)
        {
            throw new ArgumentNullException(nameof(providerFactory));
        }

        _registry.Register(apiName, providerFactory);
        return true;
    }

    private IReadOnlyList<IUsbApiProvider> ResolveProviders(UsbApiKind apiKind)
    {
        if (apiKind == UsbApiKind.Auto)
        {
            return _registry.CreateAll();
        }

        var apiName = apiKind switch
        {
            UsbApiKind.Native => NativeUsbApiProvider.ApiNameConst,
            UsbApiKind.LibUsbDotNet => LibUsbApiProvider.ApiNameConst,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(apiName)) return Array.Empty<IUsbApiProvider>();

        if (_registry.TryCreate(apiName, out var provider) && provider != null)
        {
            return new[] { provider };
        }

        return Array.Empty<IUsbApiProvider>();
    }
}
