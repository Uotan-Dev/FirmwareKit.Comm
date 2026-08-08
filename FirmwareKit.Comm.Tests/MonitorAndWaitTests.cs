using FirmwareKit.Comm;
using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Core;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Tests for the wait-for-device APIs and the polling device monitor.
/// <para>等待设备 API 与轮询设备监视器的测试。</para>
/// </summary>
public sealed class MonitorAndWaitTests
{
    [Fact]
    public async Task WaitForDeviceAppear_ReturnsTrue_WhenDeviceAppears()
    {
        var provider = new MutableProvider();
        var layer = CreateLayer(provider);

        var waitTask = layer.WaitForDeviceAppearAsync(UsbApiKind.Auto, new UsbDeviceFilter { VendorId = 0x1F3A }, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken); // let the polling loop start with an empty snapshot
        provider.Devices.Add(CreateInfo("appear-path", serial: "S1"));

        Assert.True(await waitTask);
    }

    [Fact]
    public async Task WaitForDeviceAppear_ReturnsFalse_OnTimeout()
    {
        var provider = new MutableProvider();
        var layer = CreateLayer(provider);

        bool appeared = await layer.WaitForDeviceAppearAsync(UsbApiKind.Auto, null, TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        Assert.False(appeared);
    }

    [Fact]
    public async Task WaitForDeviceDisappear_ReturnsTrue_WhenDeviceRemoved()
    {
        var provider = new MutableProvider();
        provider.Devices.Add(CreateInfo("disappear-path", serial: "S2", vid: 0x05C6));
        var layer = CreateLayer(provider);

        var waitTask = layer.WaitForDeviceDisappearAsync(UsbApiKind.Auto, new UsbDeviceFilter { VendorId = 0x05C6 }, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        provider.Devices.Clear();

        Assert.True(await waitTask);
    }

    [Fact]
    public async Task WaitForModeSwitch_ReturnsTrue_WhenOldGoneAndNewPresent()
    {
        var provider = new MutableProvider();
        provider.Devices.Add(CreateInfo("adb-path", serial: "ADB-SERIAL", vid: 0x18D1, pid: 0xD00D));
        var layer = CreateLayer(provider);

        var waitTask = layer.WaitForModeSwitchAsync(
            new UsbDeviceFilter { VendorId = 0x18D1, ProductId = 0xD00D },
            new UsbDeviceFilter { VendorId = 0x1F3A, ProductId = 0xEFE8 },
            UsbApiKind.Auto,
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        provider.Devices.Clear();
        provider.Devices.Add(CreateInfo("fastboot-path", serial: "FASTBOOT-SERIAL", vid: 0x1F3A, pid: 0xEFE8));

        Assert.True(await waitTask);
    }

    [Fact]
    public void Monitor_ReportsInitialAdded_ThenAddedRemovedChanged()
    {
        var current = new List<UsbDeviceInfo> { CreateInfo("dev-a", serial: "A", speed: UsbDeviceSpeed.High) };
        var changes = new List<UsbDeviceChange>();

        using var monitor = new UsbDeviceMonitor(() => current.ToList(), c => changes.AddRange(c), null, TimeSpan.FromMilliseconds(50), fireInitialSnapshot: true);

        Thread.Sleep(150);
        Assert.Contains(changes, c => c.Kind == UsbDeviceChangeKind.Added && c.Device.SerialNumber == "A");

        // Added: a new device appears.
        changes.Clear();
        current.Add(CreateInfo("dev-b", serial: "B", speed: UsbDeviceSpeed.High));
        Thread.Sleep(150);
        Assert.Contains(changes, c => c.Kind == UsbDeviceChangeKind.Added && c.Device.SerialNumber == "B");

        // Changed: same physical identity key, but the speed metadata differs.
        changes.Clear();
        current[0] = CreateInfo("dev-a", serial: "A", speed: UsbDeviceSpeed.Super);
        Thread.Sleep(150);
        Assert.Contains(changes, c => c.Kind == UsbDeviceChangeKind.Changed && c.Device.SerialNumber == "A");

        // Removed: device disappears.
        changes.Clear();
        current.RemoveAt(0);
        Thread.Sleep(150);
        Assert.Contains(changes, c => c.Kind == UsbDeviceChangeKind.Removed && c.Device.SerialNumber == "A");
    }

    [Fact]
    public void Monitor_CancellationToken_DisposesMonitor()
    {
        using var cts = new CancellationTokenSource();
        var current = new List<UsbDeviceInfo>();
        var changes = new List<UsbDeviceChange>();

        using var monitor = new UsbDeviceMonitor(() => current.ToList(), c => changes.AddRange(c), null, TimeSpan.FromMilliseconds(50), fireInitialSnapshot: false, cts.Token);

        cts.Cancel();
        Thread.Sleep(150);

        // After cancellation the monitor is disposed; a late change must not raise events.
        changes.Clear();
        current.Add(CreateInfo("late", serial: "LATE"));
        Thread.Sleep(150);
        Assert.Empty(changes);
    }

    private static UsbCommunicationLayer CreateLayer(MutableProvider provider)
    {
        var registry = new UsbApiRegistry();
        registry.Register(provider.ApiName, () => provider);
        return new UsbCommunicationLayer(registry);
    }

    private static UsbDeviceInfo CreateInfo(string path, string? serial = null, ushort vid = 0x1F3A, ushort pid = 0xEFE8, UsbDeviceSpeed speed = UsbDeviceSpeed.High)
    {
        return new UsbDeviceInfo
        {
            ApiName = "test-mutable",
            SourceApiKind = UsbApiKind.Custom,
            DevicePath = path,
            DeviceKey = path,
            SerialNumber = serial,
            VendorId = vid,
            ProductId = pid,
            InterfaceClass = 0xFF,
            InterfaceSubClass = 0xFF,
            InterfaceProtocol = 0xFF,
            InterfaceMetadataObserved = true,
            Speed = speed
        };
    }

    private sealed class MutableProvider : IUsbApiProvider, IUsbApiDiscoveryProvider
    {
        public string ApiName => "test-mutable";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public List<UsbDeviceInfo> Devices { get; } = new();

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
            => Array.Empty<IUsbDeviceSession>();

        public IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null)
            => Devices.Where(d => filter == null || filter.Matches(d)).ToList();
    }
}
