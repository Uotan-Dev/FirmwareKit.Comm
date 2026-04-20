using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Providers;

namespace FirmwareKit.Comm.Usb.Core;

/// <summary>
/// Stores and creates registered USB API providers.
/// 存储并创建已注册的 USB API 提供器。
/// </summary>
public sealed class UsbApiRegistry
{
    private readonly Dictionary<string, Func<IUsbApiProvider>> _factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Occurs when a provider is registered.
    /// 当提供器注册时触发。
    /// </summary>
    public event Action<IUsbApiProvider>? ProviderRegistered;

    /// <summary>
    /// Occurs when an API name is registered.
    /// 当 API 名称注册时触发。
    /// </summary>
    public event Action<string>? ProviderNameRegistered;

    /// <summary>
    /// Registers a provider factory under the specified API name.
    /// 在指定 API 名称下注册提供器工厂。
    /// </summary>
    /// <param name="apiName">The API name. API 名称。</param>
    /// <param name="factory">The provider factory. 提供器工厂方法。</param>
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
    /// 按名称尝试创建提供器。
    /// </summary>
    /// <param name="apiName">The API name. API 名称。</param>
    /// <param name="provider">The provider instance when successful. 成功时返回的提供器实例。</param>
    /// <returns><c>true</c> if a provider was created; otherwise, <c>false</c>. 创建成功返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public bool TryCreate(string apiName, out IUsbApiProvider? provider)
    {
        provider = null;
        if (!_factories.TryGetValue(apiName, out var factory)) return false;

        provider = factory();
        return true;
    }

    /// <summary>
    /// Creates every registered provider.
    /// 创建所有已注册的提供器实例。
    /// </summary>
    /// <returns>A read-only list of providers. 提供器只读列表。</returns>
    public IReadOnlyList<IUsbApiProvider> CreateAll()
    {
        return _factories.Values.Select(factory => factory()).ToList();
    }

    /// <summary>
    /// Gets the registered API names.
    /// 获取已注册的 API 名称列表。
    /// </summary>
    /// <returns>A read-only list of names. 名称只读列表。</returns>
    public IReadOnlyList<string> GetApiNames()
    {
        return _factories.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Creates the default registry with native and libusb providers.
    /// 创建包含 native 与 libusb 提供器的默认注册表。
    /// </summary>
    /// <returns>The default registry. 默认注册表实例。</returns>
    public static UsbApiRegistry CreateDefault()
    {
        var registry = new UsbApiRegistry();
        registry.Register(NativeUsbApiProvider.ApiNameConst, () => new NativeUsbApiProvider());
        registry.Register(LibUsbApiProvider.ApiNameConst, () => new LibUsbApiProvider());
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
