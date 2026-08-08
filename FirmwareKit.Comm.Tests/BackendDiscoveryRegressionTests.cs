using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Backend;
using FirmwareKit.Comm.Backend.LibUsb;
using FirmwareKit.Comm.Backend.Linux;

namespace FirmwareKit.Comm.IntegrationTests;

/// <summary>
/// Regression tests for the two discovery/projection fixes: LibUsbDevice VID/PID
/// delegation to the base class (projection showed 0x0000) and LinuxUsbFinder
/// sysfs speed resolution (device dirs are named bus-port, not usb{devnum}).
/// <para>两个发现/投影修复的回归测试：LibUsbDevice VID/PID 委托到基类
/// （投影曾显示 0x0000）以及 LinuxUsbFinder 的 sysfs 速度解析
/// （设备目录按 bus-port 命名，而非 usb{devnum}）。</para>
/// </summary>
public sealed class BackendDiscoveryRegressionTests
{
    [Fact]
    public void LibUsbDevice_VidPid_DelegateToBaseClassVendorProductIds()
    {
        var device = new LibUsbDevice
        {
            Vid = 0x0403,
            Pid = 0x6001
        };

        // The projection path (UsbDeviceInfoFactory) reads VendorId/ProductId.
        // UsbDeviceInfoFactory 投影读取的是 VendorId/ProductId。
        Assert.Equal((ushort)0x0403, device.VendorId);
        Assert.Equal((ushort)0x6001, device.ProductId);
        Assert.Equal(device.VendorId, device.Vid);
        Assert.Equal(device.ProductId, device.Pid);
    }

    [Fact]
    public void LibUsbDevice_BaseClassVendorProductIds_AreVisibleViaVidPid()
    {
        var device = new LibUsbDevice
        {
            VendorId = 0x18D1,
            ProductId = 0xD00D
        };

        Assert.Equal((ushort)0x18D1, device.Vid);
        Assert.Equal((ushort)0xD00D, device.Pid);
    }

    [Fact]
    public void ResolveSpeed_ReadsSysfsByBusNumDevNum_NotUsbDevNumPath()
    {
        // Regression: the old code probed /sys/bus/usb/devices/usb002/speed, which
        // never exists (device dirs are named "1-1"), so speed always fell back to
        // bcdUSB inference (declared USB 2.0 => High) even on a Full-speed UHCI link.
        // 回归：旧代码探测 /sys/bus/usb/devices/usb002/speed，该目录永不存在
        // （设备目录名为 "1-1"），导致速度总是回退到 bcdUSB 推断
        // （声明 USB 2.0 => High），即使链路实为 Full-speed UHCI。
        string root = CreateTempSysfsRoot(deviceDirName: "1-1", busNum: 1, devNum: 2, speedMbps: "12");

        try
        {
            var speed = LinuxUsbFinder.ResolveSpeed("/dev/bus/usb/001/002", bcdUsb: 0x0200, sysfsDevicesRoot: root);

            Assert.Equal(UsbDeviceSpeed.Full, speed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveSpeed_HighSpeedSysfs_ReportsHigh()
    {
        string root = CreateTempSysfsRoot(deviceDirName: "2-3", busNum: 2, devNum: 3, speedMbps: "480");

        try
        {
            var speed = LinuxUsbFinder.ResolveSpeed("/dev/bus/usb/002/003", bcdUsb: 0x0100, sysfsDevicesRoot: root);

            Assert.Equal(UsbDeviceSpeed.High, speed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveSpeed_SysfsUnavailable_FallsBackToBcdUsbInference()
    {
        // No matching device dir (or unreadable sysfs): fall back to bcdUSB inference.
        // 无匹配设备目录（或 sysfs 不可读）：回退到 bcdUSB 推断。
        string root = CreateTempSysfsRoot(deviceDirName: "9-1", busNum: 9, devNum: 1, speedMbps: "12");

        try
        {
            var speed = LinuxUsbFinder.ResolveSpeed("/dev/bus/usb/001/002", bcdUsb: 0x0200, sysfsDevicesRoot: root);

            Assert.Equal(UsbDeviceSpeed.High, speed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempSysfsRoot(string deviceDirName, int busNum, int devNum, string speedMbps)
    {
        string root = Path.Combine(Path.GetTempPath(), "fkc-sysfs-" + Guid.NewGuid().ToString("N"));
        string deviceDir = Path.Combine(root, deviceDirName);
        Directory.CreateDirectory(deviceDir);
        File.WriteAllText(Path.Combine(deviceDir, "busnum"), busNum.ToString());
        File.WriteAllText(Path.Combine(deviceDir, "devnum"), devNum.ToString());
        File.WriteAllText(Path.Combine(deviceDir, "speed"), speedMbps);
        return root;
    }
}
