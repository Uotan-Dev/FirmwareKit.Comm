using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;
using FirmwareKit.Comm.Usb.Providers;

namespace FirmwareKit.Comm.Usb.Core;

/// <summary>
/// Provides USB discovery and API registration operations.
/// <para>提供 USB 发现与 API 注册能力。</para>
/// </summary>
public sealed class UsbCommunicationLayer
{
    private readonly UsbApiRegistry _registry;

    /// <summary>
    /// Initializes a new communication layer.
    /// <para>初始化新的通信层。</para>
    /// </summary>
    /// <param name="registry">Optional registry to use. <para>可选注册表实例。</para></param>
    public UsbCommunicationLayer(UsbApiRegistry? registry = null)
    {
        _registry = registry ?? UsbApiRegistry.CreateDefault();
    }

    /// <summary>
    /// Gets the available API names.
    /// <para>获取可用 API 名称列表。</para>
    /// </summary>
    /// <returns>A read-only list of names. <para>名称只读列表。</para></returns>
    public IReadOnlyList<string> GetAvailableApis() => _registry.GetApiNames();

    /// <summary>
    /// Gets capability summaries for the currently registered USB APIs.
    /// <para>获取当前已注册 USB API 的能力摘要。</para>
    /// </summary>
    /// <returns>A read-only list of capability summaries. <para>能力摘要只读列表。</para></returns>
    public IReadOnlyList<UsbApiCapabilities> GetAvailableApiCapabilities()
    {
        return ResolveProviders(UsbApiKind.Auto)
            .Select(CreateCapabilities)
            .ToList();
    }

    /// <summary>
    /// Enumerates devices for the selected backend.
    /// <para>按选定后端枚举设备。</para>
    /// </summary>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <returns>A read-only list of matched devices. <para>匹配设备只读列表。</para></returns>
    public IReadOnlyList<UsbDeviceInfo> EnumerateDevices(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null)
    {
        return EnumerateDevicesCore(apiKind, filter, cancellationToken: default);
    }

    /// <summary>
    /// Enumerates devices for the selected backend with cancellation support.
    /// <para>按选定后端枚举设备，并支持取消。</para>
    /// </summary>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A read-only list of matched devices. <para>匹配设备只读列表。</para></returns>
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
    /// <para>按选定后端异步枚举设备。</para>
    /// </summary>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <param name="cancellationToken">A cancellation token. <para>取消令牌。</para></param>
    /// <returns>A task that resolves to the matched devices. <para>返回匹配设备列表的任务。</para></returns>
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
    /// <para>枚举设备并对每个匹配项执行回调。</para>
    /// </summary>
    /// <param name="onDeviceFound">Callback invoked per device. <para>每个设备匹配时触发的回调。</para></param>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
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
    /// <para>通过轮询快照监视 USB 设备新增与移除。</para>
    /// </summary>
    /// <param name="onChanged">Change callback. <para>设备变化回调。</para></param>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <param name="pollInterval">Polling interval. <para>轮询间隔。</para></param>
    /// <param name="fireInitialSnapshot">Whether to emit initial Added events. <para>是否触发初始 Added 事件。</para></param>
    /// <param name="onError">Optional error callback invoked when enumeration or callback handling fails. <para>枚举或回调失败时触发的可选错误回调。</para></param>
    /// <returns>A disposable monitor handle. <para>可释放的监视句柄。</para></returns>
    public IDisposable MonitorDevices(
        Action<IReadOnlyList<UsbDeviceChange>> onChanged,
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null,
        TimeSpan? pollInterval = null,
        bool fireInitialSnapshot = false,
        Action<Exception>? onError = null)
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
                    onError?.Invoke(ex);
                    UsbTrace.Log($"MonitorDevices enumerate failed: {ex.GetType().Name}: {ex.Message}");
                    return Array.Empty<UsbDeviceInfo>();
                }
            },
            onChanged,
            onError,
            interval,
            fireInitialSnapshot);
    }

    /// <summary>
    /// Opens matching device sessions for the selected backend.
    /// <para>为选定后端打开匹配的设备会话。</para>
    /// </summary>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <returns>A wrapped collection of sessions. <para>封装后的会话集合。</para></returns>
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
    /// Opens the first matching USB device session for the selected backend.
    /// <para>打开选定后端中第一个匹配的 USB 设备会话。</para>
    /// </summary>
    /// <param name="apiKind">The backend selection mode. <para>后端选择模式。</para></param>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <returns>The first matching session, or <c>null</c> if none was found. <para>返回第一个匹配会话；如果没有则返回 <c>null</c>。</para></returns>
    public IUsbDeviceSession? OpenDeviceSession(
        UsbApiKind apiKind = UsbApiKind.Auto,
        UsbDeviceFilter? filter = null)
    {
        var providers = ResolveProviders(apiKind);

        foreach (var provider in providers)
        {
            if (!provider.IsSupportedOnCurrentPlatform)
            {
                continue;
            }

            var sessions = provider.EnumerateDeviceSessions(filter);
            if (sessions.Count == 0)
            {
                continue;
            }

            var firstSession = sessions[0];
            for (var index = 1; index < sessions.Count; index++)
            {
                sessions[index].Dispose();
            }

            return firstSession;
        }

        return null;
    }

    /// <summary>
    /// Registers a custom USB API provider.
    /// <para>注册自定义 USB API 提供器。</para>
    /// </summary>
    /// <param name="apiName">The API name. <para>API 名称。</para></param>
    /// <param name="providerFactory">The provider factory. <para>提供器工厂方法。</para></param>
    /// <returns><c>true</c> when the provider is registered. <para>注册成功时返回 <c>true</c>。</para></returns>
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
            UsbApiKind.HarmonyOS => HarmonyOSUsbApiProvider.ApiNameConst,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(apiName)) return Array.Empty<IUsbApiProvider>();

        if (_registry.TryCreate(apiName, out var provider) && provider != null)
        {
            return new[] { provider };
        }

        return Array.Empty<IUsbApiProvider>();
    }

    private static UsbApiCapabilities CreateCapabilities(IUsbApiProvider provider)
    {
        if (provider is IUsbApiCapabilityProvider capabilityProvider)
        {
            return capabilityProvider.GetCapabilities();
        }

        return new UsbApiCapabilities
        {
            ApiName = provider.ApiName,
            ApiKind = provider.ApiKind,
            IsSupportedOnCurrentPlatform = provider.IsSupportedOnCurrentPlatform,
            SupportsNativeDiscovery = provider is IUsbApiDiscoveryProvider,
            SupportsDeviceSessions = true,
            SupportsControlTransfers = true,
            SupportsNativeAsyncIo = false,
            SupportsNativeHotPlugMonitoring = false,
            RequiresExternalRuntime = false,
            Notes = "Capability data was inferred because the provider does not implement IUsbApiCapabilityProvider."
        };
    }
}
