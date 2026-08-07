using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Usb.Backend.Windows.Win32API;
using static FirmwareKit.Comm.Usb.Backend.Windows.WinUSBAPI;

namespace FirmwareKit.Comm.Usb.Backend.Windows;

internal class WinUSBDevice : UsbDevice
{
    private const int WinUsbDefaultTimeoutMs = UsbTransferPolicies.WinUsbDefaultTimeoutMs;
    private const int ERROR_SEM_TIMEOUT = 121;
    public override int DefaultTimeoutMs => WinUsbDefaultTimeoutMs;

    private byte InterfaceNum;
    private byte ReadBulkID, WriteBulkID;
    private byte ReadBulkIndex, WriteBulkIndex;
    private SafeWinUsbHandle WinUSBHandle = new SafeWinUsbHandle(IntPtr.Zero);
    private SafeFileHandle FileHandle = new SafeFileHandle(new IntPtr(-1), ownsHandle: true);
    private Win32API.USBDeviceDescriptor USBDeviceDescriptor;
    private Win32API.USBDeviceConfigDescriptor USBDeviceConfigDescriptor;
    private Win32API.USBDeviceInterfaceDescriptor USBDeviceInterfaceDescriptor;

    public override int CreateHandle()
    {
        IntPtr hUsb = SimpleCreateHandle(DevicePath, true);
        uint bytesTransferred;
        if (hUsb == new IntPtr(-1))
            return Marshal.GetLastWin32Error();
        FileHandle = new SafeFileHandle(hUsb, ownsHandle: true);
        if (!WinUsb_Initialize(hUsb, out IntPtr winUsbHandle))
            return Marshal.GetLastWin32Error();
        WinUSBHandle = new SafeWinUsbHandle(winUsbHandle);
        if (!WinUsb_GetCurrentAlternateSetting(WinUSBHandle.DangerousGetHandle(), out InterfaceNum))
            return Marshal.GetLastWin32Error();
        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<USBDeviceDescriptor>());
        if (!WinUsb_GetDescriptor(WinUSBHandle.DangerousGetHandle(), USB_DEVICE_DESCRIPTOR_TYPE, 0, 0, ptr, (uint)Marshal.SizeOf<USBDeviceDescriptor>(), out bytesTransferred))
        {
            Marshal.FreeHGlobal(ptr);
            return Marshal.GetLastWin32Error();
        }
        USBDeviceDescriptor = Marshal.PtrToStructure<USBDeviceDescriptor>(ptr);
        VendorId = USBDeviceDescriptor.idVendor;
        ProductId = USBDeviceDescriptor.idProduct;
        Marshal.FreeHGlobal(ptr);
        ptr = Marshal.AllocHGlobal(Marshal.SizeOf<USBDeviceConfigDescriptor>());
        if (!WinUsb_GetDescriptor(WinUSBHandle.DangerousGetHandle(), USB_CONFIGURATION_DESCRIPTOR_TYPE, 0, 0, ptr, (uint)Marshal.SizeOf<USBDeviceConfigDescriptor>(), out bytesTransferred))
        {
            Marshal.FreeHGlobal(ptr);
            return Marshal.GetLastWin32Error();
        }
        USBDeviceConfigDescriptor = Marshal.PtrToStructure<USBDeviceConfigDescriptor>(ptr);
        Marshal.FreeHGlobal(ptr);
        if (!WinUsb_QueryInterfaceSettings(WinUSBHandle.DangerousGetHandle(), InterfaceNum, out USBDeviceInterfaceDescriptor))
            return Marshal.GetLastWin32Error();

        InterfaceClass = USBDeviceInterfaceDescriptor.bInterfaceClass;
        InterfaceSubClass = USBDeviceInterfaceDescriptor.bInterfaceSubClass;
        InterfaceProtocol = USBDeviceInterfaceDescriptor.bInterfaceProtocol;
        InterfaceMetadataObserved = true;

        for (byte endpoint = 0; endpoint < USBDeviceInterfaceDescriptor.bNumEndpoints; endpoint++)
        {
            WinUSBPipeInfo pipeInfo;
            if (!WinUsb_QueryPipe(WinUSBHandle.DangerousGetHandle(), InterfaceNum, endpoint, out pipeInfo))
                return Marshal.GetLastWin32Error();
            if (pipeInfo.PipeType == WinUSBPipeType.UsbdPipeTypeBulk)
            {
                if ((pipeInfo.PipeID & USB_ENDPOINT_DIRECTION_MASK) != 0)
                {
                    if (ReadBulkID == 0)
                    {
                        ReadBulkID = pipeInfo.PipeID;
                        ReadBulkIndex = endpoint;
                    }
                }
                else
                {
                    if (WriteBulkID == 0)
                    {
                        WriteBulkID = pipeInfo.PipeID;
                        WriteBulkIndex = endpoint;
                    }
                }
            }
        }

        if (ReadBulkID == 0 || WriteBulkID == 0)
        {
            return -1;
        }

        GetSerialNumber();

        byte bTrue = 1;
        byte bFalse = 0;
        uint timeout = 60000; // Increased to 60s for large flash operations

        // Policy configuration
        WinUsb_SetPipePolicy(WinUSBHandle.DangerousGetHandle(), ReadBulkID, AUTO_CLEAR_STALL, 1, ref bTrue);
        WinUsb_SetPipePolicy(WinUSBHandle.DangerousGetHandle(), WriteBulkID, AUTO_CLEAR_STALL, 1, ref bTrue);
        WinUsb_SetPipePolicy(WinUSBHandle.DangerousGetHandle(), ReadBulkID, PIPE_TRANSFER_TIMEOUT, 4, ref timeout);
        WinUsb_SetPipePolicy(WinUSBHandle.DangerousGetHandle(), WriteBulkID, PIPE_TRANSFER_TIMEOUT, 4, ref timeout);

        // WinUSB RAW_IO can significantly improve stability for large transfers.
        // It requires that the transfer size is a multiple of the packet size (typically 512).
        WinUsb_SetPipePolicy(WinUSBHandle.DangerousGetHandle(), ReadBulkID, RAW_IO, 1, ref bFalse);
        WinUsb_SetPipePolicy(WinUSBHandle.DangerousGetHandle(), WriteBulkID, RAW_IO, 1, ref bFalse);

        // Align with AOSP host behavior: avoid forcing ZLP from the host side.
        WinUsb_SetPipePolicy(WinUSBHandle.DangerousGetHandle(), WriteBulkID, SHORT_PACKET_TERMINATE, 1, ref bFalse);

        return 0;
    }

    public IntPtr Handle => !WinUSBHandle.IsInvalid ? WinUSBHandle.DangerousGetHandle() : FileHandle.DangerousGetHandle();

    private void SetPipeTimeout(int timeoutMs)
    {
        if (WinUSBHandle.IsInvalid || timeoutMs <= 0)
        {
            return;
        }

        uint timeout = (uint)timeoutMs;
        WinUsb_SetPipePolicy(WinUSBHandle.DangerousGetHandle(), ReadBulkID, PIPE_TRANSFER_TIMEOUT, 4, ref timeout);
        WinUsb_SetPipePolicy(WinUSBHandle.DangerousGetHandle(), WriteBulkID, PIPE_TRANSFER_TIMEOUT, 4, ref timeout);
    }

    public override void Reset()
    {
        if (!WinUSBHandle.IsInvalid)
        {
            WinUsb_ResetPipe(WinUSBHandle.DangerousGetHandle(), ReadBulkID);
            WinUsb_ResetPipe(WinUSBHandle.DangerousGetHandle(), WriteBulkID);
        }
    }

    public override int ControlTransfer(FirmwareKit.Comm.Usb.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        if (WinUSBHandle.IsInvalid)
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

        var transferPacket = new WINUSB_SETUP_PACKET
        {
            RequestType = setupPacket.RequestType,
            Request = setupPacket.Request,
            Value = setupPacket.Value,
            Index = setupPacket.Index,
            Length = (ushort)Math.Max(0, length)
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

        uint bytesTransferred;
        if (!WinUsb_ControlTransfer(WinUSBHandle.DangerousGetHandle(), transferPacket, transferBuffer, (uint)length, out bytesTransferred, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (isInDirection && buffer != null && transferBuffer != null && transferBuffer != buffer)
        {
            Buffer.BlockCopy(transferBuffer, 0, buffer, offset, (int)bytesTransferred);
        }

        return (int)bytesTransferred;
    }

    public override int GetSerialNumber()
    {
        uint bytes_get;
        uint descriptorSize = 64;
        IntPtr ptr = Marshal.AllocHGlobal((int)descriptorSize);
        while (!WinUsb_GetDescriptor(WinUSBHandle.DangerousGetHandle(), USB_STRING_DESCRIPTOR_TYPE,
            USBDeviceDescriptor.iSerialNumber, 0x0409,
            ptr, descriptorSize, out bytes_get))
        {
            if ((uint)Marshal.GetLastWin32Error() != (uint)ERROR_INSUFFICIENT_BUFFER)
                return Marshal.GetLastWin32Error();
            descriptorSize *= 2;
            Marshal.FreeHGlobal(ptr);
            ptr = Marshal.AllocHGlobal((int)descriptorSize);
        }
        SerialNumber = Marshal.PtrToStringUni(ptr + 2, (int)(bytes_get - 2) / 2)?.TrimEnd('\0');
        Marshal.FreeHGlobal(ptr);
        return 0;
    }

    public override byte[] Read(int length)
    {
        return Read(length, DefaultTimeoutMs);
    }

    public override byte[] Read(int length, int timeoutMs)
    {
        if (length <= 0) return Array.Empty<byte>();

        byte[] data = new byte[length];
        int totalBytesRead = ReadInto(data, 0, length, timeoutMs);
        if (totalBytesRead == length) return data;
        if (totalBytesRead == 0) return Array.Empty<byte>();

        byte[] realData = new byte[totalBytesRead];
        Buffer.BlockCopy(data, 0, realData, 0, totalBytesRead);
        return realData;
    }

    public override int ReadInto(byte[] buffer, int offset, int length)
    {
        return ReadInto(buffer, offset, length, DefaultTimeoutMs);
    }

    protected override string BackendName => "winusb";

    protected override bool IsOpen => !WinUSBHandle.IsInvalid;

    protected override UsbChunkResult ReadChunk(IntPtr buffer, int length, int timeoutMs)
    {
        SetPipeTimeout(timeoutMs);
        uint bytesRead;
        if (WinUsb_ReadPipe(WinUSBHandle.DangerousGetHandle(), ReadBulkID, buffer, (uint)length, out bytesRead, IntPtr.Zero))
        {
            return UsbChunkResult.Success((int)bytesRead);
        }

        int err = Marshal.GetLastWin32Error();
        return err == ERROR_SEM_TIMEOUT ? UsbChunkResult.Timeout(err) : UsbChunkResult.Fatal(err);
    }

    protected override UsbChunkResult WriteChunk(IntPtr buffer, int length, int timeoutMs)
    {
        SetPipeTimeout(timeoutMs);
        uint bytesWritten;
        if (WinUsb_WritePipe(WinUSBHandle.DangerousGetHandle(), WriteBulkID, buffer, (uint)length, out bytesWritten, IntPtr.Zero))
        {
            return UsbChunkResult.Success((int)bytesWritten);
        }

        return UsbChunkResult.Fatal(Marshal.GetLastWin32Error());
    }

    protected override Exception CreateReadFatalException(int nativeError) => new Win32Exception(nativeError);

    protected override Exception CreateWriteFatalException(int nativeError) => new Win32Exception(nativeError);

    public override long Write(byte[] data, int length)
    {
        return Write(data, length, DefaultTimeoutMs);
    }

    public override Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (WinUSBHandle.IsInvalid)
        {
            return Task.FromException<int>(new UsbDeviceHandleClosedException("Device handle is closed."));
        }
        if (length <= 0) return Task.FromResult(0);
        ValidateBufferRange(buffer, offset, length);
        return ReadIntoOverlappedAsync(buffer, offset, length, timeoutMs, cancellationToken);
    }

    private async Task<int> ReadIntoOverlappedAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken)
    {
        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, DefaultTimeoutMs);
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            int total = 0;
            int remaining = length;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int lenToRead = Math.Min(remaining, MaxChunkSize);
                IntPtr ptr = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + offset + total);
                uint transferred = await OverlappedTransferAsync(ptr, lenToRead, ReadBulkID, effectiveTimeoutMs, cancellationToken).ConfigureAwait(false);
                total += (int)transferred;
                remaining -= (int)transferred;
                if (transferred < lenToRead) break; // short packet or timeout
            }
            return total;
        }
        finally
        {
            handle.Free();
        }
    }

    public override Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (WinUSBHandle.IsInvalid)
        {
            return Task.FromException<long>(new UsbDeviceHandleClosedException("Device handle is closed."));
        }
        ValidateWriteData(data, length);
        if (length == 0) return Task.FromResult(0L);
        return WriteOverlappedAsync(data, length, timeoutMs, cancellationToken);
    }

    private async Task<long> WriteOverlappedAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken)
    {
        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, DefaultTimeoutMs);
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            int total = 0;
            int remaining = length;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int lenToSend = Math.Min(remaining, MaxChunkSize);
                IntPtr ptr = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + total);
                uint transferred = await OverlappedTransferAsync(ptr, lenToSend, WriteBulkID, effectiveTimeoutMs, cancellationToken).ConfigureAwait(false);
                total += (int)transferred;
                remaining -= (int)transferred;
                if (transferred < lenToSend) break;
            }
            return total;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Performs a single chunk transfer with native overlapped (asynchronous) I/O.
    /// <para>使用原生重叠（异步）I/O 执行单次分块传输。</para>
    /// </summary>
    private async Task<uint> OverlappedTransferAsync(IntPtr buffer, int length, byte pipeId, int timeoutMs, CancellationToken cancellationToken)
    {
        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset);
        var overlapped = new Win32API.OVERLAPPED
        {
            OffsetLow = 0,
            OffsetHigh = 0,
            hEvent = evt.SafeWaitHandle.DangerousGetHandle()
        };

        // The OVERLAPPED must stay pinned until the pending operation completes.
        GCHandle ovHandle = GCHandle.Alloc(overlapped, GCHandleType.Pinned);
        try
        {
            uint transferred = 0;
            bool ok = pipeId == ReadBulkID
                ? WinUsb_ReadPipe(WinUSBHandle.DangerousGetHandle(), pipeId, buffer, (uint)length, out transferred, ovHandle.AddrOfPinnedObject())
                : WinUsb_WritePipe(WinUSBHandle.DangerousGetHandle(), pipeId, buffer, (uint)length, out transferred, ovHandle.AddrOfPinnedObject());

            if (ok)
            {
                return transferred;
            }

            int err = Marshal.GetLastWin32Error();
            if (err != ERROR_IO_PENDING)
            {
                if (err == ERROR_SEM_TIMEOUT) return 0;
                throw new Win32Exception(err);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            RegisteredWaitHandle waitHandle = ThreadPool.RegisterWaitForSingleObject(
                evt, (_, timedOut) => tcs.TrySetResult(timedOut), null, timeoutMs, executeOnlyOnce: true);
            try
            {
                using CancellationTokenRegistration registration = cancellationToken.Register(() =>
                {
                    _ = CancelIoEx(WinUSBHandle.DangerousGetHandle(), ovHandle.AddrOfPinnedObject());
                    tcs.TrySetCanceled(cancellationToken);
                });

                bool timedOut;
                try
                {
                    timedOut = await tcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The cancellation handler already called CancelIoEx; wait for the aborted
                    // operation to complete so the pinned OVERLAPPED stays valid until freed.
                    _ = evt.WaitOne(5000);
                    throw;
                }

                if (timedOut)
                {
                    _ = CancelIoEx(WinUSBHandle.DangerousGetHandle(), ovHandle.AddrOfPinnedObject());
                    _ = evt.WaitOne(5000);
                    return 0;
                }

                // InternalHigh holds the transferred byte count on completion.
                long bytesDone = Marshal.ReadIntPtr(ovHandle.AddrOfPinnedObject(), (int)Marshal.OffsetOf(typeof(Win32API.OVERLAPPED), "InternalHigh")).ToInt64();
                return (uint)bytesDone;
            }
            finally
            {
                waitHandle.Unregister(null);
            }
        }
        finally
        {
            ovHandle.Free();
        }
    }

    public override void Dispose()
    {
        WinUSBHandle.Dispose();
        FileHandle.Dispose();
    }


}




