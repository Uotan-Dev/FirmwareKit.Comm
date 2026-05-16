using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Usb.Backend.HarmonyOS.HarmonyOSUsbDDK;

namespace FirmwareKit.Comm.Usb.Backend.HarmonyOS;

internal class HarmonyOSUsbDevice : UsbDevice
{
    private const int PlatformDefaultTimeoutMs = UsbTransferPolicies.DefaultTimeoutMs;

    private ulong _deviceId;
    private ulong _interfaceHandle;
    private byte _interfaceIndex;
    private byte _epIn;
    private byte _epOut;
    private IntPtr _devMmap;
    private bool _disposed;
    private bool _ddkInitialized;

    public override int DefaultTimeoutMs => PlatformDefaultTimeoutMs;

    internal void Initialize(
        ulong deviceId,
        byte interfaceIndex,
        byte epIn,
        byte epOut,
        ushort vendorId,
        ushort productId,
        byte interfaceClass,
        byte interfaceSubClass,
        byte interfaceProtocol,
        string? serialNumber)
    {
        _deviceId = deviceId;
        _interfaceIndex = interfaceIndex;
        _epIn = epIn;
        _epOut = epOut;
        VendorId = vendorId;
        ProductId = productId;
        InterfaceClass = interfaceClass;
        InterfaceSubClass = interfaceSubClass;
        InterfaceProtocol = interfaceProtocol;
        InterfaceMetadataObserved = true;
        UsbDeviceType = UsbDeviceType.HarmonyOS;
        DevicePath = $"harmony-ddk://{deviceId}";
        SerialNumber = serialNumber;
    }

    public override int CreateHandle()
    {
        int ret = OH_Usb_Init();
        if (ret != USB_DDK_NO_ERROR)
        {
            UsbTrace.Log($"HarmonyOSUsbDevice: OH_Usb_Init failed: {GetErrorMessage(ret)}");
            return ret;
        }

        _ddkInitialized = true;

        ret = OH_Usb_ClaimInterface(_deviceId, _interfaceIndex, ref _interfaceHandle);
        if (ret != USB_DDK_NO_ERROR)
        {
            UsbTrace.Log($"HarmonyOSUsbDevice: OH_Usb_ClaimInterface failed: {GetErrorMessage(ret)}");
            OH_Usb_Release();
            _ddkInitialized = false;
            return ret;
        }

        IntPtr devMmapPtr = IntPtr.Zero;
        ret = OH_Usb_CreateDeviceMemMap(_deviceId, (UIntPtr)UsbTransferPolicies.LinuxUsbFsMaxBulkSize, ref devMmapPtr);
        if (ret != USB_DDK_NO_ERROR)
        {
            UsbTrace.Log($"HarmonyOSUsbDevice: OH_Usb_CreateDeviceMemMap failed: {GetErrorMessage(ret)}");
            OH_Usb_ReleaseInterface(_interfaceHandle);
            OH_Usb_Release();
            _ddkInitialized = false;
            return ret;
        }

        _devMmap = devMmapPtr;

        GetSerialNumber();
        return USB_DDK_NO_ERROR;
    }

    public override void Reset()
    {
        if (_disposed || !_ddkInitialized) return;

        OH_Usb_ReleaseInterface(_interfaceHandle);
        OH_Usb_Release();

        _ddkInitialized = false;

        int ret = OH_Usb_Init();
        if (ret == USB_DDK_NO_ERROR)
        {
            _ddkInitialized = true;
            OH_Usb_ClaimInterface(_deviceId, _interfaceIndex, ref _interfaceHandle);
        }
    }

    public override int ControlTransfer(Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        if (_disposed || !_ddkInitialized)
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
        var stopwatch = Stopwatch.StartNew();

        var setup = new UsbControlRequestSetup
        {
            bmRequestType = setupPacket.RequestType,
            bRequest = setupPacket.Request,
            wValue = setupPacket.Value,
            wIndex = setupPacket.Index,
            wLength = (ushort)length
        };

        int ret;
        bool isInDirection = (setupPacket.RequestType & 0x80) != 0;

        if (isInDirection)
        {
            byte[] readBuffer = new byte[length > 0 ? length : 256];
            uint dataLen = (uint)readBuffer.Length;

            ret = OH_Usb_SendControlReadRequest(_interfaceHandle, ref setup, (uint)effectiveTimeoutMs, readBuffer, ref dataLen);

            stopwatch.Stop();

            if (ret == USB_DDK_NO_ERROR && buffer != null && dataLen > 0)
            {
                int copyLen = Math.Min((int)dataLen, length);
                Buffer.BlockCopy(readBuffer, 0, buffer, offset, copyLen);
                EmitControlTransferTrace(length, copyLen, effectiveTimeoutMs, stopwatch.ElapsedMilliseconds, UsbTransferOutcome.Success);
                return copyLen;
            }
        }
        else
        {
            byte[] writeBuffer;
            if (length > 0 && buffer != null)
            {
                writeBuffer = new byte[length];
                Buffer.BlockCopy(buffer, offset, writeBuffer, 0, length);
            }
            else
            {
                writeBuffer = Array.Empty<byte>();
            }

            ret = OH_Usb_SendControlWriteRequest(_interfaceHandle, ref setup, (uint)effectiveTimeoutMs, writeBuffer, (uint)writeBuffer.Length);

            stopwatch.Stop();

            if (ret == USB_DDK_NO_ERROR)
            {
                EmitControlTransferTrace(length, length, effectiveTimeoutMs, stopwatch.ElapsedMilliseconds, UsbTransferOutcome.Success);
                return length;
            }
        }

        var outcome = ret == USB_DDK_TIMEOUT ? UsbTransferOutcome.Timeout : UsbTransferOutcome.FatalError;
        EmitControlTransferTrace(length, 0, effectiveTimeoutMs, stopwatch.ElapsedMilliseconds, outcome);

        if (ret != USB_DDK_NO_ERROR)
        {
            throw new UsbTransferException($"USB control transfer failed: {GetErrorMessage(ret)}", ret);
        }

        return 0;
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

    public override int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
    {
        if (_disposed || !_ddkInitialized)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }

        ValidateBufferRange(buffer, offset, length);
        if (length <= 0) return 0;

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
        var stopwatch = Stopwatch.StartNew();

        var pipe = new UsbRequestPipe
        {
            interfaceHandle = _interfaceHandle,
            endpointAddress = _epIn,
            timeout = (uint)effectiveTimeoutMs
        };

        var devMmap = new UsbDeviceMemMap
        {
            deviceId = _deviceId,
            buffer = Marshal.AllocHGlobal(length),
            size = (UIntPtr)length,
            offset = UIntPtr.Zero,
            length = (UIntPtr)length
        };

        try
        {
            int ret = OH_Usb_SendPipeRequest(ref pipe, ref devMmap);

            stopwatch.Stop();

            if (ret == USB_DDK_NO_ERROR)
            {
                int transferred = (int)devMmap.length;
                if (transferred > 0)
                {
                    Marshal.Copy(devMmap.buffer, buffer, offset, Math.Min(transferred, length));
                }

                var outcome = transferred >= length ? UsbTransferOutcome.Success : UsbTransferOutcome.ShortTransfer;
                EmitTransferTrace(UsbTransferOperation.Read, length, transferred, effectiveTimeoutMs, stopwatch.ElapsedMilliseconds, outcome);
                return transferred;
            }

            var errorOutcome = ret == USB_DDK_TIMEOUT ? UsbTransferOutcome.Timeout : UsbTransferOutcome.FatalError;
            EmitTransferTrace(UsbTransferOperation.Read, length, 0, effectiveTimeoutMs, stopwatch.ElapsedMilliseconds, errorOutcome);

            if (ret == USB_DDK_TIMEOUT) return 0;

            throw new UsbTransferException($"USB read failed: {GetErrorMessage(ret)}", ret);
        }
        finally
        {
            Marshal.FreeHGlobal(devMmap.buffer);
        }
    }

    public override long Write(byte[] data, int length)
    {
        return Write(data, length, PlatformDefaultTimeoutMs);
    }

    public override long Write(byte[] data, int length, int timeoutMs)
    {
        if (_disposed || !_ddkInitialized)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }

        if (length == 0) return 0;
        ValidateWriteData(data, length);

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
        var stopwatch = Stopwatch.StartNew();

        var pipe = new UsbRequestPipe
        {
            interfaceHandle = _interfaceHandle,
            endpointAddress = _epOut,
            timeout = (uint)effectiveTimeoutMs
        };

        IntPtr bufferPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.Copy(data, 0, bufferPtr, length);

            var devMmap = new UsbDeviceMemMap
            {
                deviceId = _deviceId,
                buffer = bufferPtr,
                size = (UIntPtr)length,
                offset = UIntPtr.Zero,
                length = (UIntPtr)length
            };

            int ret = OH_Usb_SendPipeRequest(ref pipe, ref devMmap);

            stopwatch.Stop();

            if (ret == USB_DDK_NO_ERROR)
            {
                int transferred = (int)devMmap.length;
                var outcome = transferred >= length ? UsbTransferOutcome.Success : UsbTransferOutcome.ShortTransfer;
                EmitTransferTrace(UsbTransferOperation.Write, length, transferred, effectiveTimeoutMs, stopwatch.ElapsedMilliseconds, outcome);
                return transferred;
            }

            var errorOutcome = ret == USB_DDK_TIMEOUT ? UsbTransferOutcome.Timeout : UsbTransferOutcome.FatalError;
            EmitTransferTrace(UsbTransferOperation.Write, length, 0, effectiveTimeoutMs, stopwatch.ElapsedMilliseconds, errorOutcome);

            if (ret == USB_DDK_TIMEOUT) return -1;

            throw new UsbTransferException($"USB write failed: {GetErrorMessage(ret)}", ret);
        }
        finally
        {
            Marshal.FreeHGlobal(bufferPtr);
        }
    }

    public override int GetSerialNumber()
    {
        if (_disposed || !_ddkInitialized) return -1;

        var setup = new UsbControlRequestSetup
        {
            bmRequestType = 0x80,
            bRequest = 0x06,
            wValue = 0x0300,
            wIndex = 0,
            wLength = 255
        };

        byte[] descriptor = new byte[256];
        uint dataLen = 256;

        int ret = OH_Usb_SendControlReadRequest(_interfaceHandle, ref setup, 1000, descriptor, ref dataLen);
        if (ret != USB_DDK_NO_ERROR || dataLen < 4) return -1;

        int langCount = (int)(dataLen - 2) / 2;
        if (langCount == 0) return -1;

        ushort langId = (ushort)(descriptor[2] | (descriptor[3] << 8));

        var devDesc = new UsbDeviceDescriptor();
        ret = OH_Usb_GetDeviceDescriptor(_deviceId, ref devDesc);
        if (ret != USB_DDK_NO_ERROR) return -1;

        if (devDesc.iSerialNumber == 0) return -1;

        setup.wValue = (ushort)((0x03 << 8) | devDesc.iSerialNumber);
        setup.wIndex = langId;
        setup.wLength = 255;
        dataLen = 256;

        ret = OH_Usb_SendControlReadRequest(_interfaceHandle, ref setup, 1000, descriptor, ref dataLen);
        if (ret != USB_DDK_NO_ERROR || dataLen < 2) return -1;

        int stringLen = (int)(dataLen - 2);
        if (stringLen > 0)
        {
            SerialNumber = System.Text.Encoding.Unicode.GetString(descriptor, 2, stringLen).TrimEnd('\0');
        }

        return 0;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_devMmap != IntPtr.Zero)
        {
            OH_Usb_DestroyDeviceMemMap(_devMmap);
            _devMmap = IntPtr.Zero;
        }

        if (_interfaceHandle != 0)
        {
            OH_Usb_ReleaseInterface(_interfaceHandle);
            _interfaceHandle = 0;
        }

        if (_ddkInitialized)
        {
            OH_Usb_Release();
            _ddkInitialized = false;
        }
    }

    private void EmitControlTransferTrace(int requested, int transferred, int timeoutMs, long elapsedMs, UsbTransferOutcome outcome)
    {
        UsbTrace.EmitTransfer(new UsbTransferEvent
        {
            Backend = "harmony-usbddk",
            DevicePath = DevicePath,
            Operation = UsbTransferOperation.Write,
            RequestedBytes = requested,
            TransferredBytes = transferred,
            TimeoutMs = timeoutMs,
            RetryCount = 0,
            NativeErrorCode = null,
            ElapsedMs = elapsedMs,
            Outcome = outcome
        });
    }

    private void EmitTransferTrace(UsbTransferOperation operation, int requested, int transferred, int timeoutMs, long elapsedMs, UsbTransferOutcome outcome)
    {
        UsbTrace.EmitTransfer(new UsbTransferEvent
        {
            Backend = "harmony-usbddk",
            DevicePath = DevicePath,
            Operation = operation,
            RequestedBytes = requested,
            TransferredBytes = transferred,
            TimeoutMs = timeoutMs,
            RetryCount = 0,
            NativeErrorCode = null,
            ElapsedMs = elapsedMs,
            Outcome = outcome
        });
    }
}
