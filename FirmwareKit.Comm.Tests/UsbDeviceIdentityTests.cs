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
