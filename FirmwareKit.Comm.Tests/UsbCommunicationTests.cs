using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Core;
using System.Collections.Concurrent;

namespace FirmwareKit.Comm.IntegrationTests;

public sealed class UsbCommunicationLayerIntegrationTests
{
    [Fact]
    public void DefaultLayer_ContainsNativeAndLibUsbApis()
    {
        var layer = new UsbCommunicationLayer();
        var apis = layer.GetAvailableApis();

        Assert.Contains("native", apis, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("libusb", apis, StringComparer.OrdinalIgnoreCase);
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

        var devices = layer.EnumerateDevices(UsbApiKind.Auto, filter);
        Assert.NotNull(devices);
        Assert.Empty(devices);
    }

    [Fact]
    public async Task EnumerateDevicesAsync_DoesNotThrow()
    {
        var layer = new UsbCommunicationLayer(new UsbApiRegistry());
        _ = layer.RegisterApi("custom", () => new EmptyProvider());

        var devices = await layer.EnumerateDevicesAsync(UsbApiKind.Auto, new UsbDeviceFilter());
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

        var devices = layer.EnumerateDevices(UsbApiKind.Auto, new UsbDeviceFilter());
        Assert.Empty(devices);
    }

    [Fact]
    public void GetAvailableApiCapabilities_ReportsBackendNotes()
    {
        var layer = new UsbCommunicationLayer();
        var capabilities = layer.GetAvailableApiCapabilities();

        var native = Assert.Single(capabilities, item => string.Equals(item.ApiName, "native", StringComparison.OrdinalIgnoreCase));
        Assert.True(native.SupportsNativeDiscovery);
        Assert.False(native.SupportsNativeAsyncIo);
        Assert.False(native.SupportsNativeHotPlugMonitoring);
        Assert.False(native.RequiresExternalRuntime);
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

        var read = await asyncSession.ReadAsync(4, 2000);
        var buffer = new byte[8];
        var count = await asyncSession.ReadIntoAsync(buffer, 0, 8, 3000);
        var written = await asyncSession.WriteAsync(new byte[] { 1, 2, 3 }, 3, 4000);
        await asyncSession.ResetAsync();

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

        var devices = layer.EnumerateDevices(UsbApiKind.Auto, filter);
        Assert.Empty(devices);

        Assert.NotNull(provider.LastFilter);
        Assert.Equal((byte)0xFF, provider.LastFilter!.InterfaceClass);
        Assert.Equal((byte)0xFF, provider.LastFilter.InterfaceSubClass);
        Assert.Equal((byte)0xFF, provider.LastFilter.InterfaceProtocol);
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
        });

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

        _ = layer.EnumerateDevices(UsbApiKind.Auto, new UsbDeviceFilter());
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
            fireInitialSnapshot: false);

        provider.DevicePresent = true;
        Assert.True(signal.Wait(TimeSpan.FromSeconds(3)));

        signal.Reset();
        provider.DevicePresent = false;
        Assert.True(signal.Wait(TimeSpan.FromSeconds(3)));

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
            onError: ex => capturedError = ex);

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
                    InterfaceProtocol = 0xFF
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
                    SerialNumber = "dev-1"
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
            ProductId = 0xEFE8
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

        public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
        {
            LastControlTimeoutMs = timeoutMs;
            return length;
        }

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

        public Task<int> ControlTransferAsync(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
        {
            LastControlTimeoutMs = timeoutMs;
            return Task.FromResult(length);
        }

        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            AsyncResetInvoked = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
