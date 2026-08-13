using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Configuration;
using FirmwareKit.Comm.Providers;

namespace FirmwareKit.Comm.Core;

/// <summary>
/// Stores and creates registered USB API providers.
/// <para>存储并创建已注册的 USB API 提供器。</para>
/// </summary>
public sealed class UsbApiRegistry
{
    private readonly Dictionary<string, Func<IUsbApiProvider>> _factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Occurs when a provider is registered.
    /// <para>当提供器注册时触发。</para>
    /// </summary>
    public event Action<IUsbApiProvider>? ProviderRegistered;

    /// <summary>
    /// Occurs when an API name is registered.
    /// <para>当 API 名称注册时触发。</para>
    /// </summary>
    public event Action<string>? ProviderNameRegistered;

    /// <summary>
    /// Registers a provider factory under the specified API name.
    /// <para>在指定 API 名称下注册提供器工厂。</para>
    /// </summary>
    /// <param name="apiName">The API name. <para>API 名称。</para></param>
    /// <param name="factory">The provider factory. <para>提供器工厂方法。</para></param>
    public void Register(string apiName, Func<IUsbApiProvider> factory)
    {
        if (string.IsNullOrWhiteSpace(apiName))
        {
            throw new ArgumentException("API name cannot be null or whitespace.", nameof(apiName));
        }

        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        _factories[apiName] = factory;
        ProviderNameRegistered?.Invoke(apiName);
        ProviderRegistered?.Invoke(new RegisteredProviderPlaceholder(apiName));
    }

    /// <summary>
    /// Tries to create a provider by name.
    /// <para>按名称尝试创建提供器。</para>
    /// </summary>
    /// <param name="apiName">The API name. <para>API 名称。</para></param>
    /// <param name="provider">The provider instance when successful. <para>成功时返回的提供器实例。</para></param>
    /// <returns><c>true</c> if a provider was created; otherwise, <c>false</c>. <para>创建成功返回 <c>true</c>，否则返回 <c>false</c>。</para></returns>
    public bool TryCreate(string apiName, out IUsbApiProvider? provider)
    {
        provider = null;
        if (!_factories.TryGetValue(apiName, out var factory)) return false;

        provider = factory();
        return true;
    }

    /// <summary>
    /// Creates every registered provider.
    /// <para>创建所有已注册的提供器实例。</para>
    /// </summary>
    /// <returns>A read-only list of providers. <para>提供器只读列表。</para></returns>
    public IReadOnlyList<IUsbApiProvider> CreateAll()
    {
        return _factories.Values.Select(factory => factory()).ToList();
    }

    /// <summary>
    /// Gets the registered API names.
    /// <para>获取已注册的 API 名称列表。</para>
    /// </summary>
    /// <returns>A read-only list of names. <para>名称只读列表。</para></returns>
    public IReadOnlyList<string> GetApiNames()
    {
        return _factories.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Creates the default registry with native, libusb and harmony providers,
    /// registered in the order prescribed by <see cref="UsbBackendConfiguration"/>
    /// for the current platform (macOS prefers libusb, Windows prefers native,
    /// and the native backend is a fallback / enumeration-only path elsewhere).
    /// <para>创建包含 native、libusb 与 harmony 提供器的默认注册表，并按
    /// <see cref="UsbBackendConfiguration"/> 为当前平台规定的顺序注册
    /// （macOS 优先 libusb，Windows 优先原生，其余平台原生后端仅作回退/枚举）。</para>
    /// </summary>
    /// <returns>The default registry. <para>默认注册表实例。</para></returns>
    public static UsbApiRegistry CreateDefault()
    {
        var registry = new UsbApiRegistry();

        var priority = UsbBackendConfiguration.ForCurrentPlatform.ResolveAvailableBackends();
        var registered = new HashSet<UsbApiKind>();

        foreach (UsbApiKind kind in priority)
        {
            switch (kind)
            {
                case UsbApiKind.Native:
                    registry.Register(NativeUsbApiProvider.ApiNameConst, () => new NativeUsbApiProvider());
                    registered.Add(UsbApiKind.Native);
                    break;
                case UsbApiKind.LibUsbDotNet:
                    registry.Register(LibUsbApiProvider.ApiNameConst, () => new LibUsbApiProvider());
                    registered.Add(UsbApiKind.LibUsbDotNet);
                    break;
                case UsbApiKind.HarmonyOS:
                    registry.Register(HarmonyOSUsbApiProvider.ApiNameConst, () => new HarmonyOSUsbApiProvider());
                    registered.Add(UsbApiKind.HarmonyOS);
                    break;
            }
        }

        // Ensure every known backend is registered even when not listed in the
        // platform configuration (e.g. opt-in HarmonyOS), so explicit API-kind
        // selection still works.
        // <para>确保每个已知后端都被注册，即使未列入平台配置（如显式开启的
        // HarmonyOS），使显式按 API 类型选择仍可用。</para>
        if (!registered.Contains(UsbApiKind.Native))
        {
            registry.Register(NativeUsbApiProvider.ApiNameConst, () => new NativeUsbApiProvider());
        }
        if (!registered.Contains(UsbApiKind.LibUsbDotNet))
        {
            registry.Register(LibUsbApiProvider.ApiNameConst, () => new LibUsbApiProvider());
        }
        if (!registered.Contains(UsbApiKind.HarmonyOS))
        {
            registry.Register(HarmonyOSUsbApiProvider.ApiNameConst, () => new HarmonyOSUsbApiProvider());
        }

        return registry;
    }

    private sealed class RegisteredProviderPlaceholder : IUsbApiProvider
    {
        public RegisteredProviderPlaceholder(string apiName)
        {
            ApiName = apiName;
        }

        public string ApiName { get; }

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
        {
            return Array.Empty<IUsbDeviceSession>();
        }
    }
}
