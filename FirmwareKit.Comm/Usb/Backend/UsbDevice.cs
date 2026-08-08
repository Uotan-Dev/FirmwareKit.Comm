using System.Diagnostics;
using System.Runtime.InteropServices;
using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;

namespace FirmwareKit.Comm.Usb.Backend;

/// <summary>
/// Represents an abstract USB device that provides read, write, control transfer, and lifecycle operations.
/// <para>表示一个抽象 USB 设备，提供读取、写入、控制传输及生命周期操作。</para>
/// </summary>
internal abstract class UsbDevice : IDisposable
{
    /// <summary>
    /// Gets the default timeout in milliseconds for this device.
    /// <para>获取此设备的默认超时时间（毫秒）。</para>
    /// </summary>
    public virtual int DefaultTimeoutMs => UsbTransferPolicies.DefaultTimeoutMs;

    /// <summary>
    /// Gets or sets the device path used to open the handle.
    /// <para>获取或设置用于打开句柄的设备路径。</para>
    /// </summary>
    public string DevicePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serial number of the device.
    /// <para>获取或设置设备的序列号。</para>
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Gets or sets the USB vendor identifier.
    /// <para>获取或设置 USB 厂商标识。</para>
    /// </summary>
    public ushort VendorId { get; set; }

    /// <summary>
    /// Gets or sets the USB product identifier.
    /// <para>获取或设置 USB 产品标识。</para>
    /// </summary>
    public ushort ProductId { get; set; }

    /// <summary>
    /// Gets or sets the interface class code.
    /// <para>获取或设置接口类代码。</para>
    /// </summary>
    public byte? InterfaceClass { get; set; }

    /// <summary>
    /// Gets or sets the interface subclass code.
    /// <para>获取或设置接口子类代码。</para>
    /// </summary>
    public byte? InterfaceSubClass { get; set; }

    /// <summary>
    /// Gets or sets the interface protocol code.
    /// <para>获取或设置接口协议代码。</para>
    /// </summary>
    public byte? InterfaceProtocol { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether interface metadata has been observed from the device descriptor.
    /// <para>获取或设置一个值，指示是否已从设备描述符中观测到接口元数据。</para>
    /// </summary>
    public bool InterfaceMetadataObserved { get; set; }

    /// <summary>
    /// Gets or sets the USB transfer speed reported by the backend, when available.
    /// <para>获取或设置后端报告的 USB 传输速度（若可用）。</para>
    /// </summary>
    public UsbDeviceSpeed Speed { get; set; }

    /// <summary>
    /// Gets or sets the interfaces (and their endpoints) observed for this device.
    /// <para>获取或设置观测到的设备接口（及其端点）列表。</para>
    /// </summary>
    public IReadOnlyList<UsbInterfaceInfo> Interfaces { get; set; } = Array.Empty<UsbInterfaceInfo>();

    /// <summary>
    /// Gets or sets the platform-specific USB device type.
    /// <para>获取或设置平台特定的 USB 设备类型。</para>
    /// </summary>
    public UsbDeviceType UsbDeviceType { get; set; }

    /// <summary>
    /// Gets the backend tag used in transfer trace events.
    /// <para>获取传输跟踪事件中使用的后端标签。</para>
    /// </summary>
    protected abstract string BackendName { get; }

    /// <summary>
    /// Gets the maximum payload size for a single chunk transfer.
    /// <para>获取单次分块传输的最大载荷大小。</para>
    /// </summary>
    protected virtual int MaxChunkSize => UsbTransferPolicies.MaxChunkSize;

    /// <summary>
    /// Gets a value indicating whether the device handle is open and ready for I/O.
    /// <para>获取一个值，指示设备句柄是否已打开并可用于 I/O。</para>
    /// </summary>
    protected abstract bool IsOpen { get; }

    /// <summary>
    /// Performs a single chunked bulk read.
    /// <para>执行单次分块批量读取。</para>
    /// </summary>
    /// <param name="buffer">The pinned target buffer pointer. <para>已固定的目标缓冲区指针。</para></param>
    /// <param name="length">The number of bytes to read. <para>要读取的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The chunk transfer result. <para>分块传输结果。</para></returns>
    protected abstract UsbChunkResult ReadChunk(IntPtr buffer, int length, int timeoutMs);

    /// <summary>
    /// Performs a single chunked bulk write.
    /// <para>执行单次分块批量写入。</para>
    /// </summary>
    /// <param name="buffer">The pinned source buffer pointer. <para>已固定的源缓冲区指针。</para></param>
    /// <param name="length">The number of bytes to write. <para>要写入的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The chunk transfer result. <para>分块传输结果。</para></returns>
    protected abstract UsbChunkResult WriteChunk(IntPtr buffer, int length, int timeoutMs);

    /// <summary>
    /// Creates the exception thrown when a read fails with a fatal native error.
    /// <para>创建在读取遇到致命原生错误时抛出的异常。</para>
    /// </summary>
    protected virtual Exception CreateReadFatalException(int nativeError)
    {
        if (IsDisconnectionError(nativeError))
        {
            return new UsbDeviceDisconnectedException($"USB device disconnected during read (native error: 0x{nativeError:X}).", nativeError);
        }
        return new IOException($"USB read failed with fatal error: 0x{nativeError:X}");
    }

    /// <summary>
    /// Creates the exception thrown when a write fails with a fatal native error.
    /// <para>创建在写入遇到致命原生错误时抛出的异常。</para>
    /// </summary>
    protected virtual Exception CreateWriteFatalException(int nativeError)
    {
        if (IsDisconnectionError(nativeError))
        {
            return new UsbDeviceDisconnectedException($"USB device disconnected during write (native error: 0x{nativeError:X}).", nativeError);
        }
        return new IOException($"USB write failed with fatal error: 0x{nativeError:X}");
    }

    /// <summary>
    /// Determines whether a native error code indicates the device was unplugged/disconnected.
    /// <para>判断给定原生错误码是否表示设备已拔出/断开。</para>
    /// Backends override this with their platform-specific disconnection codes so the shared
    /// read/write loops throw <see cref="UsbDeviceDisconnectedException"/> instead of a generic
    /// <see cref="IOException"/>, letting upper-layer protocols trigger re-enumeration.
    /// <para>后端以各自平台的断开错误码覆盖此方法，使共享读写循环抛出
    /// <see cref="UsbDeviceDisconnectedException"/> 而非通用 <see cref="IOException"/>，
    /// 便于上层协议触发重新枚举。</para>
    /// </summary>
    /// <param name="nativeError">The native error code. <para>原生错误码。</para></param>
    /// <returns><c>true</c> when the code indicates a disconnection. <para>该错误码表示断开时返回 <c>true</c>。</para></returns>
    protected virtual bool IsDisconnectionError(int nativeError) => false;

    /// <summary>
    /// Describes the outcome of a single chunk transfer.
    /// <para>描述单次分块传输的结果。</para>
    /// </summary>
    protected readonly struct UsbChunkResult
    {
        public UsbChunkResult(UsbChunkStatus status, int transferred, int nativeError = 0, int retryCount = 0)
        {
            Status = status;
            Transferred = transferred;
            NativeError = nativeError;
            RetryCount = retryCount;
        }

        public UsbChunkStatus Status { get; }
        public int Transferred { get; }
        public int NativeError { get; }
        public int RetryCount { get; }

        public static UsbChunkResult Success(int transferred) => new(UsbChunkStatus.Success, transferred);
        public static UsbChunkResult Timeout(int nativeError) => new(UsbChunkStatus.Timeout, 0, nativeError);
        public static UsbChunkResult Fatal(int nativeError) => new(UsbChunkStatus.FatalError, 0, nativeError);
        public static UsbChunkResult Error(int nativeError) => new(UsbChunkStatus.Error, 0, nativeError);
    }

    /// <summary>
    /// Classifies a chunk transfer outcome.
    /// <para>对分块传输结果进行分类。</para>
    /// </summary>
    protected enum UsbChunkStatus
    {
        Success,
        Timeout,
        FatalError,
        Error
    }

    /// <summary>
    /// Reads data from the device using the default timeout.
    /// <para>使用默认超时时间从设备读取数据。</para>
    /// </summary>
    /// <param name="length">The number of bytes to read. <para>要读取的字节数。</para></param>
    /// <returns>The received data. <para>接收到的数据。</para></returns>
    public abstract byte[] Read(int length);

    /// <summary>
    /// Reads data from the device with a specified timeout.
    /// <para>使用指定超时时间从设备读取数据。</para>
    /// </summary>
    /// <param name="length">The number of bytes to read. <para>要读取的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The received data. <para>接收到的数据。</para></returns>
    public virtual byte[] Read(int length, int timeoutMs) => Read(length);

    /// <summary>
    /// Performs a USB control transfer.
    /// <para>执行 USB 控制传输。</para>
    /// </summary>
    /// <param name="setupPacket">The setup packet for the control request. <para>控制请求的 Setup 包。</para></param>
    /// <param name="buffer">The data buffer, or <c>null</c> for zero-length transfers. <para>数据缓冲区；零长度传输时为 <c>null</c>。</para></param>
    /// <param name="offset">The byte offset within the buffer. <para>缓冲区内的字节偏移量。</para></param>
    /// <param name="length">The number of bytes to transfer. <para>要传输的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The number of bytes transferred. <para>已传输的字节数。</para></returns>
    /// <exception cref="NotSupportedException">Thrown when the device does not support control transfers. <para>当设备不支持控制传输时抛出。</para></exception>
    public virtual int ControlTransfer(FirmwareKit.Comm.Usb.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs)
    {
        throw new NotSupportedException($"{GetType().Name} does not support control transfers.");
    }

    /// <summary>
    /// Validates that the specified buffer range is within bounds.
    /// <para>验证指定的缓冲区范围是否在边界内。</para>
    /// </summary>
    /// <param name="buffer">The buffer to validate. <para>要验证的缓冲区。</para></param>
    /// <param name="offset">The byte offset within the buffer. <para>缓冲区内的字节偏移量。</para></param>
    /// <param name="length">The number of bytes. <para>字节数。</para></param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is <c>null</c>. <para>当 <paramref name="buffer"/> 为 <c>null</c> 时抛出。</para></exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> or <paramref name="length"/> is out of range. <para>当 <paramref name="offset"/> 或 <paramref name="length"/> 超出范围时抛出。</para></exception>
    internal static void ValidateBufferRange(byte[] buffer, int offset, int length)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length < 0 || length > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }

    /// <summary>
    /// Validates that the write data buffer is not null and the length is within bounds.
    /// <para>验证写入数据缓冲区不为 null 且长度在边界内。</para>
    /// </summary>
    /// <param name="data">The data buffer to validate. <para>要验证的数据缓冲区。</para></param>
    /// <param name="length">The number of bytes to write. <para>要写入的字节数。</para></param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is <c>null</c>. <para>当 <paramref name="data"/> 为 <c>null</c> 时抛出。</para></exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is out of range. <para>当 <paramref name="length"/> 超出范围时抛出。</para></exception>
    internal static void ValidateWriteData(byte[] data, int length)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (length < 0 || length > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }

    /// <summary>
    /// Reads data from the device directly into the specified buffer using the default timeout.
    /// <para>使用默认超时时间将数据直接读入指定的缓冲区。</para>
    /// </summary>
    /// <param name="buffer">The target buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The byte offset within the buffer. <para>缓冲区内的字节偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>要读取的字节数。</para></param>
    /// <returns>The number of bytes actually read. <para>实际读取的字节数。</para></returns>
    public virtual int ReadInto(byte[] buffer, int offset, int length)
    {
        if (length <= 0) return 0;
        ValidateBufferRange(buffer, offset, length);

        byte[] data = Read(length);
        if (data.Length == 0) return 0;
        Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
        return data.Length;
    }

    /// <summary>
    /// Snapshots the transferred payload bytes when opt-in frame capture is enabled.
    /// <para>开启可选抓帧时对已传输载荷字节做快照。</para>
    /// </summary>
    private static byte[]? CapturePayload(byte[] buffer, int offset, int count)
    {
        if (!UsbTrace.CaptureFrames || count <= 0) return null;
        int cap = Math.Min(count, UsbTrace.MaxCaptureBytes);
        var frame = new byte[cap];
        Buffer.BlockCopy(buffer, offset, frame, 0, cap);
        return frame;
    }

    /// <summary>
    /// Emits a transfer trace event using the backend tag.
    /// <para>使用后端标签发出传输跟踪事件。</para>
    /// </summary>
    private void EmitTransfer(UsbTransferOperation operation, int requestedBytes, int transferredBytes, int timeoutMs, int retryCount, int? nativeErrorCode, long elapsedMs, UsbTransferOutcome outcome, byte[]? payload = null)
    {
        UsbTrace.EmitTransfer(new UsbTransferEvent
        {
            Backend = BackendName,
            DevicePath = DevicePath,
            Operation = operation,
            RequestedBytes = requestedBytes,
            TransferredBytes = transferredBytes,
            TimeoutMs = timeoutMs,
            RetryCount = retryCount,
            NativeErrorCode = nativeErrorCode,
            ElapsedMs = elapsedMs,
            Outcome = outcome,
            Payload = payload
        });
    }

    /// <summary>
    /// Reads data from the device directly into the specified buffer with a specified timeout.
    /// <para>使用指定超时时间将数据直接读入指定的缓冲区。</para>
    /// <b>Timeout semantics:</b> the timeout applies per chunk (see <see cref="MaxChunkSize"/>),
    /// not to the whole operation - a large read spanning several chunks can take up to
    /// chunkCount × timeoutMs. Use <c>ReadExact</c> when a total-budget read is needed.
    /// <para><b>超时语义：</b>超时作用于每块（见 <see cref="MaxChunkSize"/>）而非整个操作——
    /// 跨多块的大读取最多耗时 块数 × timeoutMs。需要整体预算读取时使用 <c>ReadExact</c>。</para>
    /// </summary>
    /// <param name="buffer">The target buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The byte offset within the buffer. <para>缓冲区内的字节偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>要读取的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The number of bytes actually read. <para>实际读取的字节数。</para></returns>
    public virtual int ReadInto(byte[] buffer, int offset, int length, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        int? lastError = null;
        var outcome = UsbTransferOutcome.Success;

        if (!IsOpen)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }
        if (length <= 0) return 0;
        ValidateBufferRange(buffer, offset, length);

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, DefaultTimeoutMs);
        int lenRemaining = length;
        int count = 0;
        int retryCount = 0;

        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            while (lenRemaining > 0)
            {
                int lenToRead = Math.Min(lenRemaining, MaxChunkSize);
                IntPtr ptr = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + offset + count);

                var chunk = ReadChunk(ptr, lenToRead, effectiveTimeoutMs);
                retryCount += chunk.RetryCount;

                if (chunk.Status == UsbChunkStatus.Timeout)
                {
                    lastError = chunk.NativeError;
                    outcome = UsbTransferOutcome.Timeout;
                    break;
                }

                if (chunk.Status == UsbChunkStatus.FatalError)
                {
                    lastError = chunk.NativeError;
                    EmitTransfer(UsbTransferOperation.Read, length, count, effectiveTimeoutMs, retryCount, lastError, stopwatch.ElapsedMilliseconds, UsbTransferOutcome.FatalError, CapturePayload(buffer, offset, count));
                    throw CreateReadFatalException(chunk.NativeError);
                }

                if (chunk.Status == UsbChunkStatus.Error)
                {
                    lastError = chunk.NativeError;
                    outcome = UsbTransferOutcome.Error;
                    break;
                }

                int transferred = chunk.Transferred;
                count += transferred;
                lenRemaining -= transferred;

                if (transferred < lenToRead) break;
            }
        }
        finally
        {
            handle.Free();
        }

        if (outcome == UsbTransferOutcome.Success && count > 0 && count < length)
        {
            outcome = UsbTransferOutcome.ShortTransfer;
        }

        EmitTransfer(UsbTransferOperation.Read, length, count, effectiveTimeoutMs, retryCount, lastError, stopwatch.ElapsedMilliseconds, outcome, CapturePayload(buffer, offset, count));
        return count;
    }

    /// <summary>
    /// Asynchronously reads data from the device.
    /// <para>异步从设备读取数据。</para>
    /// </summary>
    /// <param name="length">The number of bytes to read. <para>要读取的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. <para>用于监视取消请求的令牌。</para></param>
    /// <returns>A task that represents the asynchronous read operation. <para>表示异步读取操作的任务。</para></returns>
    public virtual Task<byte[]> ReadAsync(int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        return FirmwareKit.Comm.Usb.Abstractions.UsbAsyncExecution.Run(() => Read(length, timeoutMs), cancellationToken);
    }

    /// <summary>
    /// Asynchronously reads data directly into the specified buffer.
    /// <para>异步将数据直接读入指定的缓冲区。</para>
    /// </summary>
    /// <param name="buffer">The target buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The byte offset within the buffer. <para>缓冲区内的字节偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>要读取的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. <para>用于监视取消请求的令牌。</para></param>
    /// <returns>A task that represents the asynchronous read operation with the number of bytes read. <para>表示异步读取操作并返回已读取字节数的任务。</para></returns>
    public virtual Task<int> ReadIntoAsync(byte[] buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        return FirmwareKit.Comm.Usb.Abstractions.UsbAsyncExecution.Run(() => ReadInto(buffer, offset, length, timeoutMs), cancellationToken);
    }

    /// <summary>
    /// Writes data to the device using the default timeout.
    /// <para>使用默认超时时间向设备写入数据。</para>
    /// </summary>
    /// <param name="data">The data to write. <para>要写入的数据。</para></param>
    /// <param name="length">The number of bytes to write. <para>要写入的字节数。</para></param>
    /// <returns>The number of bytes actually written. <para>实际写入的字节数。</para></returns>
    public abstract long Write(byte[] data, int length);

    /// <summary>
    /// Writes data to the device with a specified timeout.
    /// <para>使用指定超时时间向设备写入数据。</para>
    /// <b>Timeout semantics:</b> the timeout applies per chunk (see <see cref="MaxChunkSize"/>),
    /// not to the whole operation - a large write spanning several chunks can take up to
    /// chunkCount × timeoutMs.
    /// <para><b>超时语义：</b>超时作用于每块（见 <see cref="MaxChunkSize"/>）而非整个操作——
    /// 跨多块的大写入最多耗时 块数 × timeoutMs。</para>
    /// </summary>
    /// <param name="data">The data to write. <para>要写入的数据。</para></param>
    /// <param name="length">The number of bytes to write. <para>要写入的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The number of bytes actually written. <para>实际写入的字节数。</para></returns>
    public virtual long Write(byte[] data, int length, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        int? lastError = null;
        var outcome = UsbTransferOutcome.Success;
        int retryCount = 0;

        if (!IsOpen)
        {
            throw new UsbDeviceHandleClosedException("Device handle is closed.");
        }
        ValidateWriteData(data, length);

        int effectiveTimeoutMs = UsbTransferPolicies.NormalizeTimeout(timeoutMs, DefaultTimeoutMs);

        if (length == 0)
        {
            EmitTransfer(UsbTransferOperation.Write, 0, 0, effectiveTimeoutMs, 0, null, stopwatch.ElapsedMilliseconds, UsbTransferOutcome.Success);
            return 0;
        }

        int lenRemaining = length;
        int count = 0;

        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            while (lenRemaining > 0)
            {
                int lenToSend = Math.Min(lenRemaining, MaxChunkSize);
                IntPtr ptr = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + count);

                var chunk = WriteChunk(ptr, lenToSend, effectiveTimeoutMs);
                retryCount += chunk.RetryCount;

                if (chunk.Status == UsbChunkStatus.Timeout)
                {
                    lastError = chunk.NativeError;
                    outcome = UsbTransferOutcome.Timeout;
                    break;
                }

                if (chunk.Status == UsbChunkStatus.FatalError)
                {
                    lastError = chunk.NativeError;
                    EmitTransfer(UsbTransferOperation.Write, length, count, effectiveTimeoutMs, retryCount, lastError, stopwatch.ElapsedMilliseconds, UsbTransferOutcome.FatalError, CapturePayload(data, 0, count));
                    throw CreateWriteFatalException(chunk.NativeError);
                }

                if (chunk.Status == UsbChunkStatus.Error)
                {
                    lastError = chunk.NativeError;
                    outcome = UsbTransferOutcome.Error;
                    break;
                }

                int transferred = chunk.Transferred;
                count += transferred;
                lenRemaining -= transferred;

                if (transferred < lenToSend) break;
            }
        }
        finally
        {
            handle.Free();
        }

        if (outcome == UsbTransferOutcome.Success && count > 0 && count < length)
        {
            outcome = UsbTransferOutcome.ShortTransfer;
        }

        EmitTransfer(UsbTransferOperation.Write, length, count, effectiveTimeoutMs, retryCount, lastError, stopwatch.ElapsedMilliseconds, outcome, CapturePayload(data, 0, count));
        return count;
    }

    /// <summary>
    /// Asynchronously writes data to the device.
    /// <para>异步向设备写入数据。</para>
    /// </summary>
    /// <param name="data">The data to write. <para>要写入的数据。</para></param>
    /// <param name="length">The number of bytes to write. <para>要写入的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. <para>用于监视取消请求的令牌。</para></param>
    /// <returns>A task that represents the asynchronous write operation with the number of bytes written. <para>表示异步写入操作并返回已写入字节数的任务。</para></returns>
    public virtual Task<long> WriteAsync(byte[] data, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        return FirmwareKit.Comm.Usb.Abstractions.UsbAsyncExecution.Run(() => Write(data, length, timeoutMs), cancellationToken);
    }

    /// <summary>
    /// Asynchronously performs a USB control transfer.
    /// <para>异步执行 USB 控制传输。</para>
    /// </summary>
    /// <param name="setupPacket">The setup packet for the control request. <para>控制请求的 Setup 包。</para></param>
    /// <param name="buffer">The data buffer, or <c>null</c> for zero-length transfers. <para>数据缓冲区；零长度传输时为 <c>null</c>。</para></param>
    /// <param name="offset">The byte offset within the buffer. <para>缓冲区内的字节偏移量。</para></param>
    /// <param name="length">The number of bytes to transfer. <para>要传输的字节数。</para></param>
    /// <param name="timeoutMs">The timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. <para>用于监视取消请求的令牌。</para></param>
    /// <returns>A task that represents the asynchronous control transfer with the number of bytes transferred. <para>表示异步控制传输并返回已传输字节数的任务。</para></returns>
    public virtual Task<int> ControlTransferAsync(FirmwareKit.Comm.Usb.Abstractions.UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs, CancellationToken cancellationToken = default)
    {
        return FirmwareKit.Comm.Usb.Abstractions.UsbAsyncExecution.Run(() => ControlTransfer(setupPacket, buffer, offset, length, timeoutMs), cancellationToken);
    }

    /// <summary>
    /// Retrieves the serial number string from the device.
    /// <para>从设备获取序列号字符串。</para>
    /// </summary>
    /// <returns>Zero on success; a negative error code on failure. <para>成功时返回零；失败时返回负数错误码。</para></returns>
    public abstract int GetSerialNumber();

    /// <summary>
    /// Opens the device handle and claims the USB interface.
    /// <para>打开设备句柄并声明 USB 接口。</para>
    /// </summary>
    /// <returns>Zero on success; a non-zero error code on failure. <para>成功时返回零；失败时返回非零错误码。</para></returns>
    public abstract int CreateHandle();

    /// <summary>
    /// Resets the USB device.
    /// <para>重置 USB 设备。</para>
    /// </summary>
    public abstract void Reset();

    /// <summary>
    /// Asynchronously resets the USB device.
    /// <para>异步重置 USB 设备。</para>
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. <para>用于监视取消请求的令牌。</para></param>
    /// <returns>A task that represents the asynchronous reset operation. <para>表示异步重置操作的任务。</para></returns>
    public virtual Task ResetAsync(CancellationToken cancellationToken = default)
    {
        return FirmwareKit.Comm.Usb.Abstractions.UsbAsyncExecution.Run(Reset, cancellationToken);
    }

    /// <summary>
    /// Releases the device handle and all associated resources.
    /// <para>释放设备句柄及所有关联资源。</para>
    /// </summary>
    public abstract void Dispose();
}

/// <summary>
/// Identifies the platform-specific USB device backend type.
/// <para>标识平台特定的 USB 设备后端类型。</para>
/// </summary>
internal enum UsbDeviceType
{
    /// <summary>
    /// Windows legacy USB backend.
    /// <para>Windows 传统 USB 后端。</para>
    /// </summary>
    WinLegacy = 0,

    /// <summary>
    /// Windows WinUSB backend.
    /// <para>Windows WinUSB 后端。</para>
    /// </summary>
    WinUSB = 1,

    /// <summary>
    /// Linux usbfs backend.
    /// <para>Linux usbfs 后端。</para>
    /// </summary>
    Linux = 2,

    /// <summary>
    /// LibUSB cross-platform backend.
    /// <para>LibUSB 跨平台后端。</para>
    /// </summary>
    LibUSB = 3,

    /// <summary>
    /// macOS native backend.
    /// <para>macOS 原生后端。</para>
    /// </summary>
    MacOS = 4,

    /// <summary>
    /// HarmonyOS USBManager backend (via IPC bridge).
    /// <para>HarmonyOS USBManager 后端（通过 IPC 桥接）。</para>
    /// </summary>
    HarmonyOS = 5
}
