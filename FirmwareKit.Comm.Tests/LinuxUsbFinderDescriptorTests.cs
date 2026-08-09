using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Backend.Linux;

namespace FirmwareKit.Comm.IntegrationTests;

/// <summary>
/// Tests the pure usbfs descriptor parser (LinuxUsbFinder.TryParseDescriptor) with
/// constructed descriptor bytes, so enumeration correctness is verified without hardware.
/// <para>用构造的描述符字节测试 usbfs 描述符解析纯函数（LinuxUsbFinder.TryParseDescriptor），
/// 使枚举正确性可在无硬件环境下得到验证。</para>
/// </summary>
public sealed class LinuxUsbFinderDescriptorTests
{
    /// <summary>
    /// Builds a device descriptor (18 B) followed by one configuration (9 B), one interface
    /// (9 B) and the given endpoint descriptors, mirroring a single usbfs read().
    /// <para>构建设备描述符（18 B）+ 一个配置（9 B）+ 一个接口（9 B）+ 给定端点描述符，
    /// 模拟一次 usbfs read() 的返回。</para>
    /// </summary>
    private static byte[] BuildDescriptor(
        ushort vid,
        ushort pid,
        byte interfaceClass = 0xFF,
        byte interfaceSubClass = 0xFF,
        byte interfaceProtocol = 0xFF,
        params (byte address, byte attributes)[] endpoints)
    {
        var buffer = new List<byte>
        {
            18, 1,                        // bLength, bDescriptorType (device)
            0x00, 0x02,                   // bcdUSB 2.0
            0, 0, 0, 64,                  // class/subclass/protocol, bMaxPacketSize0
            (byte)(vid & 0xFF), (byte)(vid >> 8),       // idVendor LE
            (byte)(pid & 0xFF), (byte)(pid >> 8),       // idProduct LE
            0x00, 0x01,                   // bcdDevice
            0, 0, 0, 1                    // iManufacturer, iProduct, iSerialNumber, bNumConfigurations
        };

        int configLen = 9 + 9 + endpoints.Length * 7;
        buffer.AddRange(new byte[]
        {
            9, 2,                         // bLength, bDescriptorType (configuration)
            (byte)(configLen & 0xFF), (byte)(configLen >> 8), // wTotalLength LE
            1, 1, 0, 0x80, 50             // bNumInterfaces, bConfigurationValue, iConfiguration, bmAttributes, bMaxPower
        });

        buffer.AddRange(new byte[]
        {
            9, 4,                         // bLength, bDescriptorType (interface)
            0, 0, (byte)endpoints.Length, // bInterfaceNumber, bAlternateSetting, bNumEndpoints
            interfaceClass, interfaceSubClass, interfaceProtocol, 0
        });

        foreach ((byte address, byte attributes) in endpoints)
        {
            buffer.AddRange(new byte[]
            {
                7, 5, address, attributes, // bLength, bDescriptorType, bEndpointAddress, bmAttributes
                0x00, 0x02, 0              // wMaxPacketSize 512 LE, bInterval
            });
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Builds a device descriptor (18 B) followed by one configuration (9 B) and the given
    /// interfaces, each with its own endpoints — mirrors a composite device (e.g. FT2232H)
    /// inside a single usbfs read().
    /// <para>构建设备描述符（18 B）+ 一个配置（9 B）+ 给定接口（各自带端点），
    /// 模拟复合设备（如 FT2232H）在一次 usbfs read() 中的返回。</para>
    /// </summary>
    private static byte[] BuildMultiInterfaceDescriptor(
        ushort vid,
        ushort pid,
        params (byte interfaceNumber, byte interfaceClass, byte interfaceSubClass, byte interfaceProtocol, (byte address, byte attributes)[] endpoints)[] interfaces)
    {
        var buffer = new List<byte>
        {
            18, 1,                        // bLength, bDescriptorType (device)
            0x00, 0x02,                   // bcdUSB 2.0
            0, 0, 0, 64,                  // class/subclass/protocol, bMaxPacketSize0
            (byte)(vid & 0xFF), (byte)(vid >> 8),       // idVendor LE
            (byte)(pid & 0xFF), (byte)(pid >> 8),       // idProduct LE
            0x00, 0x01,                   // bcdDevice
            0, 0, 0, 1                    // iManufacturer, iProduct, iSerialNumber, bNumConfigurations
        };

        int configLen = 9;
        foreach (var ifc in interfaces)
        {
            configLen += 9 + ifc.endpoints.Length * 7;
        }
        buffer.AddRange(new byte[]
        {
            9, 2,                         // bLength, bDescriptorType (configuration)
            (byte)(configLen & 0xFF), (byte)(configLen >> 8), // wTotalLength LE
            (byte)interfaces.Length, 1, 0, 0x80, 50 // bNumInterfaces, bConfigurationValue, iConfiguration, bmAttributes, bMaxPower
        });

        foreach (var ifc in interfaces)
        {
            buffer.AddRange(new byte[]
            {
                9, 4,                         // bLength, bDescriptorType (interface)
                ifc.interfaceNumber, 0, (byte)ifc.endpoints.Length, // bInterfaceNumber, bAlternateSetting, bNumEndpoints
                ifc.interfaceClass, ifc.interfaceSubClass, ifc.interfaceProtocol, 0
            });

            foreach ((byte address, byte attributes) in ifc.endpoints)
            {
                buffer.AddRange(new byte[]
                {
                    7, 5, address, attributes, // bLength, bDescriptorType, bEndpointAddress, bmAttributes
                    0x00, 0x02, 0              // wMaxPacketSize 512 LE, bInterval
                });
            }
        }

        return buffer.ToArray();
    }

    [Fact]
    public void TryParseDescriptor_ParsesVidPidInterfaceAndBulkPair()
    {
        byte[] desc = BuildDescriptor(0x0403, 0x6001,
            endpoints: new (byte, byte)[] { (0x81, 0x02), (0x01, 0x02) }); // IN/OUT bulk

        var info = LinuxUsbFinder.TryParseDescriptor(desc, desc.Length, filter: null);

        Assert.NotNull(info);
        Assert.Equal((ushort)0x0403, info!.VendorId);
        Assert.Equal((ushort)0x6001, info.ProductId);
        Assert.Equal((byte)0xFF, info.InterfaceClass);
        Assert.Equal((byte)0, info.InterfaceId);
        Assert.Equal((byte)0x81, info.EndpointIn);
        Assert.Equal((byte)0x01, info.EndpointOut);
        Assert.Single(info.Interfaces);
        Assert.Equal(2, info.Interfaces[0].Endpoints.Count);
    }

    [Fact]
    public void TryParseDescriptor_AppliesVendorFilter()
    {
        byte[] desc = BuildDescriptor(0x0403, 0x6001,
            endpoints: new (byte, byte)[] { (0x81, 0x02), (0x01, 0x02) });

        var info = LinuxUsbFinder.TryParseDescriptor(desc, desc.Length, new UsbDeviceFilter { VendorId = 0x1234 });

        Assert.Null(info);
    }

    [Fact]
    public void TryParseDescriptor_AppliesProductFilter()
    {
        byte[] desc = BuildDescriptor(0x0403, 0x6001,
            endpoints: new (byte, byte)[] { (0x81, 0x02), (0x01, 0x02) });

        var info = LinuxUsbFinder.TryParseDescriptor(desc, desc.Length, new UsbDeviceFilter { ProductId = 0x9999 });

        Assert.Null(info);
    }

    [Fact]
    public void TryParseDescriptor_InterfaceClassFilter_MatchesVendorClass()
    {
        byte[] desc = BuildDescriptor(0x18D1, 0xD00D,
            endpoints: new (byte, byte)[] { (0x81, 0x02), (0x01, 0x02) });

        var info = LinuxUsbFinder.TryParseDescriptor(desc, desc.Length, new UsbDeviceFilter { InterfaceClass = 0xFF });

        Assert.NotNull(info);
        Assert.Equal((byte)0xFF, info!.InterfaceClass);
    }

    [Fact]
    public void TryParseDescriptor_ExplicitEndpoints_WinOverFirstBulkPair()
    {
        // Rockchip-style: the device exposes 0x81/0x01 as the first bulk pair but the
        // loader targets 0x82/0x02; the filter must select the requested endpoints.
        // <para>Rockchip 风格：设备首对 bulk 端点是 0x81/0x01，但 loader 针对 0x82/0x02；
        // 过滤器必须选中请求的端点。</para>
        byte[] desc = BuildDescriptor(0x2207, 0x1000,
            endpoints: new (byte, byte)[] { (0x81, 0x02), (0x01, 0x02), (0x82, 0x02), (0x02, 0x02) });

        var info = LinuxUsbFinder.TryParseDescriptor(desc, desc.Length,
            new UsbDeviceFilter { EndpointAddressIn = 0x82, EndpointAddressOut = 0x02 });

        Assert.NotNull(info);
        Assert.Equal((byte)0x82, info!.EndpointIn);
        Assert.Equal((byte)0x02, info.EndpointOut);
    }

    [Fact]
    public void TryParseDescriptor_EndpointFilter_MissingEndpointRejects()
    {
        byte[] desc = BuildDescriptor(0x2207, 0x1000,
            endpoints: new (byte, byte)[] { (0x81, 0x02), (0x01, 0x02) });

        // Requests an IN endpoint the interface does not expose; must reject.
        // <para>请求接口未暴露的 IN 端点；必须拒绝。</para>
        var info = LinuxUsbFinder.TryParseDescriptor(desc, desc.Length,
            new UsbDeviceFilter { EndpointAddressIn = 0x82, EndpointAddressOut = 0x02 });

        Assert.Null(info);
    }

    [Fact]
    public void TryParseDescriptor_InterruptOnlyDevice_MatchesWithInOnlyFilter()
    {
        // HID-style interrupt-only device (no OUT pipe); IN-only filter must match.
        // <para>HID 风格的中断仅设备（无 OUT 管道）；仅 IN 过滤器必须匹配。</para>
        byte[] desc = BuildDescriptor(0x046D, 0xC077,
            endpoints: ((byte)0x81, (byte)0x03)); // interrupt IN

        var info = LinuxUsbFinder.TryParseDescriptor(desc, desc.Length,
            new UsbDeviceFilter { EndpointAddressIn = 0x81 });

        Assert.NotNull(info);
        Assert.Equal((byte)0x81, info!.EndpointIn);
        Assert.Equal((byte)0, info.EndpointOut);
    }

    [Fact]
    public void TryParseDescriptor_TruncatedDescriptor_ReturnsNull()
    {
        byte[] desc = BuildDescriptor(0x0403, 0x6001,
            endpoints: new (byte, byte)[] { (0x81, 0x02), (0x01, 0x02) });

        Assert.Null(LinuxUsbFinder.TryParseDescriptor(desc, length: 10, filter: null));
    }

    [Fact]
    public void TryParseDescriptor_NoBulkOrInterruptEndpoints_ReturnsNull()
    {
        // Endpoints exist but are neither bulk (0x02) nor interrupt (0x03) — no I/O pair.
        // <para>端点存在但既非 bulk（0x02）也非中断（0x03）——无 I/O 端点对。</para>
        byte[] desc = BuildDescriptor(0x0403, 0x6001,
            endpoints: new (byte, byte)[] { (0x81, 0x01), (0x01, 0x01) }); // control-type endpoints

        Assert.Null(LinuxUsbFinder.TryParseDescriptor(desc, desc.Length, filter: null));
    }

    [Fact]
    public void TryParseDescriptor_MultiInterface_ReportsAllInterfaces()
    {
        // FT2232H-style composite: interface 0 (0x81/0x02) and interface 1 (0x83/0x04).
        // The parser must report BOTH interfaces, not just the first one that matches.
        // <para>FT2232H 风格复合设备：接口 0（0x81/0x02）与接口 1（0x83/0x04）。
        // 解析器必须上报全部接口，而非仅上报首个命中的接口。</para>
        byte[] desc = BuildMultiInterfaceDescriptor(
            0x0403, 0x6010,
            (0, 0xFF, 0xFF, 0xFF, new (byte, byte)[] { (0x81, 0x02), (0x02, 0x02) }),
            (1, 0xFF, 0xFF, 0xFF, new (byte, byte)[] { (0x83, 0x02), (0x04, 0x02) }));

        var info = LinuxUsbFinder.TryParseDescriptor(desc, desc.Length, filter: null);

        Assert.NotNull(info);
        Assert.Equal(2, info!.Interfaces.Count);
        Assert.Equal((byte)0, info.Interfaces[0].InterfaceNumber);
        Assert.Equal(2, info.Interfaces[0].Endpoints.Count);
        Assert.Equal((byte)1, info.Interfaces[1].InterfaceNumber);
        Assert.Equal(2, info.Interfaces[1].Endpoints.Count);
        Assert.Equal((byte)0x81, info.EndpointIn);
        Assert.Equal((byte)0x02, info.EndpointOut);
    }

    [Fact]
    public void TryParseDescriptor_MultiInterface_InterfaceNumberFilter_SelectsSecondInterface()
    {
        // A filter targeting interface 1 must bind that interface's endpoints while the
        // full interface list is still reported.
        // <para>针对接口 1 的过滤器必须绑定该接口的端点，同时仍上报完整接口列表。</para>
        byte[] desc = BuildMultiInterfaceDescriptor(
            0x0403, 0x6010,
            (0, 0xFF, 0xFF, 0xFF, new (byte, byte)[] { (0x81, 0x02), (0x02, 0x02) }),
            (1, 0xFF, 0xFF, 0xFF, new (byte, byte)[] { (0x83, 0x02), (0x04, 0x02) }));

        var info = LinuxUsbFinder.TryParseDescriptor(desc, desc.Length,
            new UsbDeviceFilter { InterfaceNumber = 1 });

        Assert.NotNull(info);
        Assert.Equal(2, info!.Interfaces.Count);
        Assert.Equal((byte)1, info.InterfaceId);
        Assert.Equal((byte)0x83, info.EndpointIn);
        Assert.Equal((byte)0x04, info.EndpointOut);
    }
}
