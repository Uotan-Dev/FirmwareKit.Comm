using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Core;
using System.Collections.Concurrent;

namespace FirmwareKit.Comm.IntegrationTests;

public sealed class UsbCommunicationLayerIntegrationTests
{
    [Fact]
    public void DefaultLayer_GetAvailableApis_FiltersByPlatformSupport()
    {
        // GetAvailableApis() reports only backends supported on the current platform.
        // GetAvailableApis() 只报告当前平台受支持的后端。
        var layer = new UsbCommunicationLayer();
        var apis = layer.GetAvailableApis();

        // native is available on every desktop platform (Windows/Linux/macOS).
        // native 在所有桌面平台（Windows/Linux/macOS）均可用。
        Assert.Contains("native", apis, StringComparer.OrdinalIgnoreCase);

        // harmony is opt-in and hidden by default (FIRMWAREKIT_USB_ENABLE_HARMONY=1
        // required), and no hosted CI runner is a HarmonyOS device.
        // harmony 为显式开启项，默认隐藏（需 FIRMWAREKIT_USB_ENABLE_HARMONY=1），
        // 且托管 CI runner 均非 HarmonyOS 设备。
        Assert.DoesNotContain("harmony", apis, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnumerateDevices_WithFilter_DoesNotThrow()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom", () => new EmptyProvider());

        var filter = new UsbDeviceFilter
        {
            VendorId = 0xFFFF,
            ProductId = 0xFFFF
        };

        var devices = layer.EnumerateDevices(UsbApiKind.Auto, filter, TestContext.Current.CancellationToken);
        Assert.NotNull(devices);
        Assert.Empty(devices);
    }

    [Fact]
    public async Task EnumerateDevicesAsync_DoesNotThrow()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom", () => new EmptyProvider());

        var devices = await layer.EnumerateDevicesAsync(UsbApiKind.Auto, new UsbDeviceFilter(), TestContext.Current.CancellationToken);
        Assert.NotNull(devices);
        Assert.Empty(devices);
    }

    [Fact]
    public async Task EnumerateDevicesAsync_RespectsCancellation()
    {
        UsbCommunicationLayer? layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom", () => new EmptyProvider());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            layer.EnumerateDevicesAsync(UsbApiKind.Auto, new UsbDeviceFilter(), cts.Token));
    }

    [Fact]
    public void RegisterCustomProvider_IsInvokable()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom", () => new EmptyProvider());

        var apis = layer.GetAvailableApis();
        Assert.Contains("custom", apis, StringComparer.OrdinalIgnoreCase);

        var devices = layer.EnumerateDevices(UsbApiKind.Auto, new UsbDeviceFilter(), TestContext.Current.CancellationToken);
        Assert.Empty(devices);
    }

    [Fact]
    public void GetAvailableApiCapabilities_ReportsBackendNotes()
    {
        var layer = new UsbCommunicationLayer();
        var capabilities = layer.GetAvailableApiCapabilities();

        var native = Assert.Single(capabilities, item => string.Equals(item.ApiName, "native", StringComparison.OrdinalIgnoreCase));
        var libusb = Assert.Single(capabilities, item => string.Equals(item.ApiName, "libusb", StringComparison.OrdinalIgnoreCase));
        var harmony = Assert.Single(capabilities, item => string.Equals(item.ApiName, "harmony", StringComparison.OrdinalIgnoreCase));
        Assert.True(native.SupportsNativeDiscovery);
        Assert.True(native.SupportsControlTransfers);
        Assert.False(native.SupportsNativeAsyncIo);
        Assert.False(native.SupportsNativeHotPlugMonitoring);
        Assert.False(native.RequiresExternalRuntime);
        Assert.True(libusb.SupportsControlTransfers);
        Assert.True(libusb.SupportsNativeAsyncIo);
        Assert.True(libusb.RequiresExternalRuntime);
        Assert.Equal(UsbApiKind.HarmonyOS, harmony.ApiKind);
        Assert.True(harmony.SupportsControlTransfers);
        Assert.False(harmony.RequiresExternalRuntime);
    }

    [Fact]
    public void NativeCapabilities_ReportPerBackendAsyncSupport()
    {
        var layer = new UsbCommunicationLayer();
        var capabilities = layer.GetAvailableApiCapabilities();
        var native = Assert.Single(capabilities, item => string.Equals(item.ApiName, "native", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(native.Backends);
        Assert.Contains(native.Backends!, b => b.BackendName == "winusb" && b.SupportsNativeAsyncIo);
        Assert.Contains(native.Backends!, b => b.BackendName == "linux-usbfs" && b.SupportsNativeAsyncIo);
        Assert.Contains(native.Backends!, b => b.BackendName == "winusb-legacy" && !b.SupportsNativeAsyncIo);
        Assert.Contains(native.Backends!, b => b.BackendName == "macos-iousbhost" && !b.SupportsNativeAsyncIo);
        Assert.Contains(native.Backends!, b => b.BackendName == "harmony-ddk" && !b.SupportsNativeAsyncIo);
    }

    [Fact]
    public void SessionTimeoutMethods_AreInvokableThroughRegisteredProvider()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom-timeout", () => new TimeoutProvider());

        using var sessions = layer.OpenDeviceSessions(UsbApiKind.Auto, new UsbDeviceFilter
        {
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        });

        var session = Assert.Single(sessions.Sessions);
        var read = session.Read(4, 1234);
        var count = session.ReadInto(new byte[8], 0, 8, 4321);
        var written = session.Write(new byte[] { 1, 2, 3 }, 3, 987);
        var transferred = session.ControlTransfer(new UsbSetupPacket
        {
            RequestType = 0x80,
            Request = 0x06,
            Value = 0x0100,
            Index = 0x0000,
            Length = 4
        }, new byte[4], 0, 4, 2468);

        Assert.Equal(4, read.Length);
        Assert.Equal(8, count);
        Assert.Equal(3, written);
        Assert.Equal(4, transferred);

        var timeoutSession = Assert.IsType<TimeoutSession>(session);
        Assert.Equal(1234, timeoutSession.DefaultTimeoutMs);
        Assert.Equal(1234, timeoutSession.LastReadTimeoutMs);
        Assert.Equal(4321, timeoutSession.LastReadIntoTimeoutMs);
        Assert.Equal(987, timeoutSession.LastWriteTimeoutMs);
        Assert.Equal(2468, timeoutSession.LastControlTimeoutMs);
    }

    [Fact]
    public async Task SessionAsyncMethods_AreInvokableThroughRegisteredProvider()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom-timeout", () => new TimeoutProvider());

        using var sessions = layer.OpenDeviceSessions(UsbApiKind.Auto, new UsbDeviceFilter
        {
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        });

        var session = Assert.Single(sessions.Sessions);
        var asyncSession = Assert.IsAssignableFrom<IAsyncUsbDeviceSession>(session);

        var read = await asyncSession.ReadAsync(4, 2000, TestContext.Current.CancellationToken);
        var buffer = new byte[8];
        var count = await asyncSession.ReadIntoAsync(buffer, 0, 8, 3000, TestContext.Current.CancellationToken);
        var written = await asyncSession.WriteAsync(new byte[] { 1, 2, 3 }, 3, 4000, TestContext.Current.CancellationToken);
        await asyncSession.ResetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, read.Length);
        Assert.Equal(8, count);
        Assert.Equal(3, written);

        var timeoutSession = Assert.IsType<TimeoutSession>(session);
        Assert.Equal(1234, timeoutSession.DefaultTimeoutMs);
        Assert.Equal(2000, timeoutSession.LastAsyncReadTimeoutMs);
        Assert.Equal(3000, timeoutSession.LastAsyncReadIntoTimeoutMs);
        Assert.Equal(4000, timeoutSession.LastAsyncWriteTimeoutMs);
        Assert.True(timeoutSession.AsyncResetInvoked);
    }

    [Fact]
    public void EnumerateDevices_PassesInterfaceCriteriaToProvider()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        var provider = new InspectingProvider();
        _ = layer.RegisterApi("inspect", () => provider);

        var filter = new UsbDeviceFilter
        {
            InterfaceClass = 0xFF,
            InterfaceSubClass = 0xFF,
            InterfaceProtocol = 0xFF
        };

        var devices = layer.EnumerateDevices(UsbApiKind.Auto, filter, TestContext.Current.CancellationToken);
        Assert.Empty(devices);

        Assert.NotNull(provider.LastFilter);
        Assert.Equal((byte)0xFF, provider.LastFilter!.InterfaceClass);
        Assert.Equal((byte)0xFF, provider.LastFilter.InterfaceSubClass);
        Assert.Equal((byte)0xFF, provider.LastFilter.InterfaceProtocol);
    }

    [Fact]
    public void CapabilityDefaults_AssumeControlTransferSupport()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom-capabilities", () => new EmptyProvider());

        var capabilities = layer.GetAvailableApiCapabilities();
        var custom = Assert.Single(capabilities, item => string.Equals(item.ApiName, "custom", StringComparison.OrdinalIgnoreCase));

        Assert.True(custom.SupportsControlTransfers);
        Assert.True(custom.SupportsDeviceSessions);
    }

    [Fact]
    public void EnumerateDevices_PrefersDiscoveryProvider_WhenAvailable()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        var provider = new DiscoveryOnlyInspectingProvider();
        _ = layer.RegisterApi("discover", () => provider);

        var devices = layer.EnumerateDevices(UsbApiKind.Auto, new UsbDeviceFilter
        {
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        }, TestContext.Current.CancellationToken);

        var device = Assert.Single(devices);
        Assert.Equal((ushort)0x1F3A, device.VendorId);
        Assert.Equal((ushort)0xEFE8, device.ProductId);
        Assert.Equal(1, provider.DiscoveryCalls);
        Assert.Equal(0, provider.SessionCalls);
    }

    [Fact]
    public void SessionMethods_InvalidArguments_Throw()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom-guards", () => new TimeoutProvider());

        using var sessions = layer.OpenDeviceSessions(UsbApiKind.Auto, new UsbDeviceFilter
        {
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        });

        var session = Assert.Single(sessions.Sessions);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.Read(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Read(-1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ReadInto(new byte[4], 0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ReadInto(new byte[4], -1, 1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Write(new byte[2], 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Write(new byte[2], -1, 100));
    }

    [Fact]
    public void RegisterApi_DoesNotInvokeFactoryImmediately()
    {
        var registry = new UsbApiRegistry();
        var layer = new UsbCommunicationLayer(registry);
        var createCount = 0;

        _ = layer.RegisterApi("counted", () =>
        {
            createCount++;
            return new EmptyProvider();
        });

        Assert.Equal(0, createCount);

        _ = layer.EnumerateDevices(UsbApiKind.Auto, new UsbDeviceFilter(), TestContext.Current.CancellationToken);
        Assert.Equal(1, createCount);
    }

    [Fact]
    public void MonitorDevices_EmitsAddedAndRemovedChanges()
    {
        var provider = new SwitchingDiscoveryProvider();
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("switching", () => provider);

        var changesQueue = new ConcurrentQueue<UsbDeviceChange>();
        using var signal = new ManualResetEventSlim(false);

        using var monitor = layer.MonitorDevices(
            changes =>
            {
                foreach (var change in changes)
                {
                    changesQueue.Enqueue(change);
                    signal.Set();
                }
            },
            UsbApiKind.Auto,
            filter: null,
            pollInterval: TimeSpan.FromMilliseconds(50),
            fireInitialSnapshot: false,
            cancellationToken: TestContext.Current.CancellationToken);

        provider.DevicePresent = true;
        Assert.True(signal.Wait(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));

        signal.Reset();
        provider.DevicePresent = false;
        Assert.True(signal.Wait(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));

        var allChanges = changesQueue.ToArray();
        Assert.Contains(allChanges, c => c.Kind == UsbDeviceChangeKind.Added);
        Assert.Contains(allChanges, c => c.Kind == UsbDeviceChangeKind.Removed);
    }

    [Fact]
    public void MonitorDevices_ReportsCallbackFailures()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("discover", () => new DiscoveryOnlyInspectingProvider());

        Exception? capturedError = null;

        using var monitor = layer.MonitorDevices(
            _ => throw new InvalidOperationException("boom"),
            UsbApiKind.Auto,
            filter: null,
            pollInterval: TimeSpan.FromMilliseconds(50),
            fireInitialSnapshot: true,
            onError: ex => capturedError = ex,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(capturedError);
        Assert.IsType<InvalidOperationException>(capturedError);
    }

    [Fact]
    public void SessionMethods_RejectOverflowingRanges()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom-guards", () => new TimeoutProvider());

        using var sessions = layer.OpenDeviceSessions(UsbApiKind.Auto, new UsbDeviceFilter
        {
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        });

        var session = Assert.Single(sessions.Sessions);
        var buffer = new byte[4];

        Assert.Throws<ArgumentOutOfRangeException>(() => session.ReadInto(buffer, int.MaxValue, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ReadInto(buffer, int.MaxValue, 1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Write(new byte[4], int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Write(new byte[4], int.MaxValue, 100));
    }

    private sealed class EmptyProvider : IUsbApiProvider
    {
        public string ApiName => "custom";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null) => [];
    }

    private sealed class TimeoutProvider : IUsbApiProvider
    {
        public string ApiName => "custom-timeout";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
        {
            var session = new TimeoutSession();
            if (filter == null || filter.Matches(session.DeviceInfo))
            {
                return new[] { (IUsbDeviceSession)session };
            }

            session.Dispose();
            return Array.Empty<IUsbDeviceSession>();
        }
    }

    private sealed class InspectingProvider : IUsbApiProvider
    {
        public string ApiName => "inspect";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public UsbDeviceFilter? LastFilter { get; private set; }

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
        {
            LastFilter = filter;
            return Array.Empty<IUsbDeviceSession>();
        }
    }

    private sealed class DiscoveryOnlyInspectingProvider : IUsbApiProvider, IUsbApiDiscoveryProvider
    {
        public string ApiName => "discover";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public int DiscoveryCalls { get; private set; }

        public int SessionCalls { get; private set; }

        public IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null)
        {
            DiscoveryCalls++;
            return new[]
            {
                new UsbDeviceInfo
                {
                    ApiName = ApiName,
                    SourceApiKind = ApiKind,
                    DevicePath = "mock://discover",
                    VendorId = 0x1F3A,
                    ProductId = 0xEFE8,
                    InterfaceClass = 0xFF,
                    InterfaceSubClass = 0xFF,
                    InterfaceProtocol = 0xFF,
                    InterfaceMetadataObserved = true
                }
            };
        }

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
        {
            SessionCalls++;
            return Array.Empty<IUsbDeviceSession>();
        }
    }

    private sealed class SwitchingDiscoveryProvider : IUsbApiProvider, IUsbApiDiscoveryProvider
    {
        public string ApiName => "switching";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public volatile bool DevicePresent;

        public IReadOnlyList<UsbDeviceInfo> EnumerateDeviceInfos(UsbDeviceFilter? filter = null)
        {
            if (!DevicePresent)
            {
                return Array.Empty<UsbDeviceInfo>();
            }

            return new[]
            {
                new UsbDeviceInfo
                {
                    ApiName = ApiName,
                    SourceApiKind = ApiKind,
                    DevicePath = "mock://switching",
                    VendorId = 0x18D1,
                    ProductId = 0xD00D,
                    SerialNumber = "dev-1",
                    InterfaceMetadataObserved = true
                }
            };
        }

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
        {
            return Array.Empty<IUsbDeviceSession>();
        }
    }

    private sealed class TimeoutSession : IUsbDeviceSession, IAsyncUsbDeviceSession
    {
        public int DefaultTimeoutMs => 1234;
        public byte EndpointIn => 0x81;
        public byte EndpointOut => 0x01;

        public int LastReadTimeoutMs { get; private set; }

        public int LastReadIntoTimeoutMs { get; private set; }

        public int LastWriteTimeoutMs { get; private set; }

        public int LastControlTimeoutMs { get; private set; }

        public int LastAsyncReadTimeoutMs { get; private set; }

        public int LastAsyncReadIntoTimeoutMs { get; private set; }

        public int LastAsyncWriteTimeoutMs { get; private set; }

        public bool AsyncResetInvoked { get; private set; }

        public UsbDeviceInfo DeviceInfo { get; } = new()
        {
            ApiName = "custom-timeout",
            SourceApiKind = UsbApiKind.Custom,
            DevicePath = "mock://timeout",
            VendorId = 0x1F3A,
            ProductId = 0xEFE8,
            InterfaceMetadataObserved = true
        };

        public byte[] Read(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return new byte[length];
        }

        public byte[] Read(int length, int timeoutMs)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            LastReadTimeoutMs = timeoutMs;
            return new byte[length];
        }

        public int ReadInto(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0 || length > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return length;
        }

        public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0 || length > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            LastReadIntoTimeoutMs = timeoutMs;
            Array.Fill(buffer, (byte)0x5A, offset, length);
            return length;
        }

        public UsbReadResult ReadPacket(byte[] buffer, int offset, int length, int timeoutMs)
        {
            if (offset < 0 || length < 0 || length > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            LastReadIntoTimeoutMs = timeoutMs;
            Array.Fill(buffer, (byte)0x5A, offset, length);
            return new UsbReadResult(length, isTimeout: false, isShortPacket: false);
        }

        public UsbReadResult ReadInterrupt(byte endpointAddress, byte[] buffer, int offset, int length, int timeoutMs)
            => throw new NotSupportedException();

        public long WriteInterrupt(byte endpointAddress, byte[] data, int offset, int length, int timeoutMs)
            => throw new NotSupportedException();

        public Task<UsbReadResult> ReadPacketAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            LastAsyncReadIntoTimeoutMs = timeoutMs;
            return Task.FromResult(new UsbReadResult(length, isTimeout: false, isShortPacket: false));
        }

        public Task<UsbReadResult> ReadInterruptAsync(byte endpointAddress, byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> WriteInterruptAsync(byte endpointAddress, byte[] data, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public long Write(byte[] data, int length)
        {
            if (length < 0 || length > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return length;
        }

        public long Write(byte[] data, int length, int timeoutMs)
        {
            if (length < 0 || length > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            LastWriteTimeoutMs = timeoutMs;
            return length;
        }

        public long Write(byte[] data, int offset, int length, int timeoutMs)
        {
            if (offset < 0 || length < 0 || length > data.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            LastWriteTimeoutMs = timeoutMs;
            return length;
        }

        public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
        {
            LastControlTimeoutMs = timeoutMs;
            return length;
        }

        public void SetInterfaceAltSetting(byte interfaceNumber, byte altSetting) { }

        public void SetConfiguration(byte configuration) { }

        public void Reset()
        {
        }

        public Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            LastAsyncReadTimeoutMs = timeoutMs;
            return Task.FromResult(new byte[length]);
        }

        public Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            LastAsyncReadIntoTimeoutMs = timeoutMs;
            return Task.FromResult(length);
        }

        public Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            LastAsyncWriteTimeoutMs = timeoutMs;
            return Task.FromResult((long)length);
        }

        public Task<long> WriteAsync(byte[] data, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            LastAsyncWriteTimeoutMs = timeoutMs;
            return Task.FromResult((long)length);
        }

        public Task<int> ControlTransferAsync(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            LastControlTimeoutMs = timeoutMs;
            return Task.FromResult(length);
        }

        public Task SetInterfaceAltSettingAsync(byte interfaceNumber, byte altSetting, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetConfigurationAsync(byte configuration, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            AsyncResetInvoked = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public void HarmonyOSApiKind_IsRegisteredInDefaultRegistry()
    {
        var registry = UsbApiRegistry.CreateDefault();
        var apis = registry.GetApiNames();

        Assert.Contains("harmony", apis, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void HarmonyOSApiKind_ResolvesToHarmonyProvider()
    {
        var layer = new UsbCommunicationLayer();
        var capabilities = layer.GetAvailableApiCapabilities();
        var harmony = capabilities.FirstOrDefault(c => string.Equals(c.ApiName, "harmony", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(harmony);
        Assert.Equal(UsbApiKind.HarmonyOS, harmony.ApiKind);
    }
    [Fact]
    public void UsbApiKind_HarmonyOS_HasCorrectValue()
    {
        Assert.Equal(4, (int)UsbApiKind.HarmonyOS);
    }

    [Fact]
    public void UsbDeviceHandleClosedException_IsInvalidOperationException()
    {
        var ex = new UsbDeviceHandleClosedException("test");
        Assert.IsType<UsbDeviceHandleClosedException>(ex);
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
        Assert.Equal("test", ex.Message);
    }

    [Fact]
    public void UsbTransferException_IsIOException()
    {
        var ex = new UsbTransferException("transfer failed");
        Assert.IsType<UsbTransferException>(ex);
        Assert.IsAssignableFrom<IOException>(ex);
        Assert.Equal("transfer failed", ex.Message);
        Assert.Null(ex.NativeErrorCode);
    }

    [Fact]
    public void UsbTransferException_WithNativeErrorCode_PreservesCode()
    {
        var ex = new UsbTransferException("transfer failed", 110);
        Assert.Equal(110, ex.NativeErrorCode);
    }

    [Fact]
    public void UsbDeviceOpenException_IsInvalidOperationException()
    {
        var ex = new UsbDeviceOpenException("open failed", "/dev/bus/usb/001/001", 13);
        Assert.IsType<UsbDeviceOpenException>(ex);
        Assert.Equal("/dev/bus/usb/001/001", ex.DevicePath);
        Assert.Equal(13, ex.NativeErrorCode);
    }

    [Fact]
    public void UsbDeviceFilter_MatchesHarmonyOSApiKind()
    {
        var info = new UsbDeviceInfo
        {
            VendorId = 0x18D1,
            ProductId = 0xD00D,
            SourceApiKind = UsbApiKind.HarmonyOS
        };

        var matchingFilter = new UsbDeviceFilter { SourceApiKind = UsbApiKind.HarmonyOS };
        var nonMatchingFilter = new UsbDeviceFilter { SourceApiKind = UsbApiKind.Native };

        Assert.True(matchingFilter.Matches(info));
        Assert.False(nonMatchingFilter.Matches(info));
    }

    [Fact]
    public void UsbDeviceFilter_MatchesSourceApiKind()
    {
        var info = new UsbDeviceInfo
        {
            VendorId = 0x18D1,
            ProductId = 0xD00D,
            SourceApiKind = UsbApiKind.HarmonyOS
        };

        var matchingFilter = new UsbDeviceFilter { SourceApiKind = UsbApiKind.HarmonyOS };
        var nonMatchingFilter = new UsbDeviceFilter { SourceApiKind = UsbApiKind.LibUsbDotNet };

        Assert.True(matchingFilter.Matches(info));
        Assert.False(nonMatchingFilter.Matches(info));
    }

    [Fact]
    public void CustomProvider_WithHarmonyOSApiKind_CanBeRegistered()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("harmony-custom", () => new HarmonyOSCustomProvider());

        var capabilities = layer.GetAvailableApiCapabilities();
        var custom = Assert.Single(capabilities, c => c.ApiName == "harmony-custom");
        Assert.Equal(UsbApiKind.HarmonyOS, custom.ApiKind);
    }

    private sealed class HarmonyOSCustomProvider : IUsbApiProvider
    {
        public string ApiName => "harmony-custom";
        public UsbApiKind ApiKind => UsbApiKind.HarmonyOS;
        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
        {
            return Array.Empty<IUsbDeviceSession>();
        }
    }
}
