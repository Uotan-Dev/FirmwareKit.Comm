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

        // IOUSBLib may be absent on stripped hosts (e.g. GitHub Actions macOS runners
        // where the framework is not in the dyld cache). Both outcomes are valid:
        //   - framework present : copy-devices=True, counters reflect the scan
        //   - framework absent  : copy-devices=False and an empty result (no throw)
        // <para>精简主机（例如 GitHub Actions macOS runner）上 IOUSBLib 可能缺失。
        // 两种结果均合法：框架存在时 copy-devices=True；框架缺失时 copy-devices=False
        // 且返回空列表（不抛出异常）。</para>
        if (MacHostUsbFinder.LastCopyDevicesSucceeded)
        {
            Assert.Equal(devices.Count, MacHostUsbFinder.LastScannedDeviceCount);
        }
        else
        {
            Assert.Empty(devices);
        }
    }
}
