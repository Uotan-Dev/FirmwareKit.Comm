using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Configuration;

/// <summary>
/// Platform USB backend configuration: the default backend and the ordered list
/// of available backends for each operating system. The defaults are aligned
/// with Google adb's backend selection (<c>transport_usb.cpp is_libusb_enabled</c>):
/// non-Windows platforms prefer libusb, Windows prefers the native backend, and
/// the native IOKit/WinUSB/USBFS backend acts as a fallback or for enumeration
/// only. Environment variables can override the defaults per the official adb
/// semantics (<c>ADB_LIBUSB</c>) plus a project-specific selector.
/// <para>平台 USB 后端配置：每个操作系统的默认后端与可用后端的有序列表。默认值
/// 与谷歌 adb 的后端选择对齐（<c>transport_usb.cpp is_libusb_enabled</c>）：
/// 非 Windows 平台优先 libusb，Windows 优先原生后端，原生 IOKit/WinUSB/USBFS
/// 后端仅作回退或仅用于枚举。环境变量可按官方 adb 语义（<c>ADB_LIBUSB</c>）
/// 覆盖默认值，另有项目专属的选择器。</para>
/// </summary>
public sealed class UsbBackendConfiguration
{
    // Google adb uses the ADB_LIBUSB env var to toggle the libusb transport
    // (is_libusb_enabled): "1" forces libusb, any other value disables it.
    // <para>谷歌 adb 用 ADB_LIBUSB 环境变量切换 libusb 传输（is_libusb_enabled）：
    // "1" 强制 libusb，任何其他值禁用。</para>
    private const string AdbLibUsbEnvVar = "ADB_LIBUSB";

    // Project-specific selector: "native", "libusb", or "auto" (platform default).
    // <para>项目专属选择器："native"、"libusb" 或 "auto"（平台默认）。</para>
    private const string FirmwareKitBackendEnvVar = "FIRMWAREKIT_USB_BACKEND";

    private UsbBackendConfiguration(UsbApiKind defaultBackend, IReadOnlyList<UsbApiKind> availableBackends)
    {
        DefaultBackend = defaultBackend;
        AvailableBackends = availableBackends;
    }

    /// <summary>
    /// The platform's default backend before any environment override.
    /// <para>任何环境变量覆盖之前的平台默认后端。</para>
    /// </summary>
    public UsbApiKind DefaultBackend { get; }

    /// <summary>
    /// The ordered list of backends available on this platform, from most to
    /// least preferred. The native backend is always last so it acts purely as a
    /// fallback (or for enumeration) when the preferred backend is unavailable.
    /// <para>本平台可用后端的顺序列表，从最优先到最不优先。原生后端始终位于末尾，
    /// 以便在首选后端不可用时纯作回退（或仅用于枚举）。</para>
    /// </summary>
    public IReadOnlyList<UsbApiKind> AvailableBackends { get; }

    /// <summary>
    /// Gets the backend configuration for the current runtime platform.
    /// <para>获取当前运行时平台的后端配置。</para>
    /// </summary>
    public static UsbBackendConfiguration ForCurrentPlatform { get; } = Create(GetCurrentPlatform());

    /// <summary>
    /// Gets the backend configuration for the specified platform.
    /// <para>获取指定平台的后端配置。</para>
    /// </summary>
    /// <param name="platform">The target platform. <para>目标平台。</para></param>
    public static UsbBackendConfiguration ForPlatform(OSPlatform platform) => Create(platform);

    /// <summary>
    /// Resolves the effective default backend after applying environment
    /// overrides (<c>ADB_LIBUSB</c>, <c>FIRMWAREKIT_USB_BACKEND</c>).
    /// <para>应用环境变量覆盖（<c>ADB_LIBUSB</c>、<c>FIRMWAREKIT_USB_BACKEND</c>）
    /// 后解析有效的默认后端。</para>
    /// </summary>
    public UsbApiKind ResolveDefaultBackend()
    {
        IReadOnlyList<UsbApiKind> effective = ResolveAvailableBackends();
        return effective.Count > 0 ? effective[0] : DefaultBackend;
    }

    /// <summary>
    /// Resolves the effective backend list after applying environment overrides:
    /// <list type="bullet">
    /// <item><c>FIRMWAREKIT_USB_BACKEND=native</c> → native only</item>
    /// <item><c>FIRMWAREKIT_USB_BACKEND=libusb</c> → libusb only</item>
    /// <item><c>ADB_LIBUSB=1</c> → libusb first, native fallback</item>
    /// <item><c>ADB_LIBUSB=0</c> → native only</item>
    /// <item>otherwise → the platform defaults</item>
    /// </list>
    /// <para>应用环境变量覆盖后解析有效的后端列表：</para>
    /// <list type="bullet">
    /// <item><c>FIRMWAREKIT_USB_BACKEND=native</c> → 仅原生</item>
    /// <item><c>FIRMWAREKIT_USB_BACKEND=libusb</c> → 仅 libusb</item>
    /// <item><c>ADB_LIBUSB=1</c> → libusb 优先，原生回退</item>
    /// <item><c>ADB_LIBUSB=0</c> → 仅原生</item>
    /// <item>否则 → 平台默认</item>
    /// </list>
    /// </summary>
    public IReadOnlyList<UsbApiKind> ResolveAvailableBackends()
    {
        string? projectSelector = Environment.GetEnvironmentVariable(FirmwareKitBackendEnvVar);
        if (!string.IsNullOrWhiteSpace(projectSelector))
        {
            switch (projectSelector.Trim().ToLowerInvariant())
            {
                case "native":
                    return new[] { UsbApiKind.Native };
                case "libusb":
                    return new[] { UsbApiKind.LibUsbDotNet };
                case "auto":
                    return AvailableBackends;
                default:
                    // Unknown value: fall back to the platform defaults.
                    // <para>未知值：回退到平台默认。</para>
                    return AvailableBackends;
            }
        }

        string? adbLibUsb = Environment.GetEnvironmentVariable(AdbLibUsbEnvVar);
        if (adbLibUsb != null)
        {
            // ADB_LIBUSB=1 forces libusb; any other value disables it (matches
            // is_libusb_enabled: strcmp(env, "1") == 0).
            // <para>ADB_LIBUSB=1 强制 libusb；任何其他值禁用（与 is_libusb_enabled
            // 一致：strcmp(env, "1") == 0）。</para>
            if (string.Equals(adbLibUsb.Trim(), "1", StringComparison.Ordinal))
            {
                return ReorderLibUsbFirst();
            }
            return new[] { UsbApiKind.Native };
        }

        return AvailableBackends;
    }

    private IReadOnlyList<UsbApiKind> ReorderLibUsbFirst()
    {
        var result = new List<UsbApiKind>(AvailableBackends.Count);
        if (AvailableBackends.Contains(UsbApiKind.LibUsbDotNet))
        {
            result.Add(UsbApiKind.LibUsbDotNet);
        }
        if (AvailableBackends.Contains(UsbApiKind.Native))
        {
            result.Add(UsbApiKind.Native);
        }
        foreach (UsbApiKind kind in AvailableBackends)
        {
            if (kind != UsbApiKind.LibUsbDotNet && kind != UsbApiKind.Native && !result.Contains(kind))
            {
                result.Add(kind);
            }
        }
        return result;
    }

    private static UsbBackendConfiguration Create(OSPlatform platform)
    {
        if (platform == OSPlatform.Windows)
        {
            // Windows: native WinUSB backend is the default (is_libusb_enabled=false).
            // <para>Windows：默认原生 WinUSB 后端（is_libusb_enabled=false）。</para>
            return new UsbBackendConfiguration(
                UsbApiKind.Native,
                new UsbApiKind[] { UsbApiKind.Native, UsbApiKind.LibUsbDotNet });
        }

        if (platform == OSPlatform.OSX)
        {
            // macOS: libusb is the default (is_libusb_enabled=true); the native
            // IOKit backend is a fallback / enumeration-only path.
            // <para>macOS：默认 libusb（is_libusb_enabled=true）；原生 IOKit 后端
            // 仅作回退 / 枚举。</para>
            return new UsbBackendConfiguration(
                UsbApiKind.LibUsbDotNet,
                new UsbApiKind[] { UsbApiKind.LibUsbDotNet, UsbApiKind.Native });
        }

        // Linux and other Unix-likes: libusb is the default (is_libusb_enabled=true),
        // native USBFS is a fallback.
        // <para>Linux 及其他 Unix 系：默认 libusb（is_libusb_enabled=true），
        // 原生 USBFS 作回退。</para>
        return new UsbBackendConfiguration(
            UsbApiKind.LibUsbDotNet,
            new UsbApiKind[] { UsbApiKind.LibUsbDotNet, UsbApiKind.Native });
    }

    private static OSPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OSPlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OSPlatform.OSX;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OSPlatform.Linux;
        return OSPlatform.Create("Unknown");
    }
}
