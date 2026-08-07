namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Describes the observable capability profile of a USB backend.
/// <para>描述 USB 后端可观察到的能力轮廓。</para>
/// </summary>
public sealed class UsbApiCapabilities
{
    /// <summary>
    /// Gets or sets the public API name.
    /// <para>获取或设置对外 API 名称。</para>
    /// </summary>
    public string ApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backend family.
    /// <para>获取或设置后端类型。</para>
    /// </summary>
    public UsbApiKind ApiKind { get; set; }

    /// <summary>
    /// Gets or sets whether this backend is available on the current platform.
    /// <para>获取或设置该后端在当前平台是否可用。</para>
    /// </summary>
    public bool IsSupportedOnCurrentPlatform { get; set; }

    /// <summary>
    /// Gets or sets whether device metadata discovery is available natively.
    /// <para>获取或设置是否原生支持设备元数据发现。</para>
    /// </summary>
    public bool SupportsNativeDiscovery { get; set; }

    /// <summary>
    /// Gets or sets whether the backend can open direct device sessions.
    /// <para>获取或设置后端是否可以打开直接设备会话。</para>
    /// </summary>
    public bool SupportsDeviceSessions { get; set; }

    /// <summary>
    /// Gets or sets whether the backend supports USB control transfers.
    /// <para>获取或设置后端是否支持 USB 控制传输。</para>
    /// </summary>
    public bool SupportsControlTransfers { get; set; }

    /// <summary>
    /// Gets or sets whether asynchronous I/O is implemented natively by the backend.
    /// <para>获取或设置后端是否原生实现异步 I/O。</para>
    /// </summary>
    public bool SupportsNativeAsyncIo { get; set; }

    /// <summary>
    /// Gets or sets whether hot-plug notification is implemented natively.
    /// <para>获取或设置是否原生实现热插拔通知。</para>
    /// </summary>
    public bool SupportsNativeHotPlugMonitoring { get; set; }

    /// <summary>
    /// Gets or sets whether the backend requires an external runtime library.
    /// <para>获取或设置后端是否依赖外部运行时库。</para>
    /// </summary>
    public bool RequiresExternalRuntime { get; set; }

    /// <summary>
    /// Gets or sets optional notes about the backend profile.
    /// <para>获取或设置后端轮廓的可选说明。</para>
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets per-backend capability details for providers that dispatch to multiple
    /// native backends (e.g. the "native" provider across Windows / Linux / macOS).
    /// <para>获取或设置多原生后端提供器（如 Windows / Linux / macOS 的 "native"）的逐后端能力详情。</para>
    /// </summary>
    public IReadOnlyList<UsbBackendCapability>? Backends { get; set; }
}

/// <summary>
/// Describes the capability of a single native backend within a multi-backend provider.
/// <para>描述多后端提供器内部单个原生后端的能力。</para>
/// </summary>
public sealed class UsbBackendCapability
{
    /// <summary>
    /// Gets or sets the backend tag (matches <see cref="UsbTransferEvent.Backend"/>).
    /// <para>获取或设置后端标签（与 <see cref="UsbTransferEvent.Backend"/> 一致）。</para>
    /// </summary>
    public string BackendName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this backend implements asynchronous I/O natively.
    /// <para>获取或设置该后端是否原生实现异步 I/O。</para>
    /// </summary>
    public bool SupportsNativeAsyncIo { get; set; }
}

/// <summary>
/// Provides an explicit capability description for a USB API provider.
/// <para>为 USB API 提供器提供显式能力描述。</para>
/// </summary>
public interface IUsbApiCapabilityProvider
{
    /// <summary>
    /// Gets the capability profile.
    /// <para>获取能力轮廓。</para>
    /// </summary>
    /// <returns>The provider capability profile. <para>提供器能力轮廓。</para></returns>
    UsbApiCapabilities GetCapabilities();
}