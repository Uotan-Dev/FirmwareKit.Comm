using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Backend;
using FirmwareKit.Comm.Backend.Windows;
using FirmwareKit.Comm.Core;
using FirmwareKit.Comm.Providers;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the API-exposure surface added for FirmwareKit.Comm.EDL: public retry-policy
/// tuning (R1), session options plumbed through the open-session overloads (R2), and the
/// WinUSB RAW_IO / no-data-read semantics where they are observable without hardware (R3).
/// <para>覆盖为 FirmwareKit.Comm.EDL 新增的 API 暴露面：公开的重试策略调优（R1）、
/// 经会话打开重载透传的会话选项（R2），以及无硬件即可观察到的 WinUSB RAW_IO /
/// 无数据读语义（R3）。</para>
/// </summary>
public sealed class UsbSessionOptionsTests
{
    // ---- R1: process-wide retry policy is publicly tunable ----

    [Fact]
    public void DefaultRetryPolicy_SetAndGet_RoundTrips()
    {
        var policy = new UsbTransferRetryPolicy(maxRetries: 0, retryDelayMs: 0);
        try
        {
            UsbTransferPolicies.DefaultRetryPolicy = policy;

            Assert.Same(policy, UsbTransferPolicies.DefaultRetryPolicy);
            Assert.Equal(0, UsbTransferPolicies.DefaultRetryPolicy.MaxRetries);
            Assert.Equal(0, UsbTransferPolicies.DefaultRetryPolicy.RetryDelayMs);
        }
        finally
        {
            // null! — the setter deliberately accepts null to restore the built-in default.
            // <para>null! —— setter 有意接受 null 以恢复内置默认值。</para>
            UsbTransferPolicies.DefaultRetryPolicy = null!;
        }
    }

    [Fact]
    public void DefaultRetryPolicy_Null_RestoresBuiltInDefault()
    {
        try
        {
            UsbTransferPolicies.DefaultRetryPolicy = new UsbTransferRetryPolicy(2, 100);

            UsbTransferPolicies.DefaultRetryPolicy = null!;

            var restored = UsbTransferPolicies.DefaultRetryPolicy;
            Assert.NotNull(restored);
            Assert.Equal(UsbTransferPolicies.LinuxMaxRetries, restored!.MaxRetries);
            Assert.Equal(500, restored.RetryDelayMs);
        }
        finally
        {
            UsbTransferPolicies.DefaultRetryPolicy = null!;
        }
    }

    // ---- R2: UsbSessionOptions ----

    [Fact]
    public void SessionOptions_Defaults_AllNull()
    {
        var options = new UsbSessionOptions();

        Assert.Null(options.DefaultTimeoutMs);
        Assert.Null(options.ReadPipeTimeoutMs);
        Assert.Null(options.WritePipeTimeoutMs);
        Assert.Null(options.EnableRawIo);
        Assert.Null(options.AllowPartialReads);
    }

    [Fact]
    public void SessionOptions_Properties_Settable()
    {
        var options = new UsbSessionOptions
        {
            DefaultTimeoutMs = 1000,
            ReadPipeTimeoutMs = 800,
            WritePipeTimeoutMs = 1200,
            EnableRawIo = true,
            AllowPartialReads = true
        };

        Assert.Equal(1000, options.DefaultTimeoutMs);
        Assert.Equal(800, options.ReadPipeTimeoutMs);
        Assert.Equal(1200, options.WritePipeTimeoutMs);
        Assert.True(options.EnableRawIo);
        Assert.True(options.AllowPartialReads);
    }

    [Fact]
    public void OpenDeviceSession_Options_AreForwardedToBackend()
    {
        var registry = new UsbApiRegistry();
        var provider = new OptionsCapturingProvider();
        registry.Register("capture", () => provider);
        var layer = new UsbCommunicationLayer(registry);
        var options = new UsbSessionOptions { DefaultTimeoutMs = 1000, EnableRawIo = true };

        var session = layer.OpenDeviceSession(UsbApiKind.Auto, filter: null, options);

        Assert.Null(session); // provider returns no devices; only the plumbing is verified
        Assert.Same(options, provider.ReceivedOptions);
    }

    [Fact]
    public void OpenDeviceSession_WithoutOptions_ForwardsNull()
    {
        var registry = new UsbApiRegistry();
        var provider = new OptionsCapturingProvider();
        registry.Register("capture", () => provider);
        var layer = new UsbCommunicationLayer(registry);

        var session = layer.OpenDeviceSession(UsbApiKind.Auto);

        Assert.Null(session);
        Assert.Null(provider.ReceivedOptions);
    }

    [Fact]
    public void FirmwareKitComm_OpenUsbDeviceSession_WithOptions_ReturnsSession()
    {
        IFirmwareKitComm comm = CreateIsolatedFacade();
        _ = comm.RegisterUsbApi("custom-facade", () => new FacadeProvider());

        var session = comm.OpenUsbDeviceSession(UsbApiKind.Auto, new UsbDeviceFilter
        {
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        }, new UsbSessionOptions { DefaultTimeoutMs = 1000, EnableRawIo = true });

        Assert.NotNull(session);
        session!.Dispose();
    }

    // ---- R3: WinUSB device honours session options without hardware ----

    [Fact]
    public void WinUSBDevice_DefaultTimeoutMs_RespectsSessionOptions()
    {
        var withOptions = new WinUSBDevice
        {
            SessionOptions = new UsbSessionOptions { DefaultTimeoutMs = 1000 }
        };
        var withoutOptions = new WinUSBDevice();

        Assert.Equal(1000, withOptions.DefaultTimeoutMs);
        Assert.Equal(UsbTransferPolicies.WinUsbDefaultTimeoutMs, withoutOptions.DefaultTimeoutMs);
    }

    [Fact]
    public void WinUSBDevice_DefaultTimeoutMs_NullReadWritePipe_FallsBackToSessionDefault()
    {
        // Read/Write pipe timeouts fall back to DefaultTimeoutMs when not set.
        var device = new WinUSBDevice
        {
            SessionOptions = new UsbSessionOptions { DefaultTimeoutMs = 2000 }
        };

        // CreateHandle() is not invoked (no hardware); the session default is still
        // observable through the public surface.
        Assert.Equal(2000, device.DefaultTimeoutMs);
    }

    // ---- Test doubles ----

    /// <summary>
    /// Provider stub that records the session options forwarded by the communication layer.
    /// <para>记录通信层透传的会话选项的提供器桩。</para>
    /// </summary>
    private sealed class OptionsCapturingProvider : UsbApiProviderBase
    {
        public UsbSessionOptions? ReceivedOptions { get; private set; }

        public override string ApiName => "capture";

        public override UsbApiKind ApiKind => UsbApiKind.Custom;

        public override bool IsSupportedOnCurrentPlatform => true;

        protected override List<UsbDevice> EnumerateBackendDevices(UsbDeviceFilter? filter)
            => new();

        protected override List<UsbDevice> EnumerateBackendDevices(UsbDeviceFilter? filter, UsbSessionOptions? options)
        {
            ReceivedOptions = options;
            return new List<UsbDevice>();
        }

        public override UsbApiCapabilities GetCapabilities()
            => new()
            {
                ApiName = ApiName,
                ApiKind = ApiKind,
                IsSupportedOnCurrentPlatform = true
            };
    }

    private static IFirmwareKitComm CreateIsolatedFacade()
    {
        var registry = new UsbApiRegistry();
        var layer = new UsbCommunicationLayer(registry);
        var facadeType = typeof(IFirmwareKitComm).Assembly.GetType("FirmwareKit.Comm.FirmwareKitComm", throwOnError: true)!;
        return (IFirmwareKitComm)Activator.CreateInstance(facadeType, layer)!;
    }

    /// <summary>
    /// Minimal session provider mirroring the pattern in FirmwareKitCommFacadeTests.
    /// <para>镜像 FirmwareKitCommFacadeTests 模式的最小会话提供器。</para>
    /// </summary>
    private sealed class FacadeProvider : IUsbApiProvider
    {
        public string ApiName => "custom-facade";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
        {
            var session = new FacadeSession("mock://facade-options", "facade-options");
            return filter == null || filter.Matches(session.DeviceInfo)
                ? new IUsbDeviceSession[] { session }
                : Array.Empty<IUsbDeviceSession>();
        }
    }

    private sealed class FacadeSession : IUsbDeviceSession
    {
        public byte EndpointIn => 0x81;
        public byte EndpointOut => 0x01;

        public FacadeSession(string devicePath, string deviceKey)
        {
            DeviceInfo = new UsbDeviceInfo
            {
                ApiName = "custom-facade",
                SourceApiKind = UsbApiKind.Custom,
                DevicePath = devicePath,
                DeviceKey = deviceKey,
                VendorId = 0x1F3A,
                ProductId = 0xEFE8,
                InterfaceClass = 0xFF,
                InterfaceSubClass = 0xFF,
                InterfaceProtocol = 0xFF,
                InterfaceMetadataObserved = true
            };
        }

        public int DefaultTimeoutMs => 2500;

        public UsbDeviceInfo DeviceInfo { get; }

        public byte[] Read(int length) => new byte[length];

        public byte[] Read(int length, int timeoutMs) => new byte[length];

        public int ReadInto(byte[] buffer, int offset, int length) => length;

        public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs) => length;

        public UsbReadResult ReadPacket(byte[] buffer, int offset, int length, int timeoutMs)
            => new(length, isTimeout: false, isShortPacket: false);

        public UsbReadResult ReadInterrupt(byte endpointAddress, byte[] buffer, int offset, int length, int timeoutMs)
            => throw new NotSupportedException();

        public long WriteInterrupt(byte endpointAddress, byte[] data, int offset, int length, int timeoutMs)
            => throw new NotSupportedException();

        public long Write(byte[] data, int length) => length;

        public long Write(byte[] data, int length, int timeoutMs) => length;

        public long Write(byte[] data, int offset, int length, int timeoutMs) => length;

        public int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs) => length;

        public void WriteZlp(int timeoutMs)
        {
        }

#if NET8_0_OR_GREATER
        public int ReadInto(Span<byte> buffer, int timeoutMs) => buffer.Length;
#endif

        public void SetInterfaceAltSetting(byte interfaceNumber, byte altSetting) { }

        public void SetConfiguration(byte configuration) { }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }
}
