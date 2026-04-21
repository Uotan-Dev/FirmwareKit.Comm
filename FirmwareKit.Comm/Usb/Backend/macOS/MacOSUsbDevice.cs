using System.Runtime.InteropServices;
using System.Diagnostics;
using FirmwareKit.Comm.Usb.Diagnostics;
using static FirmwareKit.Comm.Usb.Backend.macOS.MacOSUsbAPI;

namespace FirmwareKit.Comm.Usb.Backend.macOS;

internal class MacOSUsbDevice : UsbDevice
{
    private const int DefaultTimeoutMs = UsbTransferPolicies.DefaultTimeoutMs;

    private IntPtr devicePtr;
    private IntPtr interfacePtr;
    public ulong RegistryEntryId { get; set; }
    public byte bulkIn { get; set; }
    public byte bulkOut { get; set; }

    public override int CreateHandle()
    {
        IntPtr service = IntPtr.Zero;
        if (RegistryEntryId != 0)
        {
            IntPtr matching = IORegistryEntryIDMatching(RegistryEntryId);
            if (matching != IntPtr.Zero)
            {
                service = IOServiceGetMatchingService(IntPtr.Zero, matching);
            }
        }

        if (service == IntPtr.Zero && !string.IsNullOrWhiteSpace(DevicePath))
        {
            service = IORegistryEntryFromPath(IntPtr.Zero, DevicePath);
        }

        if (service == IntPtr.Zero) return -1;

        try
        {
            IntPtr pluginInterface = IntPtr.Zero;
            int score = 0;
            var pluginTypeGuid = kIOUSBDeviceUserClientTypeID;
            var pluginInterfaceGuid = kIOCFPlugInInterfaceID;
            int kr = IOCreatePlugInInterfaceForService(service, ref pluginTypeGuid, ref pluginInterfaceGuid, out pluginInterface, out score);
            if (kr != 0 || pluginInterface == IntPtr.Zero) return kr;

            try
            {
                if (!TryQueryInterface(pluginInterface, out devicePtr, kIOUSBDeviceInterfaceID197, kIOUSBDeviceInterfaceID)) return -1;

                try
                {
                    var openDev = GetDelegate<USBDeviceOpenDelegate>(devicePtr, Offset_USBDeviceOpen);
                    var createIter = GetDelegate<USBDeviceCreateInterfaceIteratorDelegate>(devicePtr, Offset_USBDeviceCreateInterfaceIterator);
                    var setConf = GetDelegate<USBSetConfigurationDelegate>(devicePtr, Offset_USBSetConfiguration);
                    var getConf = GetDelegate<USBGetConfigurationDelegate>(devicePtr, Offset_USBGetConfiguration);

                    openDev(devicePtr);

                    byte currentConf;
                    if (getConf(devicePtr, out currentConf) == 0 && currentConf != 1)
                    {
                        setConf(devicePtr, 1);
                    }

                    IOUSBFindInterfaceRequest request = new IOUSBFindInterfaceRequest
                    {
                        bInterfaceClass = kIOUSBFindInterfaceDontCare,
                        bInterfaceSubClass = kIOUSBFindInterfaceDontCare,
                        bInterfaceProtocol = kIOUSBFindInterfaceDontCare,
                        bAlternateSetting = kIOUSBFindInterfaceDontCare
                    };

                    IntPtr interfaceIter;
                    if (createIter(devicePtr, ref request, out interfaceIter) == 0 && interfaceIter != IntPtr.Zero)
                    {
                        IntPtr ifcService;
                        while ((ifcService = IOIteratorNext(interfaceIter)) != IntPtr.Zero)
                        {
                            IntPtr ifcPlugin = IntPtr.Zero;
                            var ifcPluginTypeGuid = kIOUSBInterfaceUserClientTypeID;
                            var ifcPluginInterfaceGuid = kIOCFPlugInInterfaceID;
                            if (IOCreatePlugInInterfaceForService(ifcService, ref ifcPluginTypeGuid, ref ifcPluginInterfaceGuid, out ifcPlugin, out score) == 0 && ifcPlugin != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr candidateInterface;
                                    if (TryQueryInterface(ifcPlugin, out candidateInterface, kIOUSBInterfaceInterfaceID197, kIOUSBInterfaceInterfaceID190, kIOUSBInterfaceInterfaceID))
                                    {
                                        try
                                        {
                                            var getNumEpts = GetDelegate<GetNumEndpointsDelegate>(candidateInterface, Offset_GetNumEndpoints);
                                            var getPipeProps = GetDelegate<GetPipePropertiesDelegate>(candidateInterface, Offset_GetPipeProperties);

                                            byte numEpts;
                                            if (getNumEpts(candidateInterface, out numEpts) != 0)
                                            {
                                                continue;
                                            }

                                            byte candidateBulkIn = 0;
                                            byte candidateBulkOut = 0;
                                            for (byte i = 1; i <= numEpts; i++)
                                            {
                                                byte direction, number, transferType, interval;
                                                ushort maxPacketSize;
                                                if (getPipeProps(candidateInterface, i, out direction, out number, out transferType, out maxPacketSize, out interval) != 0)
                                                {
                                                    continue;
                                                }

                                                if (transferType != 0x02)
                                                {
                                                    continue;
                                                }

                                                if (direction == 1)
                                                {
                                                    if (candidateBulkIn == 0)
                                                    {
                                                        candidateBulkIn = i;
                                                    }
                                                }
                                                else
                                                {
                                                    if (candidateBulkOut == 0)
                                                    {
                                                        candidateBulkOut = i;
                                                    }
                                                }
                                            }

                                            if (candidateBulkIn == 0 || candidateBulkOut == 0)
                                            {
                                                continue;
                                            }

                                            if (bulkIn != 0 && bulkIn != candidateBulkIn)
                                            {
                                                continue;
                                            }

                                            if (bulkOut != 0 && bulkOut != candidateBulkOut)
                                            {
                                                continue;
                                            }

                                            var ifcOpen = GetDelegate<USBInterfaceOpenDelegate>(candidateInterface, Offset_USBInterfaceOpen);
                                            kr = ifcOpen(candidateInterface);
                                            if (kr != 0)
                                            {
                                                continue;
                                            }

                                            bulkIn = candidateBulkIn;
                                            bulkOut = candidateBulkOut;
                                            interfacePtr = candidateInterface;

                                            var ifcClear = GetDelegate<ClearPipeStallBothEndsDelegate>(interfacePtr, Offset_ClearPipeStallBothEnds);
                                            if (bulkIn != 0) ifcClear(interfacePtr, bulkIn);
                                            if (bulkOut != 0) ifcClear(interfacePtr, bulkOut);
                                            candidateInterface = IntPtr.Zero;
                                            break;
                                        }
                                        finally
                                        {
                                            if (candidateInterface != IntPtr.Zero)
                                            {
                                                GetDelegate<ReleaseDelegate>(candidateInterface, Offset_IUnknown_Release)(candidateInterface);
                                            }
                                        }
                                    }
                                }
                                finally { GetDelegate<ReleaseDelegate>(ifcPlugin, Offset_Plugin_Release)(ifcPlugin); }
                            }
                            IOObjectRelease(ifcService);
                            if (interfacePtr != IntPtr.Zero)
                            {
                                break;
                            }
                        }
                        IOObjectRelease(interfaceIter);
                    }
                }
                catch (Exception ex)
                {
                    UsbTrace.Log($"MacOSUsbDevice.CreateHandle failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
            finally { GetDelegate<ReleaseDelegate>(pluginInterface, Offset_Plugin_Release)(pluginInterface); }
        }
        finally { IOObjectRelease(service); }

        return interfacePtr != IntPtr.Zero ? 0 : -1;
    }

    public override void Reset()
    {
        if (devicePtr != IntPtr.Zero)
        {
            GetDelegate<USBDeviceResetDelegate>(devicePtr, Offset_USBDeviceReset)(devicePtr);
        }
    }

    public override void Dispose()
    {
        if (interfacePtr != IntPtr.Zero)
        {
            GetDelegate<USBInterfaceCloseDelegate>(interfacePtr, Offset_USBInterfaceClose)(interfacePtr);
            GetDelegate<ReleaseDelegate>(interfacePtr, Offset_IUnknown_Release)(interfacePtr);
            interfacePtr = IntPtr.Zero;
        }
        if (devicePtr != IntPtr.Zero)
        {
            GetDelegate<USBDeviceCloseDelegate>(devicePtr, Offset_USBDeviceClose)(devicePtr);
            GetDelegate<ReleaseDelegate>(devicePtr, Offset_IUnknown_Release)(devicePtr);
            devicePtr = IntPtr.Zero;
        }
    }

    public override int GetSerialNumber()
    {
        if (devicePtr == IntPtr.Zero) return -1;

        byte serialIndex;
        var getIdx = GetDelegate<USBGetSerialNumberStringIndexDelegate>(devicePtr, Offset_USBGetSerialNumberStringIndex);
        if (getIdx(devicePtr, out serialIndex) != 0 || serialIndex == 0) return -1;

        IOUSBDevRequest req = new IOUSBDevRequest();
        byte[] buf = new byte[256];
        GCHandle handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
        try
        {
            req.bmRequestType = 0x80;
            req.bRequest = 0x06;
            req.wValue = (ushort)((0x03 << 8) | serialIndex);
            req.wIndex = 0x0409;
            req.wLength = (ushort)buf.Length;
            req.pData = handle.AddrOfPinnedObject();

            var devReq = GetDelegate<DeviceRequestDelegate>(devicePtr, Offset_DeviceRequest);
            if (devReq(devicePtr, ref req) == 0 && req.wLenDone > 2)
            {
                SerialNumber = System.Text.Encoding.Unicode.GetString(buf, 2, (int)req.wLenDone - 2).TrimEnd('\0');
                return 0;
            }
            return -1;
        }
        finally { handle.Free(); }
    }

    public override byte[] Read(int length)
    {
        return Read(length, DefaultTimeoutMs);
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
        return ReadInto(buffer, offset, length, DefaultTimeoutMs);
    }

    public override int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        int? lastError = null;
        var outcome = UsbTransferOutcome.Success;

        if (interfacePtr == IntPtr.Zero || bulkIn == 0) return 0;
        if (length <= 0) return 0;
        if (offset < 0 || length < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, DefaultTimeoutMs);

        const int maxLenToRead = UsbTransferPolicies.MaxChunkSize;
        int lenRemaining = length;
        int count = 0;
        var readPipe = GetDelegate<ReadPipeTODelegate>(interfacePtr, Offset_ReadPipeTO);

        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            while (lenRemaining > 0)
            {
                int lenToRead = Math.Min(lenRemaining, maxLenToRead);
                IntPtr ptr = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + offset + count);
                uint size = (uint)lenToRead;

                int kr = readPipe(interfacePtr, bulkIn, ptr, ref size, (uint)effectiveTimeoutMs, (uint)effectiveTimeoutMs);
                if (kr != 0)
                {
                    lastError = kr;
                    if (kr == kIOReturnNoDevice || kr == kIOReturnNotResponding || kr == kIOReturnAborted)
                    {
                        outcome = UsbTransferOutcome.FatalError;
                        UsbTrace.EmitTransfer(new UsbTransferEvent
                        {
                            Backend = "macos-iokit",
                            DevicePath = DevicePath,
                            Operation = UsbTransferOperation.Read,
                            RequestedBytes = length,
                            TransferredBytes = count,
                            TimeoutMs = effectiveTimeoutMs,
                            RetryCount = 0,
                            NativeErrorCode = kr,
                            ElapsedMs = stopwatch.ElapsedMilliseconds,
                            Outcome = outcome
                        });
                        throw new IOException($"USB read failed with fatal error: 0x{kr:X}");
                    }
                    if (kr == kIOReturnTimeout)
                    {
                        outcome = UsbTransferOutcome.Timeout;
                    }
                    break;
                }

                count += (int)size;
                lenRemaining -= (int)size;

                if (size < lenToRead) break;
            }

            if (outcome == UsbTransferOutcome.Success && count > 0 && count < length)
            {
                outcome = UsbTransferOutcome.ShortTransfer;
            }

            UsbTrace.EmitTransfer(new UsbTransferEvent
            {
                Backend = "macos-iokit",
                DevicePath = DevicePath,
                Operation = UsbTransferOperation.Read,
                RequestedBytes = length,
                TransferredBytes = count,
                TimeoutMs = effectiveTimeoutMs,
                RetryCount = 0,
                NativeErrorCode = lastError,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Outcome = outcome
            });

            return count;
        }
        finally
        {
            handle.Free();
        }
    }

    public override long Write(byte[] data, int length)
    {
        return Write(data, length, DefaultTimeoutMs);
    }

    public override long Write(byte[] data, int length, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        int? lastError = null;
        var outcome = UsbTransferOutcome.Success;

        if (interfacePtr == IntPtr.Zero || bulkOut == 0) return -1;

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, DefaultTimeoutMs);

        const int maxLenToSend = UsbTransferPolicies.MaxChunkSize;
        int lenRemaining = length;
        int count = 0;
        var writePipe = GetDelegate<WritePipeTODelegate>(interfacePtr, Offset_WritePipeTO);

        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            while (lenRemaining > 0)
            {
                int lenToSend = Math.Min(lenRemaining, maxLenToSend);
                IntPtr ptr = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + count);

                int kr = writePipe(interfacePtr, bulkOut, ptr, (uint)lenToSend, (uint)effectiveTimeoutMs, (uint)effectiveTimeoutMs);
                if (kr != 0)
                {
                    lastError = kr;
                    if (kr == kIOReturnNoDevice || kr == kIOReturnNotResponding || kr == kIOReturnAborted)
                    {
                        outcome = UsbTransferOutcome.FatalError;
                        UsbTrace.EmitTransfer(new UsbTransferEvent
                        {
                            Backend = "macos-iokit",
                            DevicePath = DevicePath,
                            Operation = UsbTransferOperation.Write,
                            RequestedBytes = length,
                            TransferredBytes = count,
                            TimeoutMs = effectiveTimeoutMs,
                            RetryCount = 0,
                            NativeErrorCode = kr,
                            ElapsedMs = stopwatch.ElapsedMilliseconds,
                            Outcome = outcome
                        });
                        throw new IOException($"USB write failed with fatal error: 0x{kr:X}");
                    }
                    if (kr == kIOReturnTimeout)
                    {
                        outcome = UsbTransferOutcome.Timeout;
                    }
                    break;
                }

                lenRemaining -= lenToSend;
                count += lenToSend;
            }

            if (outcome == UsbTransferOutcome.Success && count > 0 && count < length)
            {
                outcome = UsbTransferOutcome.ShortTransfer;
            }

            UsbTrace.EmitTransfer(new UsbTransferEvent
            {
                Backend = "macos-iokit",
                DevicePath = DevicePath,
                Operation = UsbTransferOperation.Write,
                RequestedBytes = length,
                TransferredBytes = count,
                TimeoutMs = effectiveTimeoutMs,
                RetryCount = 0,
                NativeErrorCode = lastError,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Outcome = outcome
            });

            // Align with AOSP host behavior: avoid forcing explicit host-side ZLP.
            return count > 0 ? count : (length == 0 ? 0 : -1);
        }
        finally
        {
            handle.Free();
        }
    }


}



