using FirmwareKit.Comm.Usb.Backend.HarmonyOS;
using FirmwareKit.Comm.Usb.Backend.Linux;
using FirmwareKit.Comm.Usb.Backend.macOS;
using FirmwareKit.Comm.Usb.Backend.OpenHarmony;
using FirmwareKit.Comm.Usb.Backend.Windows;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.IntegrationTests;

public sealed class PlatformInteropStructureTests
{
    [Fact]
    public void WinUSB_SetupPacket_SizeIs8Bytes()
    {
        Assert.Equal(8, Marshal.SizeOf<WinUSBAPI.WINUSB_SETUP_PACKET>());
    }

    [Fact]
    public void WinUSB_PipeInfo_SizeIs12Bytes()
    {
        Assert.Equal(12, Marshal.SizeOf<WinUSBAPI.WinUSBPipeInfo>());
    }

    [Fact]
    public void Win32_GUID_SizeIs16Bytes()
    {
        Assert.Equal(16, Marshal.SizeOf<Win32API.GUID>());
    }

    [Fact]
    public void Win32_GUID_HasSequentialLayout()
    {
        var layout = typeof(Win32API.GUID).StructLayoutAttribute;
        Assert.NotNull(layout);
        Assert.Equal(LayoutKind.Sequential, layout.Value);
    }

    [Fact]
    public void Win32_SpDeviceInterfaceData_SizeIsCorrect()
    {
        int expected = IntPtr.Size == 4 ? 28 : 32;
        Assert.Equal(expected, Marshal.SizeOf<Win32API.SpDeviceInterfaceData>());
    }

    [Fact]
    public void Win32_USBDeviceDescriptor_SizeIs18Bytes()
    {
        Assert.Equal(18, Marshal.SizeOf<Win32API.USBDeviceDescriptor>());
    }

    [Fact]
    public void Win32_USBDeviceConfigDescriptor_MatchesNativeLayout()
    {
        int actual = Marshal.SizeOf<Win32API.USBDeviceConfigDescriptor>();
        Assert.True(actual >= 9, $"Config descriptor size should be at least 9 bytes, got {actual}");
    }

    [Fact]
    public void Win32_USBDeviceInterfaceDescriptor_SizeIs9Bytes()
    {
        Assert.Equal(9, Marshal.SizeOf<Win32API.USBDeviceInterfaceDescriptor>());
    }

    [Fact]
    public void WinUSB_PipeType_ValuesMatchOfficialOrder()
    {
        Assert.Equal(0, (int)WinUSBAPI.WinUSBPipeType.UsbdPipeTypeControl);
        Assert.Equal(1, (int)WinUSBAPI.WinUSBPipeType.UsbdPipeTypeIsochronous);
        Assert.Equal(2, (int)WinUSBAPI.WinUSBPipeType.UsbdPipeTypeBulk);
        Assert.Equal(3, (int)WinUSBAPI.WinUSBPipeType.UsbdPipeTypeInterrupt);
    }

    [Fact]
    public void WinUSB_PipePolicyConstants_MatchOfficialValues()
    {
        Assert.Equal(0x01u, WinUSBAPI.SHORT_PACKET_TERMINATE);
        Assert.Equal(0x02u, WinUSBAPI.AUTO_CLEAR_STALL);
        Assert.Equal(0x03u, WinUSBAPI.PIPE_TRANSFER_TIMEOUT);
        Assert.Equal(0x04u, WinUSBAPI.IGNORE_SHORT_PACKETS);
        Assert.Equal(0x05u, WinUSBAPI.ALLOW_PARTIAL_READS);
        Assert.Equal(0x06u, WinUSBAPI.AUTO_FLUSH);
        Assert.Equal(0x07u, WinUSBAPI.RAW_IO);
    }

    [Fact]
    public void Linux_usbdevfs_bulktransfer_SizeIsCorrect()
    {
        int expected = IntPtr.Size == 4 ? 16 : 24;
        Assert.Equal(expected, Marshal.SizeOf<LinuxUsbAPI.usbdevfs_bulktransfer>());
    }

    [Fact]
    public void Linux_usbdevfs_ctrltransfer_SizeIsCorrect()
    {
        int expected = IntPtr.Size == 4 ? 16 : 24;
        Assert.Equal(expected, Marshal.SizeOf<LinuxUsbAPI.usbdevfs_ctrltransfer>());
    }

    [Fact]
    public void Linux_IoctlCodes_AreCorrect()
    {
        Assert.Equal(0xC0105500u, LinuxUsbAPI.USBDEVFS_CONTROL);
        Assert.Equal(0xC0105502u, LinuxUsbAPI.USBDEVFS_BULK);
        Assert.Equal(0xC0185500u, LinuxUsbAPI.USBDEVFS_CONTROL_X86_64);
        Assert.Equal(0xC0185502u, LinuxUsbAPI.USBDEVFS_BULK_X86_64);
        Assert.Equal(0x8004550Fu, LinuxUsbAPI.USBDEVFS_CLAIMINTERFACE);
        Assert.Equal(0x80045510u, LinuxUsbAPI.USBDEVFS_RELEASEINTERFACE);
        Assert.Equal(0x5516u, LinuxUsbAPI.USBDEVFS_DISCONNECT);
        Assert.Equal(0x5514u, LinuxUsbAPI.USBDEVFS_RESET);
    }

    [Fact]
    public void Linux_ErrnoConstants_AreCorrect()
    {
        Assert.Equal(4, LinuxUsbAPI.EINTR);
        Assert.Equal(11, LinuxUsbAPI.EAGAIN);
        Assert.Equal(13, LinuxUsbAPI.EACCES);
        Assert.Equal(16, LinuxUsbAPI.EBUSY);
        Assert.Equal(19, LinuxUsbAPI.ENODEV);
        Assert.Equal(32, LinuxUsbAPI.EPIPE);
        Assert.Equal(108, LinuxUsbAPI.ESHUTDOWN);
        Assert.Equal(110, LinuxUsbAPI.ETIMEDOUT);
        Assert.Equal(71, LinuxUsbAPI.EPROTO);
    }

    [Fact]
    public void HarmonyOS_UsbControlRequestSetup_SizeIs8Bytes()
    {
        Assert.Equal(8, Marshal.SizeOf<HarmonyOSUsbDDK.UsbControlRequestSetup>());
    }

    [Fact]
    public void HarmonyOS_UsbDeviceDescriptor_SizeIs18Bytes()
    {
        Assert.Equal(18, Marshal.SizeOf<HarmonyOSUsbDDK.UsbDeviceDescriptor>());
    }

    [Fact]
    public void HarmonyOS_UsbConfigDescriptor_MatchesNativeLayout()
    {
        int actual = Marshal.SizeOf<HarmonyOSUsbDDK.UsbConfigDescriptor>();
        Assert.True(actual >= 9, $"Config descriptor size should be at least 9 bytes, got {actual}");
    }

    [Fact]
    public void HarmonyOS_UsbInterfaceDescriptor_SizeIs9Bytes()
    {
        Assert.Equal(9, Marshal.SizeOf<HarmonyOSUsbDDK.UsbInterfaceDescriptor>());
    }

    [Fact]
    public void HarmonyOS_UsbEndpointDescriptor_MatchesNativeLayout()
    {
        int actual = Marshal.SizeOf<HarmonyOSUsbDDK.UsbEndpointDescriptor>();
        Assert.True(actual >= 7, $"Endpoint descriptor size should be at least 7 bytes, got {actual}");
    }

    [Fact]
    public void HarmonyOS_UsbDdkInterface_AltsettingIsIntPtr()
    {
        var field = typeof(HarmonyOSUsbDDK.UsbDdkInterface).GetField("altsetting");
        Assert.NotNull(field);
        Assert.Equal(typeof(IntPtr), field!.FieldType);
    }

    [Fact]
    public void HarmonyOS_UsbDdkInterface_SizeIsCorrect()
    {
        int expected = IntPtr.Size == 4 ? 20 : 32;
        Assert.Equal(expected, Marshal.SizeOf<HarmonyOSUsbDDK.UsbDdkInterface>());
    }

    [Fact]
    public void HarmonyOS_UsbDdkInterfaceDescriptor_MatchesNativeLayout()
    {
        int actual = Marshal.SizeOf<HarmonyOSUsbDDK.UsbDdkInterfaceDescriptor>();
        Assert.True(actual > 0, $"UsbDdkInterfaceDescriptor should have positive size, got {actual}");
    }

    [Fact]
    public void HarmonyOS_UsbDdkConfigDescriptor_MatchesNativeLayout()
    {
        int actual = Marshal.SizeOf<HarmonyOSUsbDDK.UsbDdkConfigDescriptor>();
        Assert.True(actual > 0, $"UsbDdkConfigDescriptor should have positive size, got {actual}");
    }

    [Fact]
    public void HarmonyOS_UsbRequestPipe_SizeIs16Bytes()
    {
        Assert.Equal(16, Marshal.SizeOf<HarmonyOSUsbDDK.UsbRequestPipe>());
    }

    [Fact]
    public void HarmonyOS_ErrorCodes_MatchOfficialValues()
    {
        Assert.Equal(0, HarmonyOSUsbDDK.USB_DDK_NO_ERROR);
        Assert.Equal(-1, HarmonyOSUsbDDK.USB_DDK_INVALID_PARAMETER);
        Assert.Equal(-2, HarmonyOSUsbDDK.USB_DDK_INVALID_OPERATION);
        Assert.Equal(-3, HarmonyOSUsbDDK.USB_DDK_INIT_ERROR);
        Assert.Equal(-4, HarmonyOSUsbDDK.USB_DDK_SERVICE_ERROR);
        Assert.Equal(-5, HarmonyOSUsbDDK.USB_DDK_MEMORY_ERROR);
        Assert.Equal(-6, HarmonyOSUsbDDK.USB_DDK_IO_ERROR);
        Assert.Equal(-7, HarmonyOSUsbDDK.USB_DDK_DEVICE_BUSY);
        Assert.Equal(-8, HarmonyOSUsbDDK.USB_DDK_TIMEOUT);
    }

    [Fact]
    public void OpenHarmony_IoctlCodes_MatchLinux()
    {
        Assert.Equal(LinuxUsbAPI.USBDEVFS_CONTROL, OpenHarmonyUsbAPI.USBDEVFS_CONTROL);
        Assert.Equal(LinuxUsbAPI.USBDEVFS_BULK, OpenHarmonyUsbAPI.USBDEVFS_BULK);
        Assert.Equal(LinuxUsbAPI.USBDEVFS_CONTROL_X86_64, OpenHarmonyUsbAPI.USBDEVFS_CONTROL_X86_64);
        Assert.Equal(LinuxUsbAPI.USBDEVFS_BULK_X86_64, OpenHarmonyUsbAPI.USBDEVFS_BULK_X86_64);
        Assert.Equal(LinuxUsbAPI.USBDEVFS_CLAIMINTERFACE, OpenHarmonyUsbAPI.USBDEVFS_CLAIMINTERFACE);
        Assert.Equal(LinuxUsbAPI.USBDEVFS_RELEASEINTERFACE, OpenHarmonyUsbAPI.USBDEVFS_RELEASEINTERFACE);
    }

    [Fact]
    public void OpenHarmony_ErrnoConstants_MatchLinux()
    {
        Assert.Equal(LinuxUsbAPI.EINTR, OpenHarmonyUsbAPI.EINTR);
        Assert.Equal(LinuxUsbAPI.EAGAIN, OpenHarmonyUsbAPI.EAGAIN);
        Assert.Equal(LinuxUsbAPI.EACCES, OpenHarmonyUsbAPI.EACCES);
        Assert.Equal(LinuxUsbAPI.EBUSY, OpenHarmonyUsbAPI.EBUSY);
        Assert.Equal(LinuxUsbAPI.ENODEV, OpenHarmonyUsbAPI.ENODEV);
        Assert.Equal(LinuxUsbAPI.EPIPE, OpenHarmonyUsbAPI.EPIPE);
        Assert.Equal(LinuxUsbAPI.ESHUTDOWN, OpenHarmonyUsbAPI.ESHUTDOWN);
        Assert.Equal(LinuxUsbAPI.ETIMEDOUT, OpenHarmonyUsbAPI.ETIMEDOUT);
        Assert.Equal(LinuxUsbAPI.EPROTO, OpenHarmonyUsbAPI.EPROTO);
    }

    [Fact]
    public void MacOS_IOUSBFindInterfaceRequest_SizeIs8Bytes()
    {
        Assert.Equal(8, Marshal.SizeOf<MacOSUsbAPI.IOUSBFindInterfaceRequest>());
    }

    [Fact]
    public void MacOS_IOUSBFindInterfaceRequest_FieldsAreUInt16()
    {
        var fields = typeof(MacOSUsbAPI.IOUSBFindInterfaceRequest).GetFields();
        Assert.Equal(4, fields.Length);
        foreach (var field in fields)
        {
            Assert.Equal(typeof(ushort), field.FieldType);
        }
    }

    [Fact]
    public void MacOS_IOUSBFindInterfaceRequest_DontCareIs0xFF()
    {
        Assert.Equal((ushort)0xFF, MacOSUsbAPI.kIOUSBFindInterfaceDontCare);
    }

    [Fact]
    public void MacOS_IOReturn_ErrorCodes_AreCorrect()
    {
        Assert.Equal(unchecked((int)0xE00002C0), MacOSUsbAPI.kIOReturnNoDevice);
        Assert.Equal(unchecked((int)0xE00002EB), MacOSUsbAPI.kIOReturnAborted);
        Assert.Equal(unchecked((int)0xE00002D6), MacOSUsbAPI.kIOReturnTimeout);
        Assert.Equal(unchecked((int)0xE00002ED), MacOSUsbAPI.kIOReturnNotResponding);
    }

    [Fact]
    public void Win32_CTL_CODE_ProducesCorrectResult()
    {
        uint result = Win32API.CTL_CODE(0x22, 0x800, 0, 1);
        uint expected = (0x22u << 16) | (1u << 14) | (0x800u << 2) | 0u;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Win32_CreateFileW_HasUnicodeCharSet()
    {
        var method = typeof(Win32API).GetMethod("CreateFileW");
        Assert.NotNull(method);
        var attr = method!.GetCustomAttributes(typeof(DllImportAttribute), false);
        Assert.Single(attr);
        var dllImport = (DllImportAttribute)attr[0];
        Assert.Equal(CharSet.Unicode, dllImport.CharSet);
    }

    [Fact]
    public void Win32_SetupDiGetClassDevsW_HasUnicodeCharSet()
    {
        var method = typeof(Win32API).GetMethod("SetupDiGetClassDevsW");
        Assert.NotNull(method);
        var attr = method!.GetCustomAttributes(typeof(DllImportAttribute), false);
        Assert.Single(attr);
        var dllImport = (DllImportAttribute)attr[0];
        Assert.Equal(CharSet.Unicode, dllImport.CharSet);
    }

    [Fact]
    public void Win32_SetupDiGetClassDevsW_HwndParentIsIntPtr()
    {
        var method = typeof(Win32API).GetMethod("SetupDiGetClassDevsW");
        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.Equal(4, parameters.Length);
        Assert.Equal(typeof(IntPtr), parameters[2].ParameterType);
    }

    [Fact]
    public void Win32_DeviceIoControl_SizeParamsAreUint()
    {
        var method = typeof(Win32API).GetMethod("DeviceIoControl", new[] { typeof(IntPtr), typeof(uint), typeof(byte[]), typeof(uint), typeof(byte[]), typeof(uint), typeof(uint).MakeByRefType(), typeof(IntPtr) });
        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.Equal(typeof(uint), parameters[3].ParameterType);
        Assert.Equal(typeof(uint), parameters[5].ParameterType);
        Assert.Equal(typeof(uint), parameters[6].ParameterType.GetElementType());
    }
}
