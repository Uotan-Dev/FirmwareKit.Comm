using System.Runtime.InteropServices;
using FirmwareKit.Comm.Backend.Windows;

namespace FirmwareKit.Comm.IntegrationTests;

/// <summary>
/// Verifies the observable enumeration diagnostics (WinUSBFinder.LastSetupDiSucceeded /
/// LastScannedNodeCount / LastMatchedDeviceCount) so a device-less CI can distinguish
/// "SetupDi failed" from "SetupDi ran but found no devices".
/// <para>验证可观测的枚举诊断状态（WinUSBFinder.LastSetupDiSucceeded /
/// LastScannedNodeCount / LastMatchedDeviceCount），使无设备 CI 能区分
/// "SetupDi 失败"与"SetupDi 运行但未发现设备"。</para>
/// </summary>
public sealed class WinUSBFinderDiagnosticsTests
{
    [Fact]
    public void FindDevice_ReportsSetupDiResultAndMatchedCount()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // SetupDi P/Invoke only exists on Windows; skipped elsewhere.
        }

        var devices = WinUSBFinder.FindDevice(filter: null);

        Assert.Equal(devices.Count, WinUSBFinder.LastMatchedDeviceCount);
        Assert.True(WinUSBFinder.LastScannedNodeCount >= 0);
        // SetupDiGetClassDevsW must succeed on a healthy Windows host even with zero devices,
        // proving the enumeration mechanism ran (vs. silently failing).
        Assert.True(WinUSBFinder.LastSetupDiSucceeded, "SetupDiGetClassDevsW must succeed on Windows");
    }
}
