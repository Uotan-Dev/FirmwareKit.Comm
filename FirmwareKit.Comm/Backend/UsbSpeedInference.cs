using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Backend;

/// <summary>
/// Shared USB speed inference helpers used by backends that do not report the negotiated
/// link speed directly (macOS, HarmonyOS, Linux fallback).
/// <para>供不直接报告协商链路速度的后端（macOS、HarmonyOS、Linux 回退）使用的共享
/// USB 速度推断工具。</para>
/// </summary>
internal static class UsbSpeedInference
{
    /// <summary>
    /// Approximates the USB speed from the device descriptor's bcdUSB version.
    /// <para>根据设备描述符的 bcdUSB 版本近似推断 USB 速度。</para>
    /// This reflects the device's declared USB spec version rather than the negotiated
    /// link speed; good enough for discovery hints (e.g. EDL USB3 vs USB2 paths).
    /// <para>反映设备声明的 USB 规范版本而非协商链路速度；作为发现提示足够
    /// （例如 EDL 区分 USB3/USB2 路径）。</para>
    /// </summary>
    /// <param name="bcdUsb">The bcdUSB field of the device descriptor. <para>设备描述符的 bcdUSB 字段。</para></param>
    /// <returns>The inferred speed. <para>推断出的速度。</para></returns>
    public static UsbDeviceSpeed FromBcdUsb(ushort bcdUsb)
    {
        if (bcdUsb >= 0x0301) return UsbDeviceSpeed.SuperPlus;
        if (bcdUsb >= 0x0300) return UsbDeviceSpeed.Super;
        if (bcdUsb >= 0x0200) return UsbDeviceSpeed.High;
        return UsbDeviceSpeed.Full;
    }
}
