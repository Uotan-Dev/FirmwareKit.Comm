using FirmwareKit.Comm.Backend.Linux;

namespace FirmwareKit.Comm.IntegrationTests;

/// <summary>
/// Verifies the observable enumeration diagnostics (LinuxUsbFinder.LastUsbfsRootExists /
/// LastScannedNodes / LastMatchedDeviceCount) so a device-less CI can distinguish
/// "enumeration mechanism did not run" from "mechanism ran but found no devices".
/// <para>验证可观测的枚举诊断状态（LinuxUsbFinder.LastUsbfsRootExists /
/// LastScannedNodes / LastMatchedDeviceCount），使无设备 CI 能区分
/// "枚举机制未运行"与"机制运行但未发现设备"。</para>
/// </summary>
public sealed class LinuxUsbFinderDiagnosticsTests
{
    [Fact]
    public void FindDevice_UsbfsRootMissing_ReportsRootNotPresent()
    {
        string missingRoot = Path.Combine(Path.GetTempPath(), "fkc-no-such-usbfs-" + Guid.NewGuid().ToString("N"));

        var devices = LinuxUsbFinder.FindDevice(filter: null, usbfsRoot: missingRoot);

        Assert.Empty(devices);
        Assert.False(LinuxUsbFinder.LastUsbfsRootExists, "root missing must be observable");
        Assert.Equal(0, LinuxUsbFinder.LastScannedNodes);
        Assert.Equal(0, LinuxUsbFinder.LastMatchedDeviceCount);
    }

    [Fact]
    public void FindDevice_EmptyUsbfsRoot_ReportsScanRanWithZeroNodes()
    {
        string root = Path.Combine(Path.GetTempPath(), "fkc-usbfs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var devices = LinuxUsbFinder.FindDevice(filter: null, usbfsRoot: root);

            Assert.Empty(devices);
            Assert.True(LinuxUsbFinder.LastUsbfsRootExists, "existing empty root must be observable as present");
            Assert.Equal(0, LinuxUsbFinder.LastScannedNodes);
            Assert.Equal(0, LinuxUsbFinder.LastMatchedDeviceCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindDevice_UsbfsRootWithNodeFiles_CountsScannedNodes()
    {
        // A node file makes the scanner attempt open(); on non-Linux the native open()
        // may throw, so the scan counter (incremented before open) is the observable
        // proof that the loop ran. The exception is swallowed for the assertion.
        // <para>节点文件会让扫描器尝试 open()；在非 Linux 平台上原生 open() 可能抛出异常，
        // 因此扫描计数器（在 open 前递增）是循环确实执行的可见证据。断言时吞掉异常。</para>
        string root = Path.Combine(Path.GetTempPath(), "fkc-usbfs-nodes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "001"));
        File.WriteAllText(Path.Combine(root, "001", "002"), "fake device node");

        try
        {
            try
            {
                _ = LinuxUsbFinder.FindDevice(filter: null, usbfsRoot: root);
            }
            catch
            {
                // open() on non-Linux throws; the diagnostic counters must still be set.
            }

            Assert.True(LinuxUsbFinder.LastUsbfsRootExists);
            Assert.True(LinuxUsbFinder.LastScannedNodes >= 1, "scanner must count node files before open()");
            Assert.True(LinuxUsbFinder.LastMatchedDeviceCount >= 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
