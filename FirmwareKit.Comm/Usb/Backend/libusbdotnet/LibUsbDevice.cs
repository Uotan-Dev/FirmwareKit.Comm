using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using System.Diagnostics;

namespace FirmwareKit.Comm.Usb.Backend.libusbdotnet;

internal class LibUsbDevice : global::FirmwareKit.Comm.Usb.Backend.UsbDevice
{
    private const int PlatformDefaultTimeoutMs = UsbTransferPolicies.DefaultTimeoutMs;

    private UsbContext? context;
    private IUsbDevice? usbDevice;
    private UsbEndpointReader? reader;
    private UsbEndpointWriter? writer;
    public ushort Vid { get; set; }
    public ushort Pid { get; set; }
    public byte BusNumber { get; set; }
    public byte DeviceAddress { get; set; }
    public byte InterfaceId { get; set; } = 0;
    public byte ReadEndpointId { get; set; }
    public byte WriteEndpointId { get; set; }

    public override int DefaultTimeoutMs => PlatformDefaultTimeoutMs;

    private static string BuildDevicePath(LibUsbDotNet.LibUsb.UsbDevice device)
        => $"Bus {device.BusNumber} Device {device.Address}: {device.VendorId:X4}:{device.ProductId:X4}";

    private static bool HasBulkInterface(LibUsbDotNet.LibUsb.UsbDevice device)
    {
        try
        {
            foreach (var config in device.Configs)
            {
                foreach (var ifc in config.Interfaces)
                {
                    bool hasIn = false;
                    bool hasOut = false;
                    foreach (var endpoint in ifc.Endpoints)
                    {
                        if ((endpoint.Attributes & 0x03) != 0x02) continue;
                        if ((endpoint.EndpointAddress & 0x80) != 0) hasIn = true;
                        else hasOut = true;
                    }

                    if (hasIn && hasOut) return true;
                }
            }
        }
        catch
        {
            UsbTrace.Log("LibUsbDevice.HasBulkInterface: failed to enumerate interface endpoints.");
            return false;
        }

        return false;
    }

    public override int CreateHandle()
    {
        context = new UsbContext();
        var candidates = context.List().OfType<LibUsbDotNet.LibUsb.UsbDevice>().ToList();

        LibUsbDotNet.LibUsb.UsbDevice? device = null;

        if (BusNumber != 0 || DeviceAddress != 0)
        {
            device = candidates.FirstOrDefault(d => d.BusNumber == BusNumber && d.Address == DeviceAddress);
        }

        if (device == null && !string.IsNullOrWhiteSpace(DevicePath))
        {
            device = candidates.FirstOrDefault(d =>
                string.Equals(BuildDevicePath(d), DevicePath, StringComparison.OrdinalIgnoreCase));
        }

        if (device == null)
        {
            device = candidates.FirstOrDefault(d =>
                d.VendorId == Vid &&
                d.ProductId == Pid &&
                HasBulkInterface(d));
        }

        if (device == null)
        {
            context.Dispose();
            context = null;
            return -1;
        }

        usbDevice = device;
        try
        {
            usbDevice.Open();
        }
        catch
        {
            Dispose();
            return -1;
        }

        try
        {
            usbDevice.SetConfiguration(1);
        }
        catch (Exception ex)
        {
            UsbTrace.Log($"LibUsbDevice.SetConfiguration ignored: {ex.GetType().Name}: {ex.Message}");
        }

        byte targetInterfaceId = InterfaceId;
        byte inEndpoint = ReadEndpointId;
        byte outEndpoint = WriteEndpointId;

        if (inEndpoint == 0 || outEndpoint == 0)
        {
            foreach (var config in usbDevice.Configs)
            {
                foreach (var ifc in config.Interfaces)
                {
                    byte candidateIn = 0;
                    byte candidateOut = 0;
                    foreach (var endpoint in ifc.Endpoints)
                    {
                        if ((endpoint.Attributes & 0x03) != 0x02) continue;

                        if ((endpoint.EndpointAddress & 0x80) != 0)
                        {
                            if (candidateIn == 0) candidateIn = endpoint.EndpointAddress;
                        }
                        else
                        {
                            if (candidateOut == 0) candidateOut = endpoint.EndpointAddress;
                        }
                    }

                    if (candidateIn != 0 && candidateOut != 0)
                    {
                        targetInterfaceId = (byte)ifc.Number;
                        inEndpoint = candidateIn;
                        outEndpoint = candidateOut;
                        break;
                    }
                }

                if (inEndpoint != 0 && outEndpoint != 0) break;
            }
        }

        if (inEndpoint == 0 || outEndpoint == 0)
        {
            Dispose();
            return -1;
        }

        InterfaceId = targetInterfaceId;

        try
        {
            usbDevice.ClaimInterface(targetInterfaceId);
        }
        catch
        {
            try
            {
                (usbDevice as LibUsbDotNet.LibUsb.UsbDevice)?.DetachKernelDriver(targetInterfaceId);
                usbDevice.ClaimInterface(targetInterfaceId);
            }
            catch (Exception ex)
            {
                UsbTrace.Log($"LibUsbDevice.ClaimInterface fallback failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        reader = null;
        writer = null;

        if (inEndpoint != 0 && outEndpoint != 0)
        {
            reader = usbDevice.OpenEndpointReader((ReadEndpointID)inEndpoint);
            writer = usbDevice.OpenEndpointWriter((WriteEndpointID)outEndpoint);
        }

        if (reader == null || writer == null)
        {
            byte[] candidateInEndpoints = new byte[] { 0x81, 0x82, 0x83 };
            byte[] candidateOutEndpoints = new byte[] { 0x01, 0x02, 0x03 };

            for (int endpointIndex = 0; endpointIndex < candidateInEndpoints.Length; endpointIndex++)
            {
                var testReader = usbDevice.OpenEndpointReader((ReadEndpointID)candidateInEndpoints[endpointIndex]);
                var testWriter = usbDevice.OpenEndpointWriter((WriteEndpointID)candidateOutEndpoints[endpointIndex]);
                if (testReader != null && testWriter != null)
                {
                    reader = testReader;
                    writer = testWriter;
                    break;
                }
            }
        }

        reader?.ReadFlush();

        if (reader == null || writer == null)
        {
            Dispose();
            return -1;
        }

        GetSerialNumber();
        return 0;
    }

    public override void Dispose()
    {
        if (usbDevice != null)
        {
            usbDevice.Close();
            usbDevice = null;
        }
        if (context != null)
        {
            context.Dispose();
            context = null;
        }
    }

    public override int GetSerialNumber()
    {
        if (usbDevice != null)
        {
            SerialNumber = usbDevice.Info.SerialNumber;
            return 0;
        }
        return -1;
    }

    public override void Reset()
    {
        if (usbDevice != null)
        {
            try
            {
                usbDevice.ResetDevice();
            }
            catch (Exception ex)
            {
                UsbTrace.Log($"LibUsbDevice.Reset failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public override int ControlTransfer(FirmwareKit.Comm.Usb.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        if (usbDevice == null)
        {
            throw new Exception("Device handle is closed.");
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

        var libUsbSetup = new LibUsbDotNet.Main.UsbSetupPacket(setupPacket.RequestType, setupPacket.Request, setupPacket.Value, setupPacket.Index, setupPacket.Length);

        if (length == 0)
        {
            return usbDevice.ControlTransfer(libUsbSetup, null, 0, 0);
        }

        if (buffer != null && offset == 0 && length == buffer.Length)
        {
            return usbDevice.ControlTransfer(libUsbSetup, buffer, 0, length);
        }

        byte[] transferBuffer = new byte[length];
        bool isInDirection = (setupPacket.RequestType & 0x80) != 0;
        if (!isInDirection && buffer != null)
        {
            Buffer.BlockCopy(buffer, offset, transferBuffer, 0, length);
        }

        int transferred = usbDevice.ControlTransfer(libUsbSetup, transferBuffer, 0, length);
        if (isInDirection && buffer != null)
        {
            Buffer.BlockCopy(transferBuffer, 0, buffer, offset, Math.Min(transferred, length));
        }

        return transferred;
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
        var stopwatch = Stopwatch.StartNew();
        int? lastError = null;
        var outcome = UsbTransferOutcome.Success;

        if (reader == null) return 0;
        if (length <= 0) return 0;
        ValidateBufferRange(buffer, offset, length);

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);

        const int maxLenToRead = UsbTransferPolicies.MaxChunkSize;
        int lenRemaining = length;
        int count = 0;

        while (lenRemaining > 0)
        {
            int lenToRead = Math.Min(lenRemaining, maxLenToRead);
            int read_len;

            reader.Read(buffer, offset + count, lenToRead, effectiveTimeoutMs, out read_len);

            if (read_len <= 0)
            {
                outcome = UsbTransferOutcome.Timeout;
                break;
            }

            count += read_len;
            lenRemaining -= read_len;

            if (read_len < lenToRead) break;
        }

        if (outcome == UsbTransferOutcome.Success && count > 0 && count < length)
        {
            outcome = UsbTransferOutcome.ShortTransfer;
        }

        UsbTrace.EmitTransfer(new UsbTransferEvent
        {
            Backend = "libusb",
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

    public override long Write(byte[] data, int length)
    {
        return Write(data, length, PlatformDefaultTimeoutMs);
    }

    public override long Write(byte[] data, int length, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        int? lastError = null;
        var outcome = UsbTransferOutcome.Success;

        if (writer == null)
        {
            UsbTrace.Log("LibUsbDevice: writer is null");
            UsbTrace.EmitTransfer(new UsbTransferEvent
            {
                Backend = "libusb",
                DevicePath = DevicePath,
                Operation = UsbTransferOperation.Write,
                RequestedBytes = length,
                TransferredBytes = 0,
                TimeoutMs = timeoutMs > 0 ? timeoutMs : PlatformDefaultTimeoutMs,
                RetryCount = 0,
                NativeErrorCode = null,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Outcome = UsbTransferOutcome.NotReady
            });
            return -1;
        }

        ValidateWriteData(data, length);

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);

        const int maxLenToSend = UsbTransferPolicies.MaxChunkSize;
        int lenRemaining = length;
        int count = 0;

        UsbTrace.Log($"LibUsbDevice: Write attempt - length: {length}, data: {BitConverter.ToString(data, 0, Math.Min(length, 16))}");

        if (length == 0)
        {
            int transferred;
            var errorCode = writer.Write(data, 0, 0, effectiveTimeoutMs, out transferred);
            UsbTrace.Log($"LibUsbDevice: Zero-length write - transferred: {transferred}, errorCode: {errorCode}");
            UsbTrace.EmitTransfer(new UsbTransferEvent
            {
                Backend = "libusb",
                DevicePath = DevicePath,
                Operation = UsbTransferOperation.Write,
                RequestedBytes = 0,
                TransferredBytes = transferred,
                TimeoutMs = effectiveTimeoutMs,
                RetryCount = 0,
                NativeErrorCode = (int)errorCode,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Outcome = errorCode == 0 ? UsbTransferOutcome.Success : UsbTransferOutcome.FatalError
            });
            return transferred;
        }

        while (lenRemaining > 0)
        {
            int lenToSend = Math.Min(lenRemaining, maxLenToSend);
            int transferred;
            var errorCode = writer.Write(data, count, lenToSend, effectiveTimeoutMs, out transferred);

            if (errorCode != 0) // UsbError.Success is 0
            {
                UsbTrace.Log($"LibUsbDevice: Write error! errorCode: {errorCode}, transferred: {transferred}");
                lastError = (int)errorCode;
                outcome = UsbTransferOutcome.FatalError;
            }

            if (transferred <= 0)
            {
                UsbTrace.Log($"LibUsbDevice: Write returned non-positive transferred: {transferred}, errorCode: {errorCode}");
                if (outcome == UsbTransferOutcome.Success)
                {
                    outcome = UsbTransferOutcome.Timeout;
                    lastError = (int)errorCode;
                }
                break;
            }

            count += transferred;
            lenRemaining -= transferred;

            if (transferred < lenToSend)
            {
                UsbTrace.Log($"LibUsbDevice: Short write - transferred {transferred} < requested {lenToSend}");
                if (outcome == UsbTransferOutcome.Success)
                {
                    outcome = UsbTransferOutcome.ShortTransfer;
                }
                break;
            }
        }

        UsbTrace.Log($"LibUsbDevice: Write finished - total count: {count}");
        if (outcome == UsbTransferOutcome.Success && count > 0 && count < length)
        {
            outcome = UsbTransferOutcome.ShortTransfer;
        }

        UsbTrace.EmitTransfer(new UsbTransferEvent
        {
            Backend = "libusb",
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

        return count > 0 ? count : (length == 0 ? 0 : -1);
    }
}



