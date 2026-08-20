using FirmwareKit.Comm.Backend.Linux;

namespace FirmwareKit.Comm.IntegrationTests;

/// <summary>
/// Marks the <see cref="LinuxUsbFinderDiagnosticsTests"/> collection as serialized.
/// <para>将 <see cref="LinuxUsbFinderDiagnosticsTests"/> 集合标记为串行执行。</para>
/// </summary>
/// <remarks>
/// These tests assert on <see cref="LinuxUsbFinder"/>'s shared static diagnostic state
/// (LastUsbfsRootExists / LastScannedNodes / LastMatchedDeviceCount). xUnit v3 runs test
/// methods within a class concurrently by default, and concurrent <c>FindDevice</c>
/// calls overwrite each other's diagnostics — the flaky root cause of the Ubuntu CI
/// failure where <c>FindDevice_EmptyUsbfsRoot</c> observed
/// <c>LastUsbfsRootExists=false</c> written by a parallel <c>UsbfsRootMissing</c> test.
/// <para>这些测试断言 <see cref="LinuxUsbFinder"/> 的共享静态诊断状态。xUnit v3 默认并行
/// 运行同类测试方法，并发的 <c>FindDevice</c> 调用会互相覆盖诊断——Ubuntu CI 失败的偶发根因：
/// <c>FindDevice_EmptyUsbfsRoot</c> 观察到被并行 <c>UsbfsRootMissing</c> 测试写入的
/// <c>LastUsbfsRootExists=false</c>。</para>
/// <c>DisableParallelization = true</c> forces the three methods in this class to
/// run sequentially, so each <c>FindDevice</c> call observes only its own diagnostics.
/// <para><c>DisableParallelization = true</c> 强制本类三个方法顺序执行，使每个
/// <c>FindDevice</c> 调用仅观察到自身的诊断。</para>
/// </remarks>
[CollectionDefinition("LinuxUsbFinder diagnostics (shared static state)", DisableParallelization = true)]
public sealed class LinuxUsbFinderDiagnosticsCollectionDefinition
{
}

/// <summary>
/// Verifies the observable enumeration diagnostics (LinuxUsbFinder.LastUsbfsRootExists /
/// LastScannedNodes / LastMatchedDeviceCount) so a device-less CI can distinguish
/// "enumeration mechanism did not run" from "mechanism ran but found no devices".
/// <para>验证可观测的枚举诊断状态（LinuxUsbFinder.LastUsbfsRootExists /
/// LastScannedNodes / LastMatchedDeviceCount），使无设备 CI 能区分
/// "枚举机制未运行"与"机制运行但未发现设备"。</para>
/// </summary>
/// <remarks>
/// The <c>[Collection]</c> attribute binds this class to the serialized collection
/// defined above. Both attributes are required: <c>[Collection]</c> without
/// <c>[CollectionDefinition(DisableParallelization = true)]</c> only disables
/// cross-class parallelism, not the within-class method parallelism that causes the
/// flaky failure.
/// <para><c>[Collection]</c> 特性将本类绑定到上方定义的串行集合。两个特性都需要：仅有
/// <c>[Collection]</c> 而无 <c>[CollectionDefinition(DisableParallelization = true)]</c>
/// 只禁用跨类并行，不禁用导致偶发失败的同类方法并行。</para>
/// </remarks>
[Collection("LinuxUsbFinder diagnostics (shared static state)")]
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
