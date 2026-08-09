using System.Runtime.InteropServices;
using FirmwareKit.Comm.Backend.MacOS;

namespace FirmwareKit.Comm.IntegrationTests;

/// <summary>
/// Verifies the observable enumeration diagnostics (MacHostUsbFinder.LastCopyDevicesSucceeded /
/// LastScannedDeviceCount / LastMatchedDeviceCount) so a device-less CI can distinguish
/// "IOUSBLib call failed" from "IOUSBLib ran but found no devices".
/// <para>验证可观测的枚举诊断状态（MacHostUsbFinder.LastCopyDevicesSucceeded /
/// LastScannedDeviceCount / LastMatchedDeviceCount），使无设备 CI 能区分
/// "IOUSBLib 调用失败"与"IOUSBLib 运行但未发现设备"。</para>
/// </summary>
public sealed class MacHostUsbFinderDiagnosticsTests
{
    [Fact]
    public void FindDevice_ReportsCopyDevicesResultAndMatchedCount()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return; // IOUSBLib only exists on macOS; skipped elsewhere.
        }

        var devices = MacHostUsbFinder.FindDevice(filter: null);

        Assert.Equal(devices.Count, MacHostUsbFinder.LastMatchedDeviceCount);
        Assert.True(MacHostUsbFinder.LastScannedDeviceCount >= 0);
        // IOUSBLibCopyDevices must succeed on a healthy macOS host even with zero devices,
        // proving the enumeration mechanism ran (vs. silently failing).
        Assert.True(MacHostUsbFinder.LastCopyDevicesSucceeded, "IOUSBLibCopyDevices must succeed on macOS");
    }
}
