using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Backend;
using FirmwareKit.Comm.Core;
using FirmwareKit.Comm.Diagnostics;

namespace FirmwareKit.Comm.Providers;

/// <summary>
/// Shared template for USB API providers: enumeration → projection → capabilities.
/// <para>USB API 提供器的共享模板：枚举 → 投影 → 能力。</para>
/// Subclasses supply the backend identity, support check, backend enumeration and the
/// capability profile; the common session/info projection is implemented here once.
/// <para>子类提供后端身份、支持判定、后端枚举与能力轮廓；通用的会话/信息投影在此实现一次。</para>
/// </summary>
internal abstract class UsbApiProviderBase : IUsbApiProvider, IUsbApiDiscoveryProvider, IUsbApiCapabilityProvider
{
    /// <inheritdoc/>
    public abstract string ApiName { get; }

    /// <inheritdoc/>
    public abstract UsbApiKind ApiKind { get; }

    /// <inheritdoc/>
    public abstract bool IsSupportedOnCurrentPlatform { get; }

    /// <summary>
    /// Enumerates backend devices applying the optional filter.
    /// <para>使用可选过滤器枚举后端设备。</para>
    /// </summary>
    /// <param name="filter">Optional device filter. <para>可选设备过滤器。</para></param>
    /// <returns>The discovered backend devices. <para>发现的后端设备。</para></returns>
    protected abstract List<UsbDevice> EnumerateBackendDevices(UsbDeviceFilter? filter);

    /// <inheritdoc/>
    public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
    {
        if (!IsSupportedOnCurrentPlatform) return Array.Empty<IUsbDeviceSession>();

        var devices = EnumerateBackendDevices(filter);
        return UsbProviderProjection.ToSessions(ApiName, ApiKind, devices, filter);
    }

    /// <inheritdoc/>
    public IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null)
    {
        if (!IsSupportedOnCurrentPlatform) return Array.Empty<UsbDeviceInfo>();

        var devices = EnumerateBackendDevices(filter);
        return UsbProviderProjection.ToInfos(ApiName, ApiKind, devices, filter);
    }

    /// <inheritdoc/>
    public abstract UsbApiCapabilities GetCapabilities();

    /// <summary>
    /// Runs a backend enumeration swallowing expected availability failures
    /// (missing native runtime, init failure) and logging them instead of throwing.
    /// <para>执行后端枚举，吞掉预期的可用性失败（缺少原生运行库、初始化失败）并记录日志。</para>
    /// </summary>
    /// <param name="enumerate">The enumeration delegate. <para>枚举委托。</para></param>
    /// <param name="backendTag">Backend tag used in log messages. <para>日志中使用的后端标签。</para></param>
    /// <returns>The enumerated devices, or an empty list on availability failure. <para>枚举到的设备；可用性失败时返回空列表。</para></returns>
    protected static List<UsbDevice> SafeEnumerate(Func<List<UsbDevice>> enumerate, string backendTag)
    {
        try
        {
            return enumerate();
        }
        catch (DllNotFoundException ex)
        {
            UsbTrace.Log($"{backendTag} backend unavailable: {ex.Message}");
            return new List<UsbDevice>();
        }
        catch (TypeInitializationException ex)
        {
            UsbTrace.Log($"{backendTag} initialization failed: {ex.Message}");
            return new List<UsbDevice>();
        }
        catch
        {
            UsbTrace.Log($"{backendTag} enumeration failed with unknown exception.");
            return new List<UsbDevice>();
        }
    }
}
