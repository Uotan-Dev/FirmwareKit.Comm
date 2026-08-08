using FirmwareKit.Comm.Abstractions;

namespace FirmwareKit.Comm.Tests;

/// <summary>
/// Covers the USB exception hierarchy constructors and payload properties.
/// <para>覆盖 USB 异常体系构造器与载荷属性。</para>
/// </summary>
public sealed class UsbExceptionTests
{
    [Fact]
    public void UsbTransferException_Message_AndNativeErrorCode()
    {
        var ex = new UsbTransferException("boom", -7);

        Assert.Equal("boom", ex.Message);
        Assert.Equal(-7, ex.NativeErrorCode);
        Assert.IsAssignableFrom<IOException>(ex);
    }

    [Fact]
    public void UsbTransferException_InnerException_AndBackend()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new UsbTransferException("boom", inner) { Backend = "linux" };

        Assert.Same(inner, ex.InnerException);
        Assert.Equal("linux", ex.Backend);
        Assert.Null(ex.NativeErrorCode);
    }

    [Fact]
    public void UsbDeviceOpenException_DevicePathAndNativeErrorCode()
    {
        var ex = new UsbDeviceOpenException("cannot open", "/dev/bus/usb/001/002", 13);

        Assert.Equal("cannot open", ex.Message);
        Assert.Equal("/dev/bus/usb/001/002", ex.DevicePath);
        Assert.Equal(13, ex.NativeErrorCode);
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public void UsbDeviceOpenException_PlainMessage_HasNullPayload()
    {
        var ex = new UsbDeviceOpenException("nope");

        Assert.Null(ex.DevicePath);
        Assert.Null(ex.NativeErrorCode);
    }

    [Fact]
    public void UsbDeviceHandleClosedException_IsInvalidOperationException_WithInner()
    {
        var inner = new Exception("cause");
        var ex = new UsbDeviceHandleClosedException("closed", inner);

        Assert.Equal("closed", ex.Message);
        Assert.Same(inner, ex.InnerException);
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public void UsbDeviceDisconnectedException_IsIOException_WithNativeErrorCode()
    {
        var ex = new UsbDeviceDisconnectedException("unplugged", -110);

        Assert.Equal("unplugged", ex.Message);
        Assert.Equal(-110, ex.NativeErrorCode);
        Assert.IsAssignableFrom<IOException>(ex);
    }

    [Fact]
    public void UsbDeviceDisconnectedException_InnerException()
    {
        var inner = new Exception("cause");
        var ex = new UsbDeviceDisconnectedException("unplugged", inner);

        Assert.Same(inner, ex.InnerException);
        Assert.Null(ex.NativeErrorCode);
    }
}
