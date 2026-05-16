using FirmwareKit.Comm.Usb.Abstractions;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Usb.Backend.Windows.Win32API;

namespace FirmwareKit.Comm.Usb.Backend.Windows;

internal class LegacyUsbDevice : UsbDevice
{
    private const int IoTimeoutMs = 30000;
    public override int DefaultTimeoutMs => IoTimeoutMs;
    public static uint IoGetSerialCode => CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_READ_ACCESS);
    public static uint IoGetDescriptorCode => CTL_CODE(FILE_DEVICE_UNKNOWN, 0x802, METHOD_BUFFERED, FILE_READ_ACCESS);
    public static uint IoControlTransferCode => CTL_CODE(FILE_DEVICE_UNKNOWN, 0x803, METHOD_BUFFERED, FILE_READ_ACCESS);

    private IntPtr fileHandle = INVALID_HANDLE_VALUE;
    private bool _disposed;

    public IntPtr Handle => fileHandle;

    public override int CreateHandle()
    {
        fileHandle = SimpleCreateHandle(DevicePath);
        if (fileHandle == INVALID_HANDLE_VALUE)
            return Marshal.GetLastWin32Error();

        if (!CheckInterface())
        {
            CloseHandle(fileHandle);
            fileHandle = INVALID_HANDLE_VALUE;
            return -1;
        }

        GetSerialNumber();
        return 0;
    }

    private bool CheckInterface()
    {
        byte[] buffer = new byte[256];
        uint returned;
        return DeviceIoControl(fileHandle, IoGetSerialCode, null, 0, buffer, (uint)buffer.Length, out returned, IntPtr.Zero);
    }

    public override int GetSerialNumber()
    {
        byte[] buffer = new byte[256];
        uint returned;
        if (DeviceIoControl(fileHandle, IoGetSerialCode, null, 0, buffer, (uint)buffer.Length, out returned, IntPtr.Zero))
        {
            SerialNumber = System.Text.Encoding.Unicode.GetString(buffer, 0, (int)returned).TrimEnd('\0');
            return 0;
        }
        return Marshal.GetLastWin32Error();
    }

    public override int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        if (_disposed || fileHandle == INVALID_HANDLE_VALUE)
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

        int setupSize = Marshal.SizeOf<WinUSBAPI.WINUSB_SETUP_PACKET>();
        var nativeSetup = new WinUSBAPI.WINUSB_SETUP_PACKET
        {
            RequestType = setupPacket.RequestType,
            Request = setupPacket.Request,
            Value = setupPacket.Value,
            Index = setupPacket.Index,
            Length = (ushort)length
        };

        int totalInSize = setupSize + length;
        IntPtr inBuffer = Marshal.AllocHGlobal(totalInSize);
        try
        {
            Marshal.StructureToPtr(nativeSetup, inBuffer, false);

            if (length > 0 && buffer != null && (setupPacket.RequestType & 0x80) == 0)
            {
                Marshal.Copy(buffer, offset, new IntPtr(inBuffer.ToInt64() + setupSize), length);
            }

            byte[] outBuffer = new byte[setupSize + length];
            uint returned;

            if (!DeviceIoControl(fileHandle, IoControlTransferCode, inBuffer, (uint)totalInSize, outBuffer, (uint)outBuffer.Length, out returned, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            int transferred = (int)returned;
            if ((setupPacket.RequestType & 0x80) != 0 && buffer != null && transferred > 0)
            {
                int copyLen = Math.Min(transferred, length);
                Buffer.BlockCopy(outBuffer, 0, buffer, offset, copyLen);
                return copyLen;
            }

            return transferred;
        }
        finally
        {
            Marshal.FreeHGlobal(inBuffer);
        }
    }

    public override byte[] Read(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var readTask = Task.Run(() =>
        {
            byte[] buffer = new byte[length];
            uint read;
            if (ReadFile(fileHandle, buffer, (uint)length, out read, IntPtr.Zero))
            {
                byte[] result = new byte[read];
                Array.Copy(buffer, result, (int)read);
                return result;
            }
            throw new Win32Exception(Marshal.GetLastWin32Error());
        });

        if (!readTask.Wait(IoTimeoutMs))
        {
            throw new TimeoutException($"Legacy USB read timed out after {IoTimeoutMs} ms.");
        }

        return readTask.GetAwaiter().GetResult();
    }

    public override int ReadInto(byte[] buffer, int offset, int length)
    {
        return ReadInto(buffer, offset, length, IoTimeoutMs);
    }

    public override int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
    {
        if (_disposed || fileHandle == INVALID_HANDLE_VALUE)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }

        ValidateBufferRange(buffer, offset, length);
        if (length <= 0) return 0;

        uint bytesRead;
        if (!ReadFile(fileHandle, buffer, (uint)length, out bytesRead, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return (int)bytesRead;
    }

    public override long Write(byte[] data, int length)
    {
        return Write(data, length, IoTimeoutMs);
    }

    public override long Write(byte[] data, int length, int timeoutMs)
    {
        if (_disposed || fileHandle == INVALID_HANDLE_VALUE)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }

        ValidateWriteData(data, length);

        if (length == 0) return 0;

        uint written;
        if (!WriteFile(fileHandle, data, (uint)length, out written, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return written;
    }

    public override void Reset()
    {
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (fileHandle != INVALID_HANDLE_VALUE)
        {
            CloseHandle(fileHandle);
            fileHandle = INVALID_HANDLE_VALUE;
        }
        GC.SuppressFinalize(this);
    }
}
