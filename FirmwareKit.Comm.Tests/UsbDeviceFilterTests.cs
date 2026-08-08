using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers every UsbDeviceFilter matching criterion.
/// <para>覆盖 UsbDeviceFilter 的全部匹配条件。</para>
/// </summary>
public sealed class UsbDeviceFilterTests
{
    [Fact]
    public void Matches_NoCriteria_AlwaysTrue()
    {
        Assert.True(new UsbDeviceFilter().Matches(Device()));
    }

    [Fact]
    public void Matches_VendorId_Exact()
    {
        Assert.True(new UsbDeviceFilter { VendorId = 0x0403 }.Matches(Device()));
    }

    [Fact]
    public void Matches_VendorId_Mismatch_ReturnsFalse()
    {
        Assert.False(new UsbDeviceFilter { VendorId = 0x1234 }.Matches(Device()));
    }

    [Fact]
    public void Matches_ProductId_Exact()
    {
        Assert.True(new UsbDeviceFilter { ProductId = 0x6001 }.Matches(Device()));
    }

    [Fact]
    public void Matches_ProductId_Mismatch_ReturnsFalse()
    {
        Assert.False(new UsbDeviceFilter { ProductId = 0x0001 }.Matches(Device()));
    }

    [Fact]
    public void Matches_SerialNumber_IgnoreCase()
    {
        Assert.True(new UsbDeviceFilter { SerialNumber = "ft123" }.Matches(Device()));
    }

    [Fact]
    public void Matches_SerialNumber_Mismatch_ReturnsFalse()
    {
        Assert.False(new UsbDeviceFilter { SerialNumber = "other" }.Matches(Device()));
    }

    [Fact]
    public void Matches_DevicePathContains_SubstringIgnoreCase()
    {
        Assert.True(new UsbDeviceFilter { DevicePathContains = "USB/001" }.Matches(Device()));
    }

    [Fact]
    public void Matches_DevicePathContains_Missing_ReturnsFalse()
    {
        Assert.False(new UsbDeviceFilter { DevicePathContains = "nope" }.Matches(Device()));
    }

    [Fact]
    public void Matches_SourceApiKind()
    {
        Assert.True(new UsbDeviceFilter { SourceApiKind = UsbApiKind.Native }.Matches(Device()));
        Assert.False(new UsbDeviceFilter { SourceApiKind = UsbApiKind.LibUsbDotNet }.Matches(Device()));
    }

    [Fact]
    public void Matches_InterfaceClass_SubClass_Protocol()
    {
        var filter = new UsbDeviceFilter { InterfaceClass = 0xFF, InterfaceSubClass = 0xFF, InterfaceProtocol = 0x00 };
        Assert.True(filter.Matches(Device()));
        Assert.False(new UsbDeviceFilter { InterfaceClass = 0x02 }.Matches(Device()));
    }

    [Fact]
    public void Matches_InterfaceNumber_ChecksInterfacesList()
    {
        Assert.True(new UsbDeviceFilter { InterfaceNumber = 0 }.Matches(Device()));
        Assert.False(new UsbDeviceFilter { InterfaceNumber = 3 }.Matches(Device()));
    }

    [Fact]
    public void Matches_EndpointAddresses_Exact()
    {
        // Device exposes 0x81/0x01 on interface 0 (see Device()).
        var filter = new UsbDeviceFilter { EndpointAddressIn = 0x81, EndpointAddressOut = 0x01 };
        Assert.True(filter.Matches(Device()));
    }

    [Fact]
    public void Matches_EndpointAddress_Missing_ReturnsFalse()
    {
        Assert.False(new UsbDeviceFilter { EndpointAddressIn = 0x82 }.Matches(Device()));
        Assert.False(new UsbDeviceFilter { EndpointAddressOut = 0x02 }.Matches(Device()));
        Assert.False(new UsbDeviceFilter { EndpointAddressIn = 0x81, EndpointAddressOut = 0x02 }.Matches(Device()));
    }

    [Fact]
    public void Matches_EndpointAddresses_IgnoreWhenNotSet()
    {
        Assert.True(new UsbDeviceFilter { EndpointAddressIn = null, EndpointAddressOut = null }.Matches(Device()));
    }

    [Fact]
    public void Matches_AllCriteriaTogether()
    {
        var filter = new UsbDeviceFilter
        {
            VendorId = 0x0403,
            ProductId = 0x6001,
            SerialNumber = "FT123",
            DevicePathContains = "/dev/bus",
            SourceApiKind = UsbApiKind.Native,
            InterfaceNumber = 0,
            EndpointAddressIn = 0x81,
            EndpointAddressOut = 0x01
        };
        Assert.True(filter.Matches(Device()));
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
        InterfaceProtocol = 0x00,
        Interfaces = new[]
        {
            new UsbInterfaceInfo
            {
                InterfaceNumber = 0,
                Class = 0xFF,
                Endpoints = new[]
                {
                    new UsbEndpointInfo { EndpointAddress = 0x81, Attributes = 0x02 },
                    new UsbEndpointInfo { EndpointAddress = 0x01, Attributes = 0x02 }
                }
            }
        }
    };
}
