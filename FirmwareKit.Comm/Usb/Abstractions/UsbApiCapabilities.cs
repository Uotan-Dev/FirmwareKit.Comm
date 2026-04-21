namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Describes the observable capability profile of a USB backend.
/// 描述 USB 后端可观察到的能力轮廓。
/// </summary>
public sealed class UsbApiCapabilities
{
    /// <summary>
    /// Gets or sets the public API name.
    /// 获取或设置对外 API 名称。
    /// </summary>
    public string ApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backend family.
    /// 获取或设置后端类型。
    /// </summary>
    public UsbApiKind ApiKind { get; set; }

    /// <summary>
    /// Gets or sets whether this backend is available on the current platform.
    /// 获取或设置该后端在当前平台是否可用。
    /// </summary>
    public bool IsSupportedOnCurrentPlatform { get; set; }

    /// <summary>
    /// Gets or sets whether device metadata discovery is available natively.
    /// 获取或设置是否原生支持设备元数据发现。
    /// </summary>
    public bool SupportsNativeDiscovery { get; set; }

    /// <summary>
    /// Gets or sets whether the backend can open direct device sessions.
    /// 获取或设置后端是否可以打开直接设备会话。
    /// </summary>
    public bool SupportsDeviceSessions { get; set; }

    /// <summary>
    /// Gets or sets whether the backend supports USB control transfers.
    /// 获取或设置后端是否支持 USB 控制传输。
    /// </summary>
    public bool SupportsControlTransfers { get; set; }

    /// <summary>
    /// Gets or sets whether asynchronous I/O is implemented natively by the backend.
    /// 获取或设置后端是否原生实现异步 I/O。
    /// </summary>
    public bool SupportsNativeAsyncIo { get; set; }

    /// <summary>
    /// Gets or sets whether hot-plug notification is implemented natively.
    /// 获取或设置是否原生实现热插拔通知。
    /// </summary>
    public bool SupportsNativeHotPlugMonitoring { get; set; }

    /// <summary>
    /// Gets or sets whether the backend requires an external runtime library.
    /// 获取或设置后端是否依赖外部运行时库。
    /// </summary>
    public bool RequiresExternalRuntime { get; set; }

    /// <summary>
    /// Gets or sets optional notes about the backend profile.
    /// 获取或设置后端轮廓的可选说明。
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Provides an explicit capability description for a USB API provider.
/// 为 USB API 提供器提供显式能力描述。
/// </summary>
public interface IUsbApiCapabilityProvider
{
    /// <summary>
    /// Gets the capability profile.
    /// 获取能力轮廓。
    /// </summary>
    /// <returns>The provider capability profile. 提供器能力轮廓。</returns>
    UsbApiCapabilities GetCapabilities();
}