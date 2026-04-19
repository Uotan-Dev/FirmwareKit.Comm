using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using FirmwareKit.Comm.Usb.Diagnostics;

namespace FirmwareKit.Comm.Usb.Backend.libusbdotnet;

internal class LibUsbDevice : UsbDevice
{
    private const int DefaultTimeoutMs = 5000;

    private UsbContext? context;
    private IUsbDevice? usbDevice;
    private UsbEndpointReader? reader;
    private UsbEndpointWriter? writer;
    public ushort Vid { get; set; }
    public ushort Pid { get; set; }
    public byte BusNumber { get; set; }
    public byte DeviceAddress { get; set; }
    public byte InterfaceId { get; set; } = 0;

    private static string BuildDevicePath(LibUsbDotNet.LibUsb.UsbDevice device)
        => $"Bus {device.BusNumber} Device {device.Address}: {device.VendorId:X4}:{device.ProductId:X4}";

    private static bool HasFastbootInterface(LibUsbDotNet.LibUsb.UsbDevice device)
    {
        try
        {
            foreach (var config in device.Configs)
            {
                foreach (var ifc in config.Interfaces)
                {
                    bool isFastbootInterface = (int)ifc.Class == 0xff && (int)ifc.SubClass == 0x42 && (int)ifc.Protocol == 0x03;
                    if (!isFastbootInterface) continue;

                    bool hasIn = false;
                    bool hasOut = false;
                    foreach (var endpoint in ifc.Endpoints)
                    {
                        if ((endpoint.EndpointAddress & 0x80) != 0) hasIn = true;
                        else hasOut = true;
                    }

                    if (hasIn && hasOut) return true;
                }
            }
        }
        catch
        {
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
                HasFastbootInterface(d));
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
        catch { }

        byte targetInterfaceId = InterfaceId;
        byte inEndpoint = 0;
        byte outEndpoint = 0;

        foreach (var config in usbDevice.Configs)
        {
            foreach (var ifc in config.Interfaces)
            {
                bool isFastbootInterface = (int)ifc.Class == 0xff && (int)ifc.SubClass == 0x42 && (int)ifc.Protocol == 0x03;
                if (!isFastbootInterface) continue;

                byte candidateIn = 0;
                byte candidateOut = 0;
                foreach (var endpoint in ifc.Endpoints)
                {
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
            catch { }
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
            catch { }
        }
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
        if (reader == null) return 0;
        if (length <= 0) return 0;
        if (offset < 0 || length < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        int effectiveTimeoutMs = timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs;

        const int maxLenToRead = 1048576;
        int lenRemaining = length;
        int count = 0;

        while (lenRemaining > 0)
        {
            int lenToRead = Math.Min(lenRemaining, maxLenToRead);
            int read_len;

            reader.Read(buffer, offset + count, lenToRead, effectiveTimeoutMs, out read_len);

            if (read_len <= 0) break;

            count += read_len;
            lenRemaining -= read_len;

            if (read_len < lenToRead) break;
        }

        return count;
    }

    public override long Write(byte[] data, int length)
    {
        return Write(data, length, DefaultTimeoutMs);
    }

    public override long Write(byte[] data, int length, int timeoutMs)
    {
        if (writer == null)
        {
            UsbTrace.Log("LibUsbDevice: writer is null");
            return -1;
        }

        int effectiveTimeoutMs = timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs;

        const int maxLenToSend = 1048576;
        int lenRemaining = length;
        int count = 0;

        UsbTrace.Log($"LibUsbDevice: Write attempt - length: {length}, data: {BitConverter.ToString(data, 0, Math.Min(length, 16))}");

        if (length == 0)
        {
            int transferred;
            var errorCode = writer.Write(data, 0, 0, effectiveTimeoutMs, out transferred);
            UsbTrace.Log($"LibUsbDevice: Zero-length write - transferred: {transferred}, errorCode: {errorCode}");
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
            }

            if (transferred <= 0)
            {
                UsbTrace.Log($"LibUsbDevice: Write returned non-positive transferred: {transferred}, errorCode: {errorCode}");
                break;
            }

            count += transferred;
            lenRemaining -= transferred;

            if (transferred < lenToSend)
            {
                UsbTrace.Log($"LibUsbDevice: Short write - transferred {transferred} < requested {lenToSend}");
                break;
            }
        }

        UsbTrace.Log($"LibUsbDevice: Write finished - total count: {count}");
        return count > 0 ? count : (length == 0 ? 0 : -1);
    }
}



