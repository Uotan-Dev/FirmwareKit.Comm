using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static FirmwareKit.Comm.Usb.Backend.Linux.LinuxUsbAPI;

namespace FirmwareKit.Comm.Usb.Backend.Linux;

internal class LinuxUsbDevice : UsbDevice
{
    private const int PlatformDefaultTimeoutMs = UsbTransferPolicies.DefaultTimeoutMs;

    private readonly LinuxUsbFd _fd = new LinuxUsbFd();

    private int Fd => (int)_fd.DangerousGetHandle();

    public byte ep_in { get; set; }
    public byte ep_out { get; set; }
    public int InterfaceId { get; set; }
    public byte iSerialNumber { get; set; }

    public override int DefaultTimeoutMs => PlatformDefaultTimeoutMs;

    public override int CreateHandle()
    {
        _fd.SetFd(open(DevicePath, O_RDWR | O_CLOEXEC));
        if (_fd.IsInvalid) return -1;
        int ifc = InterfaceId;
        int n = ioctl(Fd, (UIntPtr)USBDEVFS_CLAIMINTERFACE, ref ifc);
        if (n != 0)
        {
            ioctl(Fd, (UIntPtr)USBDEVFS_DISCONNECT, ref ifc);
            n = ioctl(Fd, (UIntPtr)USBDEVFS_CLAIMINTERFACE, ref ifc);
        }
        if (n != 0)
        {
            _fd.Dispose();
            return n;
        }
        GetSerialNumber();
        return 0;
    }

    public override void Reset()
    {
        if (!_fd.IsInvalid)
        {
            ioctl(Fd, (UIntPtr)USBDEVFS_RESET, IntPtr.Zero);
        }
    }

    public override int ControlTransfer(FirmwareKit.Comm.Usb.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        if (_fd.IsInvalid)
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

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
        usbdevfs_ctrltransfer ctrl = new usbdevfs_ctrltransfer
        {
            bRequestType = setupPacket.RequestType,
            bRequest = setupPacket.Request,
            wValue = setupPacket.Value,
            wIndex = setupPacket.Index,
            wLength = (ushort)length,
            timeout = (uint)effectiveTimeoutMs
        };

        bool isInDirection = (setupPacket.RequestType & 0x80) != 0;

        if (length > 0)
        {
            if (buffer != null && offset == 0 && length == buffer.Length)
            {
                GCHandle pinnedHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    ctrl.data = pinnedHandle.AddrOfPinnedObject();
                    int result = ioctl(Fd, (UIntPtr)(IntPtr.Size == 8 ? USBDEVFS_CONTROL_X86_64 : USBDEVFS_CONTROL_X86), ref ctrl);
                    if (result < 0)
                    {
                        throw new IOException($"USB control transfer failed with error: {Marshal.GetLastWin32Error()}");
                    }

                    return result;
                }
                finally
                {
                    pinnedHandle.Free();
                }
            }

            byte[] transferBuffer = new byte[length];
            if (!isInDirection && buffer != null)
            {
                Buffer.BlockCopy(buffer, offset, transferBuffer, 0, length);
            }

            GCHandle transferHandle = GCHandle.Alloc(transferBuffer, GCHandleType.Pinned);
            try
            {
                ctrl.data = transferHandle.AddrOfPinnedObject();
                int result = ioctl(Fd, (UIntPtr)(IntPtr.Size == 8 ? USBDEVFS_CONTROL_X86_64 : USBDEVFS_CONTROL_X86), ref ctrl);
                if (result < 0)
                {
                    throw new IOException($"USB control transfer failed with error: {Marshal.GetLastWin32Error()}");
                }

                if (isInDirection && buffer != null)
                {
                    Buffer.BlockCopy(transferBuffer, 0, buffer, offset, Math.Min(result, length));
                }

                return result;
            }
            finally
            {
                transferHandle.Free();
            }
        }

        ctrl.data = IntPtr.Zero;
        int zeroResult = ioctl(Fd, (UIntPtr)(IntPtr.Size == 8 ? USBDEVFS_CONTROL_X86_64 : USBDEVFS_CONTROL_X86), ref ctrl);
        if (zeroResult < 0)
        {
            throw new IOException($"USB control transfer failed with error: {Marshal.GetLastWin32Error()}");
        }

        return zeroResult;
    }

    public override void Dispose()
    {
        if (!_fd.IsInvalid)
        {
            int ifc = InterfaceId;
            ioctl(Fd, (UIntPtr)USBDEVFS_RELEASEINTERFACE, ref ifc);
            _fd.Dispose();
        }
    }

    public override int GetSerialNumber()
    {
        if (iSerialNumber == 0) return -1;

        usbdevfs_ctrltransfer ctrl = new usbdevfs_ctrltransfer();
        byte[] descriptor = new byte[256];
        GCHandle handle = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
        try
        {
            uint ctrlCode = (IntPtr.Size == 8) ? USBDEVFS_CONTROL_X86_64 : USBDEVFS_CONTROL_X86;
            UIntPtr ctrlCodePtr = (UIntPtr)ctrlCode;

            ctrl.bRequestType = 0x80;
            ctrl.bRequest = 0x06;
            ctrl.wValue = (ushort)(0x03 << 8);
            ctrl.wIndex = 0;
            ctrl.wLength = (ushort)descriptor.Length;
            ctrl.data = handle.AddrOfPinnedObject();
            ctrl.timeout = 1000;

            int n = ioctl(Fd, ctrlCodePtr, ref ctrl);
            int languageCount = 0;
            ushort[] languages = new ushort[128];
            if (n > 2)
            {
                languageCount = (n - 2) / 2;
                for (int i = 0; i < languageCount; i++)
                {
                    languages[i] = (ushort)(descriptor[2 + i * 2] | (descriptor[3 + i * 2] << 8));
                }
            }
            else
            {
                languages[0] = 0x0409;
                languageCount = 1;
            }

            for (int i = 0; i < languageCount; i++)
            {
                ctrl.bRequestType = 0x80;
                ctrl.bRequest = 0x06;
                ctrl.wValue = (ushort)((0x03 << 8) | iSerialNumber);
                ctrl.wIndex = languages[i];
                ctrl.wLength = (ushort)descriptor.Length;
                ctrl.data = handle.AddrOfPinnedObject();
                ctrl.timeout = 1000;

                n = ioctl(Fd, ctrlCodePtr, ref ctrl);
                if (n > 2)
                {
                    SerialNumber = UsbStringDescriptor.Decode(descriptor, n);
                    return 0;
                }
            }
            return -1;
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

    protected override string BackendName => "linux-usbfs";

    protected override bool IsOpen => !_fd.IsInvalid;

    protected override int MaxChunkSize => UsbTransferPolicies.LinuxUsbFsMaxBulkSize;

    protected override UsbChunkResult ReadChunk(IntPtr buffer, int length, int timeoutMs)
    {
        var bulk = new usbdevfs_bulktransfer
        {
            ep = ep_in,
            len = (uint)length,
            timeout = (uint)timeoutMs,
            data = buffer
        };

        uint bulkCode = (IntPtr.Size == 8) ? USBDEVFS_BULK_X86_64 : USBDEVFS_BULK_X86;
        UIntPtr bulkCodePtr = (UIntPtr)bulkCode;
        int n = -1;
        int retry = 0;
        int retryCount = 0;
        do
        {
            n = ioctl(Fd, bulkCodePtr, ref bulk);
            if (n < 0)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == EINTR || err == EAGAIN) continue;
                if (err == ETIMEDOUT) return UsbChunkResult.Timeout(err);
                if (err == ENODEV || err == ESHUTDOWN || err == EPROTO) return UsbChunkResult.Fatal(err);
                if (++retry > UsbTransferPolicies.LinuxMaxRetries) return UsbChunkResult.Error(err);
                retryCount++;
                Thread.Sleep(500);
            }
        } while (n < 0);
        return new UsbChunkResult(UsbChunkStatus.Success, n, 0, retryCount);
    }

    protected override UsbChunkResult WriteChunk(IntPtr buffer, int length, int timeoutMs)
    {
        var bulk = new usbdevfs_bulktransfer
        {
            ep = ep_out,
            len = (uint)length,
            timeout = (uint)timeoutMs,
            data = buffer
        };

        uint bulkCode = (IntPtr.Size == 8) ? USBDEVFS_BULK_X86_64 : USBDEVFS_BULK_X86;
        UIntPtr bulkCodePtr = (UIntPtr)bulkCode;
        int n = -1;
        int retry = 0;
        int retryCount = 0;
        do
        {
            n = ioctl(Fd, bulkCodePtr, ref bulk);
            if (n < 0)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == EINTR || err == EAGAIN) continue;
                if (err == ETIMEDOUT) return UsbChunkResult.Timeout(err);
                if (err == ENODEV || err == ESHUTDOWN || err == EPROTO) return UsbChunkResult.Fatal(err);
                if (++retry > UsbTransferPolicies.LinuxMaxRetries) return UsbChunkResult.Timeout(err);
                retryCount++;
                Thread.Sleep(500);
            }
        } while (n < 0);
        return new UsbChunkResult(UsbChunkStatus.Success, n, 0, retryCount);
    }

    public override long Write(byte[] data, int length)
    {
        return Write(data, length, PlatformDefaultTimeoutMs);
    }


}



