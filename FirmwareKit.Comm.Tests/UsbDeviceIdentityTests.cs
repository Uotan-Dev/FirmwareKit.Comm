using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the stable device identity key builders.
/// <para>覆盖稳定设备身份键的构建。</para>
/// </summary>
public sealed class UsbDeviceIdentityTests
{
    [Fact]
    public void BuildKey_CombinesFieldsWithPipeSeparator()
    {
        var key = UsbDeviceIdentity.BuildKey(Device());

        Assert.Equal("native|Native|0403|6001|FF|FF|00|FT123|/dev/bus/usb/001/002", key);
    }

    [Fact]
    public void BuildKey_NullInfo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UsbDeviceIdentity.BuildKey(null!));
    }

    [Fact]
    public void BuildKey_MissingOptionalFields_UsesEmptyStrings()
    {
        var info = new UsbDeviceInfo { ApiName = "libusb", SourceApiKind = UsbApiKind.LibUsbDotNet, VendorId = 0x1, ProductId = 0x2 };

        var key = UsbDeviceIdentity.BuildKey(info);

        Assert.Equal("libusb|LibUsbDotNet|0001|0002|||||", key);
    }

    [Fact]
    public void BuildPhysicalKey_ExcludesBackendAndPath()
    {
        var a = Device();
        var b = Device();
        b.ApiName = "libusb";
        b.SourceApiKind = UsbApiKind.LibUsbDotNet;
        b.DevicePath = "/dev/bus/usb/099/099";

        // Same physical device reported by two backends must share one physical key.
        Assert.Equal(UsbDeviceIdentity.BuildPhysicalKey(a), UsbDeviceIdentity.BuildPhysicalKey(b));
    }

    [Fact]
    public void BuildPhysicalKey_NullInfo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UsbDeviceIdentity.BuildPhysicalKey(null!));
    }

    [Fact]
    public void BuildKeyAndPhysicalKey_Differ_WhenBackendDiffers()
    {
        var a = Device();
        var b = Device();
        b.SourceApiKind = UsbApiKind.LibUsbDotNet;

        Assert.NotEqual(UsbDeviceIdentity.BuildKey(a), UsbDeviceIdentity.BuildKey(b));
        Assert.Equal(UsbDeviceIdentity.BuildPhysicalKey(a), UsbDeviceIdentity.BuildPhysicalKey(b));
    }

    [Fact]
    public void TryParseKeyFilter_ParsesAdbInterfaceKey()
    {
        // An ADB interface-filtered key (FF|42|01) must rebuild a filter that binds the
        // SAME interface — the by-key open regression this guards is that a null filter
        // binds the first bulk interface (FF|FF|00) and the key never matches.
        // <para>ADB 接口过滤器产生的键（FF|42|01）必须重建绑定同一接口的过滤器——本测试
        // 守护的回归点是：空过滤器会绑定第一个 bulk 接口（FF|FF|00），键永远不匹配。</para>
        const string key = "libusb|LibUsbDotNet|12D1|107D|FF|42|01|W3VBB21818212390|Bus 1 Device 8: 12D1:107D";

        var filter = UsbDeviceIdentity.TryParseKeyFilter(key);

        Assert.NotNull(filter);
        Assert.Equal((ushort)0x12D1, filter!.VendorId);
        Assert.Equal((ushort)0x107D, filter.ProductId);
        Assert.Equal((byte)0xFF, filter.InterfaceClass);
        Assert.Equal((byte)0x42, filter.InterfaceSubClass);
        Assert.Equal((byte)0x01, filter.InterfaceProtocol);
    }

    [Fact]
    public void TryParseKeyFilter_DeviceLevelKey_ParsesInterfaceTriple()
    {
        // A VID/PID-only (device-level) key carries FF|FF|00 as its interface triple; the
        // rebuilt filter must keep it so the no-filter fallback (first bulk interface) is
        // not silently re-introduced.
        // <para>VID/PID 级（设备级）键的接口三元组为 FF|FF|00；重建的过滤器必须保留它，
        // 以免静默回到无过滤器回退（第一个 bulk 接口）。</para>
        const string key = "libusb|LibUsbDotNet|12D1|107D|FF|FF|00|W3VBB21818212390|Bus 1 Device 8: 12D1:107D";

        var filter = UsbDeviceIdentity.TryParseKeyFilter(key);

        Assert.NotNull(filter);
        Assert.Equal((byte)0xFF, filter!.InterfaceClass);
        Assert.Equal((byte)0xFF, filter.InterfaceSubClass);
        Assert.Equal((byte)0x00, filter.InterfaceProtocol);
    }

    [Fact]
    public void TryParseKeyFilter_EmptyOrWhitespace_ReturnsNull()
    {
        Assert.Null(UsbDeviceIdentity.TryParseKeyFilter(null!));
        Assert.Null(UsbDeviceIdentity.TryParseKeyFilter(""));
        Assert.Null(UsbDeviceIdentity.TryParseKeyFilter("   "));
    }

    [Fact]
    public void TryParseKeyFilter_TooFewSegments_ReturnsNull()
    {
        Assert.Null(UsbDeviceIdentity.TryParseKeyFilter("native|Native|0403"));
    }

    [Fact]
    public void TryParseKeyFilter_NonHexSegments_LeaveFieldsUnset()
    {
        // Malformed VID/PID/interface segments must not throw; the parseable fields stay
        // set and the unparseable ones remain null (no filter narrowing).
        // <para>格式错误的 VID/PID/接口段不得抛异常；可解析字段保留，不可解析字段保持
        // null（不做过滤器收窄）。</para>
        const string key = "libusb|LibUsbDotNet|ZZZZ|6001|FF|42|01|serial|path";

        var filter = UsbDeviceIdentity.TryParseKeyFilter(key);

        Assert.NotNull(filter);
        Assert.Null(filter!.VendorId);
        Assert.Equal((ushort)0x6001, filter.ProductId);
        Assert.Equal((byte)0xFF, filter.InterfaceClass);
    }

    private static UsbDeviceInfo Device() => new()
    {
        ApiName = "native",
        SourceApiKind = UsbApiKind.Native,
        VendorId = 0x0403,
        ProductId = 0x6001,
        SerialNumber = "FT123",
        DevicePath = "/dev/bus/usb/001/002",
        InterfaceClass = 0xFF,
        InterfaceSubClass = 0xFF,
        InterfaceProtocol = 0x00
    };
}
