using System.Diagnostics;
using System.Runtime.InteropServices;
using FirmwareKit.Comm.Abstractions;
using FirmwareKit.Comm.Diagnostics;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace FirmwareKit.Comm.Backend.LibUsb;

internal class LibUsbDevice : global::FirmwareKit.Comm.Backend.UsbDevice
{
    private const int PlatformDefaultTimeoutMs = UsbTransferPolicies.DefaultTimeoutMs;

    // Reusable chunk buffers avoid a per-chunk allocation during large transfers. The
    // session serializes same-direction transfers (read gate / write gate), and reads and
    // writes use separate buffers, so no additional locking is required.
    // <para>可复用的分块缓冲避免大传输时每次分块分配；会话串行化同向传输
    // （读/写门闩）且读/写缓冲分离，无需额外加锁。</para>
    private byte[]? _readChunkBuffer;
    private byte[]? _writeChunkBuffer;

    private UsbContext? context;
    private IUsbDevice? usbDevice;
    private UsbEndpointReader? reader;
    private UsbEndpointWriter? writer;
    // Vid/Pid delegate to the base UsbDevice.VendorId/ProductId so that both the
    // shorthand used by CreateHandle() and the projection in
    // UsbDeviceInfoFactory (which reads VendorId/ProductId) see the same values.
    // <para>Vid/Pid 委托到基类 UsbDevice.VendorId/ProductId，使 CreateHandle() 使用的简写
    // 与 UsbDeviceInfoFactory 投影读取的 VendorId/ProductId 保持同一份数据。</para>
    public ushort Vid { get => VendorId; set => VendorId = value; }
    public ushort Pid { get => ProductId; set => ProductId = value; }
    public byte BusNumber { get; set; }
    public byte DeviceAddress { get; set; }
    public byte InterfaceId { get; set; } = 0;
    public byte ReadEndpointId { get; set; }
    public byte WriteEndpointId { get; set; }

    public override byte EndpointIn => ReadEndpointId;
    public override byte EndpointOut => WriteEndpointId;

    /// <inheritdoc/>
    internal override bool IsHandleOpen => usbDevice != null;

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

        // UsbContext.List() returns a UsbDeviceCollection that MUST be disposed: the
        // collection owns the native device handles, and leaving it undisposed lets the
        // devices' SafeHandle finalizers run against memory already freed by
        // libusb_exit, crashing with 0xC0000005 in UnrefDevice. Clone the matched
        // device (as the LibUsbDotNet docs recommend) so the session keeps a valid
        // handle after the collection is disposed.
        // <para>UsbContext.List() 返回的 UsbDeviceCollection 必须释放：集合持有原生设备
        // 句柄，不释放会导致设备 SafeHandle 终结器对 libusb_exit 已释放的内存执行 unref，
        // 在 UnrefDevice 处以 0xC0000005 崩溃。按 LibUsbDotNet 文档建议 Clone 匹配到的
        // 设备，使会话在集合释放后仍持有有效句柄。</para>
        LibUsbDotNet.LibUsb.UsbDevice? device;
        using (var candidates = context.List())
        {
            var candidateList = candidates.OfType<LibUsbDotNet.LibUsb.UsbDevice>().ToList();

            device = null;
            if (BusNumber != 0 || DeviceAddress != 0)
            {
                device = candidateList.FirstOrDefault(d => d.BusNumber == BusNumber && d.Address == DeviceAddress);
            }

            if (device == null && !string.IsNullOrWhiteSpace(DevicePath))
            {
                device = candidateList.FirstOrDefault(d =>
                    string.Equals(BuildDevicePath(d), DevicePath, StringComparison.OrdinalIgnoreCase));
            }

            if (device == null)
            {
                device = candidateList.FirstOrDefault(d =>
                    d.VendorId == Vid &&
                    d.ProductId == Pid &&
                    HasBulkInterface(d));
            }

            if (device != null)
            {
                usbDevice = device.Clone();
            }
        }

        if (device == null || usbDevice == null)
        {
            context.Dispose();
            context = null;
            return -1;
        }

        try
        {
            usbDevice.Open();
        }
        catch
        {
            // Open() failed (e.g. the interface is claimed by another session or process).
            // Never Close() a device that was never opened: LibUsbDotNet's Close() on an
            // unopened device corrupts the native refcount and the device's finalizer
            // later crashes with 0xC0000005 in UnrefDevice. Dispose() the clone (releases
            // the SafeHandle) and release only the context.
            // <para>Open() 失败（例如接口已被其他会话或进程声明）。绝不对从未成功打开的设备
            // 调用 Close()：LibUsbDotNet 对未打开设备调用 Close() 会破坏原生引用计数，
            // 设备终结器随后在 UnrefDevice 处以 0xC0000005 崩溃。对克隆调用 Dispose()
            // （释放 SafeHandle），并仅释放上下文。</para>
            (usbDevice as IDisposable)?.Dispose();
            usbDevice = null;
            Dispose();
            return -1;
        }

        try
        {
            usbDevice.SetConfiguration(1);
        }
        catch (Exception ex)
        {
            UsbTrace.LogFormatted($"LibUsbDevice.SetConfiguration ignored: {ex.GetType().Name}: {ex.Message}");
        }

        byte targetInterfaceId = InterfaceId;
        byte inEndpoint = ReadEndpointId;
        byte outEndpoint = WriteEndpointId;

        // Auto-discover a bulk IN/OUT pair only when the finder did not already bind both.
        // IN-only (HID interrupt) and OUT-only devices must NOT be rejected here: the
        // session supports single-direction binding, and ReadInterrupt/WriteInterrupt
        // target explicit endpoints regardless of the bulk pair.
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
                        // Collect bulk endpoints first (the session I/O path), then interrupt
                        // endpoints as fallback candidates so devices with only interrupt pipes
                        // (e.g. HID) can still be opened when the filter requests explicit
                        // endpoint addresses (interrupt-test).
                        int epType = endpoint.Attributes & 0x03;
                        if (epType != 0x02 && epType != 0x03) continue;

                        if ((endpoint.EndpointAddress & 0x80) != 0)
                        {
                            if (candidateIn == 0) candidateIn = endpoint.EndpointAddress;
                        }
                        else
                        {
                            if (candidateOut == 0) candidateOut = endpoint.EndpointAddress;
                        }
                    }

                    if (candidateIn != 0)
                    {
                        targetInterfaceId = (byte)ifc.Number;
                        if (inEndpoint == 0) inEndpoint = candidateIn;
                        if (outEndpoint == 0) outEndpoint = candidateOut;
                        break;
                    }
                }

                if (inEndpoint != 0 && outEndpoint != 0) break;
            }
        }

        // Only reject when the session would have NO endpoint at all; single-direction
        // sessions are valid (the missing direction's bulk helpers throw NotSupportedException).
        if (inEndpoint == 0 && outEndpoint == 0)
        {
            Dispose();
            return -1;
        }

        InterfaceId = targetInterfaceId;

        // usbhid (and other kernel drivers) bind HID interrupt devices immediately, and
        // udev may rebind right after a detach; retry detach+claim a few times instead of
        // failing on the first contention (mirrors libusb's auto-detach behaviour).
        Exception? claimError = null;
        bool claimed = false;
        try
        {
            usbDevice.ClaimInterface(targetInterfaceId);
            claimed = true;
        }
        catch (Exception ex)
        {
            claimError = ex;
        }

        if (!claimed && usbDevice is LibUsbDotNet.LibUsb.UsbDevice libusbDevice)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    libusbDevice.DetachKernelDriver(targetInterfaceId);
                }
                catch (Exception detachEx)
                {
                    claimError = detachEx;
                }

                try
                {
                    usbDevice.ClaimInterface(targetInterfaceId);
                    claimed = true;
                    break;
                }
                catch (Exception ex)
                {
                    claimError = ex;
                    Thread.Sleep(50);
                }
            }
        }

        if (!claimed)
        {
            UsbTrace.LogFormatted($"LibUsbDevice.ClaimInterface failed: {claimError?.GetType().Name}: {claimError?.Message}");
        }

        reader = null;
        writer = null;

        // Open each direction independently: interrupt-only HID devices (e.g. QEMU
        // usb-tablet) expose no OUT pipe, so a missing direction must not fail the
        // session — the session's ReadInterrupt/WriteInterrupt target explicit endpoints.
        if (inEndpoint != 0)
        {
            reader = usbDevice.OpenEndpointReader((ReadEndpointID)inEndpoint);
        }

        if (outEndpoint != 0)
        {
            writer = usbDevice.OpenEndpointWriter((WriteEndpointID)outEndpoint);
        }

        reader?.ReadFlush();

        // A session is usable as long as every direction it was asked to bind actually
        // opened. IN-only (HID interrupt) and OUT-only devices are valid; the missing
        // direction's bulk helpers throw NotSupportedException when invoked.
        if ((inEndpoint != 0 && reader == null) || (outEndpoint != 0 && writer == null))
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
            // Close() releases the device handle; Dispose() additionally releases the
            // underlying Device SafeHandle. Calling only Close() leaves the SafeHandle
            // to the finalizer, which then UnrefDevices memory already freed by
            // libusb_exit -> 0xC0000005.
            // <para>Close() 释放设备句柄；Dispose() 额外释放底层 Device SafeHandle。
            // 仅调用 Close() 会把 SafeHandle 留给终结器，终结器会对 libusb_exit 已释放的
            // 内存执行 UnrefDevice，导致 0xC0000005。</para>
            usbDevice.Close();
            (usbDevice as IDisposable)?.Dispose();
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
                UsbTrace.LogFormatted($"LibUsbDevice.Reset failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public override int ControlTransfer(FirmwareKit.Comm.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        if (usbDevice == null)
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

    protected override string BackendName => "libusb";

    // A session is open when at least one direction bound successfully; IN-only (HID
    // interrupt) and OUT-only devices are valid sessions even though bulk I/O in the
    // missing direction throws NotSupportedException.
    protected override bool IsOpen => reader != null || writer != null;

    protected override bool IsDisconnectionError(int nativeError)
        => nativeError == (int)Error.NoDevice;

    /// <summary>
    /// Maps the library timeout sentinel to libusb's convention: libusb treats 0 as
    /// "never time out", so <see cref="UsbTransferPolicies.InfiniteTimeoutMs"/> (-1) becomes 0.
    /// <para>将库的超时哨兵映射为 libusb 约定：libusb 将 0 视为"永不超时"，
    /// 因此 <see cref="UsbTransferPolicies.InfiniteTimeoutMs"/>（-1）转换为 0。</para>
    /// </summary>
    private static int ToLibusbTimeout(int timeoutMs)
        => timeoutMs == UsbTransferPolicies.InfiniteTimeoutMs ? 0 : timeoutMs;

    protected override UsbChunkResult ReadChunk(IntPtr buffer, int length, int timeoutMs)
    {
        if (reader == null)
        {
            // IN-only session bound without an OUT pipe is valid for interrupt reads;
            // bulk reads in a direction that never opened must not NRE.
            throw new NotSupportedException("The session has no bound IN endpoint for bulk reads (interrupt-only device?).");
        }

        if (_readChunkBuffer == null || _readChunkBuffer.Length < length)
        {
            _readChunkBuffer = new byte[length];
        }

        byte[] chunkBuffer = _readChunkBuffer;
        Error error = reader.Read(chunkBuffer, 0, length, ToLibusbTimeout(timeoutMs), out int readLen);
        if (error == Error.NoDevice)
        {
            return UsbChunkResult.Fatal((int)error);
        }
        if (readLen > 0)
        {
            Marshal.Copy(chunkBuffer, 0, buffer, readLen);
        }
        return readLen <= 0 ? UsbChunkResult.Timeout((int)error) : UsbChunkResult.Success(readLen);
    }

    protected override UsbChunkResult WriteChunk(IntPtr buffer, int length, int timeoutMs)
    {
        if (writer == null)
        {
            // OUT-only session bound without an IN pipe is valid for interrupt writes;
            // bulk writes in a direction that never opened must not NRE.
            throw new NotSupportedException("The session has no bound OUT endpoint for bulk writes (interrupt-only device?).");
        }

        if (_writeChunkBuffer == null || _writeChunkBuffer.Length < length)
        {
            _writeChunkBuffer = new byte[length];
        }

        byte[] chunkBuffer = _writeChunkBuffer;
        Marshal.Copy(buffer, chunkBuffer, 0, length);

        int transferred;
        Error errorCode = writer!.Write(chunkBuffer, 0, length, ToLibusbTimeout(timeoutMs), out transferred);
        if (errorCode == Error.NoDevice)
        {
            return UsbChunkResult.Fatal((int)errorCode);
        }
        if (errorCode != 0) // Error.Success is 0; libusb write errors are reported without throwing.
        {
            return UsbChunkResult.Error((int)errorCode);
        }
        if (transferred <= 0)
        {
            return UsbChunkResult.Timeout((int)errorCode);
        }
        return UsbChunkResult.Success(transferred);
    }

    public override long Write(byte[] data, int length)
    {
        return Write(data, length, PlatformDefaultTimeoutMs);
    }

    public override void WriteZlp(int timeoutMs)
    {
        if (writer == null)
        {
            throw new NotSupportedException("The session has no bound OUT endpoint for bulk writes (interrupt-only device?).");
        }

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
        byte[] zero = Array.Empty<byte>();
        Error errorCode = writer.Write(zero, 0, 0, ToLibusbTimeout(effectiveTimeoutMs), out int transferred);
        if (errorCode == Error.NoDevice)
        {
            throw new UsbDeviceDisconnectedException("USB write failed: device disconnected (Error.NoDevice).", (int)errorCode);
        }
        if (errorCode != 0)
        {
            throw new IOException($"USB zero-length write failed with libusb error: {errorCode}");
        }
    }

    public override UsbReadResult ReadInterrupt(byte endpointAddress, byte[] buffer, int offset, int length, int timeoutMs)
    {
        if (usbDevice == null)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }
        if (length <= 0) return new UsbReadResult(0, false, false);
        ValidateBufferRange(buffer, offset, length);

        // libusb's bulk transfer API is also valid for interrupt endpoints; open a
        // temporary reader on the requested endpoint for the single transfer.
        // (LibUsbDotNet 3.x endpoint objects do not implement IDisposable; they are
        // reclaimed with the owning UsbDevice, matching the session reader/writer fields.)
        var interruptReader = usbDevice.OpenEndpointReader((ReadEndpointID)endpointAddress);
        if (interruptReader == null)
        {
            return new UsbReadResult(0, isTimeout: true, isShortPacket: false);
        }
        Error error = interruptReader.Read(buffer, offset, length, ToLibusbTimeout(timeoutMs), out int readLen);
        if (error == Error.NoDevice)
        {
            throw new UsbDeviceDisconnectedException("USB interrupt read failed: device disconnected (Error.NoDevice).", (int)error);
        }
        if (readLen <= 0)
        {
            return new UsbReadResult(0, isTimeout: true, isShortPacket: false);
        }
        return new UsbReadResult(readLen, isTimeout: false, isShortPacket: readLen < length);
    }

    public override long WriteInterrupt(byte endpointAddress, byte[] data, int offset, int length, int timeoutMs)
    {
        if (usbDevice == null)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }
        ValidateWriteData(data, offset, length);
        if (length == 0) return 0;

        var interruptWriter = usbDevice.OpenEndpointWriter((WriteEndpointID)endpointAddress);
        if (interruptWriter == null)
        {
            return 0;
        }
        Error error = interruptWriter.Write(data, offset, length, ToLibusbTimeout(timeoutMs), out int transferred);
        if (error == Error.NoDevice)
        {
            throw new UsbDeviceDisconnectedException("USB interrupt write failed: device disconnected (Error.NoDevice).", (int)error);
        }
        return transferred <= 0 ? 0 : transferred;
    }

    public override async Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (length <= 0)
        {
            return Array.Empty<byte>();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var buffer = new byte[length]
        ;
        int count = await ReadIntoAsync(buffer, 0, length, timeoutMs, cancellationToken).ConfigureAwait(false);
        if (count <= 0)
        {
            return Array.Empty<byte>();
        }

        if (count == length)
        {
            return buffer;
        }

        var result = new byte[count];
        Buffer.BlockCopy(buffer, 0, result, 0, count);
        return result;
    }

    public override async Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        int? lastError = null;
        var outcome = UsbTransferOutcome.Success;

        if (reader == null)
        {
            return 0;
        }

        if (length <= 0)
        {
            return 0;
        }

        ValidateBufferRange(buffer, offset, length);
        cancellationToken.ThrowIfCancellationRequested();

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
        const int maxLenToRead = UsbTransferPolicies.MaxChunkSize;
        int lenRemaining = length;
        int count = 0;

        while (lenRemaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int lenToRead = Math.Min(lenRemaining, maxLenToRead);
            var (errorCode, read_len) = await reader.ReadAsync(buffer, offset + count, lenToRead, ToLibusbTimeout(effectiveTimeoutMs)).ConfigureAwait(false);

            if (errorCode == Error.NoDevice)
            {
                throw new UsbDeviceDisconnectedException("USB read failed: device disconnected (Error.NoDevice).", (int)errorCode);
            }

            if (errorCode != 0)
            {
                lastError = (int)errorCode;
                outcome = UsbTransferOutcome.FatalError;
            }

            if (read_len <= 0)
            {
                if (outcome == UsbTransferOutcome.Success)
                {
                    outcome = UsbTransferOutcome.Timeout;
                }

                break;
            }

            count += read_len;
            lenRemaining -= read_len;

            if (read_len < lenToRead)
            {
                if (outcome == UsbTransferOutcome.Success)
                {
                    outcome = UsbTransferOutcome.ShortTransfer;
                }

                break;
            }
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

    public override async Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
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
            return 0;
        }

        ValidateWriteData(data, length);
        cancellationToken.ThrowIfCancellationRequested();

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, PlatformDefaultTimeoutMs);
        const int maxLenToSend = UsbTransferPolicies.MaxChunkSize;
        int lenRemaining = length;
        int count = 0;

        UsbTrace.LogFormatted($"LibUsbDevice: Write attempt - length: {length}");

        if (length == 0)
        {
            var (errorCode, transferred) = await writer.WriteAsync(data, 0, 0, ToLibusbTimeout(effectiveTimeoutMs)).ConfigureAwait(false);
            UsbTrace.LogFormatted($"LibUsbDevice: Zero-length write - transferred: {transferred}, errorCode: {errorCode}");
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
            cancellationToken.ThrowIfCancellationRequested();

            int lenToSend = Math.Min(lenRemaining, maxLenToSend);
            var (errorCode, transferred) = await writer.WriteAsync(data, count, lenToSend, ToLibusbTimeout(effectiveTimeoutMs)).ConfigureAwait(false);

            if (errorCode == Error.NoDevice)
            {
                throw new UsbDeviceDisconnectedException("USB write failed: device disconnected (Error.NoDevice).", (int)errorCode);
            }

            if (errorCode != 0)
            {
                UsbTrace.LogFormatted($"LibUsbDevice: Write error! errorCode: {errorCode}, transferred: {transferred}");
                lastError = (int)errorCode;
                outcome = UsbTransferOutcome.FatalError;
            }

            if (transferred <= 0)
            {
                UsbTrace.LogFormatted($"LibUsbDevice: Write returned non-positive transferred: {transferred}, errorCode: {errorCode}");
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
                UsbTrace.LogFormatted($"LibUsbDevice: Short write - transferred {transferred} < requested {lenToSend}");
                if (outcome == UsbTransferOutcome.Success)
                {
                    outcome = UsbTransferOutcome.ShortTransfer;
                }

                break;
            }
        }

        UsbTrace.LogFormatted($"LibUsbDevice: Write finished - total count: {count}");
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

        // Match the sync Write contract: report transferred bytes, 0 on failure.
        // (-1 was previously returned on failure and could be misread as a huge count.)
        return count;
    }

    public override async Task<int> ControlTransferAsync(FirmwareKit.Comm.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (usbDevice == null)
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

        var libUsbSetup = new LibUsbDotNet.Main.UsbSetupPacket(setupPacket.RequestType, setupPacket.Request, setupPacket.Value, setupPacket.Index, setupPacket.Length);

        if (length == 0)
        {
            return await usbDevice.ControlTransferAsync(libUsbSetup).ConfigureAwait(false);
        }

        if (timeoutMs > 0 && timeoutMs != PlatformDefaultTimeoutMs)
        {
            return await Task.Run(() => ControlTransfer(setupPacket, buffer, offset, length, timeoutMs), cancellationToken).ConfigureAwait(false);
        }

        if (buffer == null)
        {
            return await usbDevice.ControlTransferAsync(libUsbSetup).ConfigureAwait(false);
        }

        if (offset == 0 && length == buffer.Length)
        {
            return await usbDevice.ControlTransferAsync(libUsbSetup, buffer, 0, length).ConfigureAwait(false);
        }

        var transferBuffer = new byte[length];
        bool isInDirection = (setupPacket.RequestType & 0x80) != 0;
        if (!isInDirection)
        {
            Buffer.BlockCopy(buffer, offset, transferBuffer, 0, length);
        }

        int transferred = await usbDevice.ControlTransferAsync(libUsbSetup, transferBuffer, 0, length).ConfigureAwait(false);
        if (isInDirection)
        {
            Buffer.BlockCopy(transferBuffer, 0, buffer, offset, Math.Min(transferred, length));
        }

        return transferred;
    }
}



