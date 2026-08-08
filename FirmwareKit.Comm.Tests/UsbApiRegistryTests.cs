using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Core;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the USB API registry: registration, lookup, ordering and defaults.
/// <para>覆盖 USB API 注册表：注册、查找、排序与默认项。</para>
/// </summary>
public sealed class UsbApiRegistryTests
{
    [Fact]
    public void Register_AddsProvider_AndRaisesEvents()
    {
        var registry = new UsbApiRegistry();
        string? registeredName = null;
        int providerEvents = 0;
        registry.ProviderNameRegistered += name => registeredName = name;
        registry.ProviderRegistered += _ => providerEvents++;

        registry.Register("test", () => new FakeProvider("test"));

        Assert.Equal("test", registeredName);
        Assert.Equal(1, providerEvents);
        Assert.True(registry.TryCreate("test", out var provider));
        Assert.Equal("test", provider!.ApiName);
    }

    [Fact]
    public void Register_IsCaseInsensitive_ReplacesExisting()
    {
        var registry = new UsbApiRegistry();
        registry.Register("Alpha", () => new FakeProvider("Alpha"));

        Assert.True(registry.TryCreate("ALPHA", out _));

        registry.Register("alpha", () => new FakeProvider("alpha"));

        Assert.Single(registry.GetApiNames());
    }

    [Fact]
    public void Register_NullOrWhitespaceName_Throws()
    {
        var registry = new UsbApiRegistry();
        Assert.Throws<ArgumentException>(() => registry.Register(null!, () => new FakeProvider("x")));
        Assert.Throws<ArgumentException>(() => registry.Register("  ", () => new FakeProvider("x")));
    }

    [Fact]
    public void Register_NullFactory_Throws()
    {
        var registry = new UsbApiRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register("x", null!));
    }

    [Fact]
    public void TryCreate_UnknownApi_ReturnsFalse()
    {
        var registry = new UsbApiRegistry();
        Assert.False(registry.TryCreate("missing", out var provider));
        Assert.Null(provider);
    }

    [Fact]
    public void TryCreate_CaseInsensitive_Matches()
    {
        var registry = new UsbApiRegistry();
        registry.Register("Native", () => new FakeProvider("Native"));

        Assert.True(registry.TryCreate("native", out var provider));
        Assert.Equal("Native", provider!.ApiName);
    }

    [Fact]
    public void CreateAll_CreatesEveryRegisteredProvider()
    {
        var registry = new UsbApiRegistry();
        registry.Register("a", () => new FakeProvider("a"));
        registry.Register("b", () => new FakeProvider("b"));

        Assert.Equal(2, registry.CreateAll().Count);
    }

    [Fact]
    public void GetApiNames_SortedOrdinalIgnoreCase()
    {
        var registry = new UsbApiRegistry();
        registry.Register("Zeta", () => new FakeProvider("Zeta"));
        registry.Register("alpha", () => new FakeProvider("alpha"));

        Assert.Equal(new[] { "alpha", "Zeta" }, registry.GetApiNames());
    }

    [Fact]
    public void CreateDefault_RegistersNativeLibusbHarmony()
    {
        var registry = UsbApiRegistry.CreateDefault();

        var names = registry.GetApiNames();
        Assert.Contains("native", names);
        Assert.Contains("libusb", names);
        Assert.Contains("harmony", names);
    }

    /// <summary>
    /// Minimal provider stub for registry tests.
    /// <para>注册表测试用的最小提供器桩。</para>
    /// </summary>
    private sealed class FakeProvider : IUsbApiProvider
    {
        public FakeProvider(string apiName) => ApiName = apiName;

        public string ApiName { get; }

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
            => Array.Empty<IUsbDeviceSession>();
    }
}
