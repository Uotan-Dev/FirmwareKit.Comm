using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Diagnostics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static FirmwareKit.Comm.Backend.MacOS.MacHostUsbAPI;

namespace FirmwareKit.Comm.Backend.MacOS;

/// <summary>
/// USB device backed by the IOUSBHost (IOUSBLib) user-space API (macOS 10.15+).
/// Replaces the legacy IOKit IOUSBDeviceInterface197 / IOUSBInterfaceInterface197 COM-vtable device.
///
/// Reference-counting conventions (IOUSBLib, consistent with IOKit):
///  - Devices taken from the IOUSBLibCopyDevices array are borrowed; CreateHandle retains the
///    one it opens (CFRetain) and Dispose releases it.
///  - Interfaces from IOUSBHostInterfaceIteratorNext are owned (+1); Dispose releases them.
///  - Pipes from IOUSBHostInterfaceCopyPipe are owned ("Copy" semantics); Dispose releases them.
/// Verify against IOUSBLib.h on macOS when first integrating.
/// </summary>
internal class MacHostUsbDevice : UsbDevice
{
    /// <summary>
    /// Gets or sets the registry entry ID used to reopen the device by identity.
    /// <para>获取或设置用于按标识重新打开设备的注册表项 ID。</para>
    /// </summary>
    public ulong RegistryEntryId { get; set; }

    private IntPtr devicePtr;
    private IntPtr interfacePtr;
    private IntPtr pipeIn;
    private IntPtr pipeOut;

    private const int PlatformDefaultTimeoutMs = UsbTransferPolicies.DefaultTimeoutMs;

    internal byte bulkIn;
    internal byte bulkOut;

    public override int CreateHandle()
    {
        if (devicePtr != IntPtr.Zero) return 0;

        IntPtr cfDevices = IntPtr.Zero;
        if (IOUSBLibCopyDevices(IntPtr.Zero, out cfDevices) != kIOReturnSuccess || cfDevices == IntPtr.Zero) return -1;

        try
        {
            long count = CFArrayGetCount(cfDevices);
            for (long i = 0; i < count; i++)
            {
                IntPtr candidate = CFArrayGetValueAtIndex(cfDevices, i);
                if (candidate == IntPtr.Zero) continue;

                ulong rid = 0;
                if (IOUSBHostDeviceGetRegistryEntryID(candidate, out rid) != kIOReturnSuccess || rid != RegistryEntryId) continue;

                uint deviceParameter = 0;
                if (IOUSBHostDeviceOpen(candidate, 0, out deviceParameter) != kIOReturnSuccess) continue; // busy or claimed

                devicePtr = CFRetain(candidate); // keep past the array release
                break;
            }
        }
        finally
        {
            CFRelease(cfDevices);
        }

        if (devicePtr == IntPtr.Zero) return -1;

        // Align with the libusb/SharpFastboot backends: ensure Configuration 1 is
        // selected before opening interfaces. Some devices power up with another
        // configuration, leaving the target interface unavailable. Failures are
        // ignored because a device may legitimately have a single, differently
        // numbered configuration (libusb backend ignores SetConfiguration too).
        // Must happen before the interface iterator is created - switching the
        // configuration resets every interface.
        try
        {
            byte[] cfgBuf = new byte[1];
            if (ControlTransferRaw(0x80, 0x08, 0x0000, 0x0000, cfgBuf, 1, 1000) > 0 && cfgBuf[0] != 1)
            {
                _ = ControlTransferRaw(0x00, 0x09, 0x0001, 0x0000, Array.Empty<byte>(), 0, 1000);
            }
        }
        catch
        {
            // Configuration probing/selection is best-effort; continue regardless.
        }

        IOUSBFindInterfaceRequest findRequest = new IOUSBFindInterfaceRequest
        {
            bInterfaceClass = kIOUSBFindInterfaceDontCare,
            bInterfaceSubClass = kIOUSBFindInterfaceDontCare,
            bInterfaceProtocol = kIOUSBFindInterfaceDontCare,
            bAlternateSetting = kIOUSBFindInterfaceDontCare
        };

        IntPtr iterator = IntPtr.Zero;
        try
        {
            if (IOUSBHostDeviceCreateInterfaceIterator(devicePtr, ref findRequest, out iterator) != kIOReturnSuccess || iterator == IntPtr.Zero)
            {
                return -1;
            }

            IntPtr ifc;
            while ((ifc = IOUSBHostInterfaceIteratorNext(iterator)) != IntPtr.Zero)
            {
                if (IOUSBHostInterfaceOpen(ifc, 0) != kIOReturnSuccess)
                {
                    CFRelease(ifc);
                    continue;
                }

                IntPtr inPipe = IntPtr.Zero;
                IntPtr outPipe = IntPtr.Zero;
                _ = IOUSBHostInterfaceCopyPipe(ifc, kIOUSBHostPortTypeBulk, bulkIn, out inPipe);
                _ = IOUSBHostInterfaceCopyPipe(ifc, kIOUSBHostPortTypeBulk, bulkOut, out outPipe);

                if (inPipe == IntPtr.Zero || outPipe == IntPtr.Zero)
                {
                    if (inPipe != IntPtr.Zero) CFRelease(inPipe);
                    if (outPipe != IntPtr.Zero) CFRelease(outPipe);
                    _ = IOUSBHostInterfaceClose(ifc);
                    CFRelease(ifc);
                    continue;
                }

                interfacePtr = ifc;
                pipeIn = inPipe;
                pipeOut = outPipe;
                _ = IOUSBHostPipeClearStall(pipeIn);
                _ = IOUSBHostPipeClearStall(pipeOut);
                return 0;
            }
        }
        finally
        {
            if (iterator != IntPtr.Zero) CFRelease(iterator);
        }

        return -1;
    }

    public override void Reset()
    {
        if (pipeIn != IntPtr.Zero)
        {
            _ = IOUSBHostPipeAbort(pipeIn);
            _ = IOUSBHostPipeClearStall(pipeIn);
        }
        if (pipeOut != IntPtr.Zero)
        {
            _ = IOUSBHostPipeAbort(pipeOut);
            _ = IOUSBHostPipeClearStall(pipeOut);
        }
    }

    public override int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        if (devicePtr == IntPtr.Zero)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }

        if (buffer == null)
        {
            if (length != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
        }
        else
        {
            ValidateBufferRange(buffer, offset, length);
        }

        var request = new IOUSBDeviceRequest
        {
            bmRequestType = setupPacket.RequestType,
            bRequest = setupPacket.Request,
            wValue = setupPacket.Value,
            wIndex = setupPacket.Index,
            wLength = (ushort)length
        };

        byte[]? transferBuffer = null;
        bool isInDirection = (setupPacket.RequestType & 0x80) != 0;

        if (length > 0)
        {
            if (buffer != null && offset == 0 && length == buffer.Length)
            {
                transferBuffer = buffer;
            }
            else
            {
                transferBuffer = new byte[length];
                if (!isInDirection && buffer != null)
                {
                    Buffer.BlockCopy(buffer, offset, transferBuffer, 0, length);
                }
            }
        }

        GCHandle handle = default;
        try
        {
            if (transferBuffer != null)
            {
                handle = GCHandle.Alloc(transferBuffer, GCHandleType.Pinned);
                request.pData = handle.AddrOfPinnedObject();
            }

            int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
            int result = IOUSBHostDeviceDeviceRequest(devicePtr, ref request, (uint)effectiveTimeoutMs);
            if (result != kIOReturnSuccess)
            {
                throw new IOException($"USB control transfer failed with error: 0x{result:X}");
            }

            if (isInDirection && buffer != null && transferBuffer != null && transferBuffer != buffer)
            {
                int bytesCopied = (int)request.wLenDone;
                Buffer.BlockCopy(transferBuffer, 0, buffer, offset, Math.Min(bytesCopied, length));
                return bytesCopied;
            }

            return (int)request.wLenDone;
        }
        finally
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }

    public override void Dispose()
    {
        if (pipeIn != IntPtr.Zero)
        {
            CFRelease(pipeIn);
            pipeIn = IntPtr.Zero;
        }
        if (pipeOut != IntPtr.Zero)
        {
            CFRelease(pipeOut);
            pipeOut = IntPtr.Zero;
        }
        if (interfacePtr != IntPtr.Zero)
        {
            _ = IOUSBHostInterfaceClose(interfacePtr);
            CFRelease(interfacePtr);
            interfacePtr = IntPtr.Zero;
        }
        if (devicePtr != IntPtr.Zero)
        {
            _ = IOUSBHostDeviceClose(devicePtr);
            CFRelease(devicePtr);
            devicePtr = IntPtr.Zero;
        }
    }

    public override int GetSerialNumber()
    {
        if (devicePtr == IntPtr.Zero) return -1;

        // Read the device descriptor via a control request; iSerialNumber lives at offset 16,
        // bcdUSB at offset 2 (used to infer the USB speed like the Linux fallback).
        byte[] dd = new byte[18];
        int done = ControlTransferRaw(0x80, 0x06, 0x0100, 0x0000, dd, dd.Length, 1000);
        if (done < 18) return -1;

        Speed = UsbSpeedInference.FromBcdUsb((ushort)(dd[2] | (dd[3] << 8)));

        byte serialIndex = dd[16];
        if (serialIndex == 0) return -1;

        // GET_DESCRIPTOR(STRING), language 0x0409 (en-US).
        byte[] buf = new byte[256];
        done = ControlTransferRaw(0x80, 0x06, (ushort)((0x03 << 8) | serialIndex), 0x0409, buf, buf.Length, 1000);
        if (done <= 2) return -1;

        SerialNumber = UsbStringDescriptor.Decode(buf, done);
        return 0;
    }

    private int ControlTransferRaw(byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, byte[] buffer, int length, int timeoutMs)
    {
        var request = new IOUSBDeviceRequest
        {
            bmRequestType = bmRequestType,
            bRequest = bRequest,
            wValue = wValue,
            wIndex = wIndex,
            wLength = (ushort)length
        };

        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            request.pData = handle.AddrOfPinnedObject();
            int kr = IOUSBHostDeviceDeviceRequest(devicePtr, ref request, (uint)timeoutMs);
            if (kr != kIOReturnSuccess) return -1;
            return (int)request.wLenDone;
        }
        finally
        {
            handle.Free();
        }
    }

    public override byte[] Read(int length)
    {
        return Read(length, PlatformDefaultTimeoutMs);
    }

    public override byte[] Read(int length, int timeoutMs)
    {
        if (length <= 0) return Array.Empty<byte>();

        byte[] buffer = new byte[length];
        int count = ReadInto(buffer, 0, length, timeoutMs);
        if (count == length) return buffer;
        if (count == 0) return Array.Empty<byte>();

        byte[] result = new byte[count];
        Buffer.BlockCopy(buffer, 0, result, 0, count);
        return result;
    }

    public override int ReadInto(byte[] buffer, int offset, int length)
    {
        return ReadInto(buffer, offset, length, PlatformDefaultTimeoutMs);
    }

    protected override string BackendName => "macos-iousbhost";

    protected override bool IsOpen => interfacePtr != IntPtr.Zero && pipeIn != IntPtr.Zero && pipeOut != IntPtr.Zero;

    protected override bool IsDisconnectionError(int nativeError)
        => nativeError == kIOReturnNoDevice || nativeError == kIOReturnNotResponding || nativeError == kIOReturnAborted;

    protected override UsbChunkResult ReadChunk(IntPtr buffer, int length, int timeoutMs)
    {
        uint transferred = 0;
        int kr = IOUSBHostPipeBulkTransfer(pipeIn, buffer, (uint)length, out transferred, (uint)timeoutMs);
        if (kr != kIOReturnSuccess)
        {
            if (kr == kIOReturnNoDevice || kr == kIOReturnNotResponding || kr == kIOReturnAborted)
            {
                return UsbChunkResult.Fatal(kr);
            }
            if (kr == kIOReturnTimeout)
            {
                return UsbChunkResult.Timeout(kr);
            }
            return UsbChunkResult.Error(kr);
        }
        return UsbChunkResult.Success((int)transferred);
    }

    protected override UsbChunkResult WriteChunk(IntPtr buffer, int length, int timeoutMs)
    {
        uint transferred = 0;
        int kr = IOUSBHostPipeWriteBulkData(pipeOut, buffer, (uint)length, out transferred, (uint)timeoutMs);
        if (kr != kIOReturnSuccess)
        {
            if (kr == kIOReturnNoDevice || kr == kIOReturnNotResponding || kr == kIOReturnAborted)
            {
                return UsbChunkResult.Fatal(kr);
            }
            if (kr == kIOReturnTimeout)
            {
                return UsbChunkResult.Timeout(kr);
            }
            return UsbChunkResult.Error(kr);
        }
        return UsbChunkResult.Success((int)transferred);
    }

    public override long Write(byte[] data, int length)
    {
        return Write(data, length, PlatformDefaultTimeoutMs);
    }

    public override void WriteZlp(int timeoutMs)
    {
        if (pipeOut == IntPtr.Zero)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }

        uint transferred = 0;
        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
        int kr = IOUSBHostPipeWriteBulkData(pipeOut, IntPtr.Zero, 0, out transferred, (uint)effectiveTimeoutMs);
        if (kr != kIOReturnSuccess)
        {
            if (kr == kIOReturnNoDevice || kr == kIOReturnNotResponding || kr == kIOReturnAborted)
            {
                throw new UsbDeviceDisconnectedException($"USB device disconnected during zero-length write (error: 0x{kr:X}).", kr);
            }
            throw new IOException($"USB zero-length write failed with error: 0x{kr:X}");
        }
    }
}
