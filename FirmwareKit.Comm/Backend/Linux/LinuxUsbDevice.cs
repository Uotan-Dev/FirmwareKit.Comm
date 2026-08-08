using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Diagnostics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static FirmwareKit.Comm.Backend.Linux.LinuxUsbAPI;

namespace FirmwareKit.Comm.Backend.Linux;

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
        int fd = open(DevicePath, O_RDWR | O_CLOEXEC);
        if (fd < 0)
        {
            int err = Marshal.GetLastWin32Error();
            if (err == EACCES)
            {
                LinuxUsbFinder.ReportPermissionDenied(DevicePath);
            }
            _fd.SetFd(fd);
            return -1;
        }
        _fd.SetFd(fd);
        int ifc = InterfaceId;
        int n = ioctl(Fd, (UIntPtr)USBDEVFS_CLAIMINTERFACE, ref ifc);
        if (n != 0)
        {
            ioctl(Fd, (UIntPtr)USBDEVFS_DISCONNECT, ref ifc);
            n = ioctl(Fd, (UIntPtr)USBDEVFS_CLAIMINTERFACE, ref ifc);
        }
        if (n != 0)
        {
            int err = Marshal.GetLastWin32Error();
            if (err == EBUSY)
            {
                LinuxUsbFinder.ReportBusy(DevicePath);
            }
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

    public override int ControlTransfer(FirmwareKit.Comm.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
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

    protected override bool IsDisconnectionError(int nativeError)
        => nativeError == ENODEV || nativeError == ESHUTDOWN || nativeError == EPROTO;

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
                if (++retry > UsbTransferPolicies.LinuxMaxRetries) return UsbChunkResult.Error(err);
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

    public override Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (_fd.IsInvalid)
        {
            return Task.FromException<int>(new UsbDeviceHandleClosedException("Device handle is closed."));
        }
        if (length <= 0) return Task.FromResult(0);
        ValidateBufferRange(buffer, offset, length);
        return ReadIntoUrbAsync(buffer, offset, length, timeoutMs, cancellationToken);
    }

    private async Task<int> ReadIntoUrbAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken)
    {
        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
        int total = 0;
        int remaining = length;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int lenToRead = Math.Min(remaining, MaxChunkSize);
            uint transferred = await SubmitUrbAsync(ep_in, buffer, offset + total, lenToRead, effectiveTimeoutMs, cancellationToken).ConfigureAwait(false);
            total += (int)transferred;
            remaining -= (int)transferred;
            if (transferred < lenToRead) break; // short packet or timeout
        }
        return total;
    }

    public override Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (_fd.IsInvalid)
        {
            return Task.FromException<long>(new UsbDeviceHandleClosedException("Device handle is closed."));
        }
        ValidateWriteData(data, length);
        if (length == 0) return Task.FromResult(0L);
        return WriteUrbAsync(data, length, timeoutMs, cancellationToken);
    }

    private async Task<long> WriteUrbAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken)
    {
        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
        int total = 0;
        int remaining = length;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int lenToSend = Math.Min(remaining, MaxChunkSize);
            uint transferred = await SubmitUrbAsync(ep_out, data, total, lenToSend, effectiveTimeoutMs, cancellationToken).ConfigureAwait(false);
            total += (int)transferred;
            remaining -= (int)transferred;
            if (transferred < lenToSend) break;
        }
        return total;
    }

    /// <summary>
    /// Submits a bulk URB and waits for completion via poll(). The kernel performs the
    /// transfer asynchronously; a thread-pool thread waits on poll() without blocking the caller.
    /// <para>提交批量 URB 并通过 poll() 等待完成。内核异步执行传输；线程池线程在 poll() 上等待，不阻塞调用方。</para>
    /// </summary>
    private async Task<uint> SubmitUrbAsync(byte endpoint, byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken)
    {
        GCHandle bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        var urb = new usbdevfs_urb
        {
            type = USBDEVFS_URB_TYPE_BULK,
            endpoint = endpoint,
            flags = 0, // keep short reads completing normally (do not set SHORT_NOT_OK)
            buffer = new IntPtr(bufferHandle.AddrOfPinnedObject().ToInt64() + offset),
            buffer_length = length,
            usercontext = IntPtr.Zero
        };

        GCHandle urbHandle = GCHandle.Alloc(urb, GCHandleType.Pinned);
        try
        {
            uint submitCode = (IntPtr.Size == 8) ? USBDEVFS_SUBMITURB_X86_64 : USBDEVFS_SUBMITURB_X86;
            if (ioctl(Fd, (UIntPtr)submitCode, urbHandle.AddrOfPinnedObject()) < 0)
            {
                throw new IOException($"USB async submit failed: {Marshal.GetLastWin32Error()}");
            }

            int pollResult;
            try
            {
                pollResult = await Task.Run(() =>
                {
                    var pfd = new PollFd { fd = Fd, events = (short)(POLLIN | POLLOUT) };
                    return poll(ref pfd, 1, timeoutMs);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancelled while the URB is still queued: discard and drain it so the pinned
                // URB can be freed and the kernel does not keep a stale reference.
                DiscardPendingUrb(urbHandle);
                throw;
            }

            if (pollResult == 0)
            {
                // Timeout: discard the pending URB and drain it so the pinned URB can be freed.
                DiscardPendingUrb(urbHandle);
                return 0;
            }
            if (pollResult < 0)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == EINTR) return 0;
                throw new IOException($"USB async poll failed: {err}");
            }

            // Reap the completed URB and read its status/actual_length.
            IntPtr reaped = IntPtr.Zero;
            uint reapCode2 = (IntPtr.Size == 8) ? USBDEVFS_REAPURBNDELAY_X86_64 : USBDEVFS_REAPURBNDELAY_X86;
            if (ioctl(Fd, (UIntPtr)reapCode2, ref reaped) < 0)
            {
                throw new IOException($"USB async reap failed: {Marshal.GetLastWin32Error()}");
            }

            var completed = Marshal.PtrToStructure<usbdevfs_urb>(reaped);
            if (completed.status != 0)
            {
                int err = -completed.status; // kernel reports negative errno in status
                if (err == ETIMEDOUT || err == EPIPE) return 0;
                if (err == ENODEV || err == ESHUTDOWN || err == EPROTO)
                {
                    throw new UsbDeviceDisconnectedException($"USB async transfer failed: device disconnected (error: {err}).", err);
                }
                return 0;
            }
            return (uint)Math.Max(0, completed.actual_length);
        }
        finally
        {
            urbHandle.Free();
            bufferHandle.Free();
        }
    }

    /// <summary>
    /// Discards a pending (not yet completed) URB and drains it from the kernel queue.
    /// <para>丢弃一个尚未完成的 URB 并将其从内核队列中取出。</para>
    /// Used on timeout and on cancellation so the pinned URB can be freed without the
    /// kernel holding a stale reference.
    /// <para>在超时与取消时使用，以便在不留内核悬空引用的情况下释放固定的 URB。</para>
    /// </summary>
    private void DiscardPendingUrb(GCHandle urbHandle)
    {
        IntPtr drain = IntPtr.Zero;
        uint discardCode = (IntPtr.Size == 8) ? USBDEVFS_DISCARDURB_X86_64 : USBDEVFS_DISCARDURB_X86;
        uint reapCode = (IntPtr.Size == 8) ? USBDEVFS_REAPURBNDELAY_X86_64 : USBDEVFS_REAPURBNDELAY_X86;
        _ = ioctl(Fd, (UIntPtr)discardCode, urbHandle.AddrOfPinnedObject());
        _ = ioctl(Fd, (UIntPtr)reapCode, ref drain);
    }
}


