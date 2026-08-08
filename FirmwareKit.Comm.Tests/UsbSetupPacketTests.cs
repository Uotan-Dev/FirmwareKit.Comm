using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the UsbSetupPacket structure defaults and field round-trips.
/// <para>覆盖 UsbSetupPacket 结构的默认值与字段往返。</para>
/// </summary>
public sealed class UsbSetupPacketTests
{
    [Fact]
    public void Default_AllFieldsZero()
    {
        UsbSetupPacket packet = default;

        Assert.Equal(0, packet.RequestType);
        Assert.Equal(0, packet.Request);
        Assert.Equal(0, packet.Value);
        Assert.Equal(0, packet.Index);
        Assert.Equal(0, packet.Length);
    }

    [Fact]
    public void Fields_RoundTrip()
    {
        var packet = new UsbSetupPacket
        {
            RequestType = 0x80,
            Request = 0x06,
            Value = 0x0100,
            Index = 0x0000,
            Length = 18
        };

        Assert.Equal(0x80, packet.RequestType);
        Assert.Equal(0x06, packet.Request);
        Assert.Equal(0x0100, packet.Value);
        Assert.Equal(0, packet.Index);
        Assert.Equal(18, packet.Length);
    }

    [Fact]
    public void Struct_IsBlittableSize()
    {
        // bmRequestType + bRequest + wValue + wIndex + wLength = 1+1+2+2+2 = 8 bytes.
        Assert.Equal(8, System.Runtime.InteropServices.Marshal.SizeOf<UsbSetupPacket>());
    }
}
