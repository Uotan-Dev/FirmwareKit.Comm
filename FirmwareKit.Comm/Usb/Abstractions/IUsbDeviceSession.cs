namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Represents an opened USB device session.
/// 表示一个已打开的 USB 设备会话。
/// </summary>
public interface IUsbDeviceSession : IDisposable
{
    /// <summary>
    /// Gets the default timeout used by this session, if the caller omits one.
    /// 获取该会话在调用方未显式指定超时时使用的默认超时。
    /// </summary>
    int DefaultTimeoutMs { get; }

    /// <summary>
    /// Gets the device metadata.
    /// 获取设备元数据。
    /// </summary>
    UsbDeviceInfo DeviceInfo { get; }

    /// <summary>
    /// Reads up to the specified number of bytes.
    /// 读取最多指定字节数的数据。
    /// </summary>
    /// <param name="length">Maximum number of bytes to read. 最多读取的字节数。</param>
    /// <returns>The bytes read from the device. 从设备读取到的字节数组。</returns>
    byte[] Read(int length);

    /// <summary>
    /// Reads up to the specified number of bytes with an operation timeout.
    /// 在指定超时时间内读取最多指定字节数的数据。
    /// </summary>
    /// <param name="length">Maximum number of bytes to read. 最多读取的字节数。</param>
    /// <param name="timeoutMs">Timeout in milliseconds. 超时时间（毫秒）。</param>
    /// <returns>The bytes read from the device. 从设备读取到的字节数组。</returns>
    byte[] Read(int length, int timeoutMs);

    /// <summary>
    /// Reads bytes into a caller-provided buffer.
    /// 将数据读取到调用方提供的缓冲区。
    /// </summary>
    /// <param name="buffer">The destination buffer. 目标缓冲区。</param>
    /// <param name="offset">The destination offset. 目标偏移量。</param>
    /// <param name="length">The number of bytes to read. 读取字节数。</param>
    /// <returns>The number of bytes read. 实际读取的字节数。</returns>
    int ReadInto(byte[] buffer, int offset, int length);

    /// <summary>
    /// Reads bytes into a caller-provided buffer with an operation timeout.
    /// 在指定超时时间内将数据读取到调用方提供的缓冲区。
    /// </summary>
    /// <param name="buffer">The destination buffer. 目标缓冲区。</param>
    /// <param name="offset">The destination offset. 目标偏移量。</param>
    /// <param name="length">The number of bytes to read. 读取字节数。</param>
    /// <param name="timeoutMs">Timeout in milliseconds. 超时时间（毫秒）。</param>
    /// <returns>The number of bytes read. 实际读取的字节数。</returns>
    int ReadInto(byte[] buffer, int offset, int length, int timeoutMs);

    /// <summary>
    /// Writes bytes to the device.
    /// 向设备写入字节数据。
    /// </summary>
    /// <param name="data">The data to write. 待写入数据。</param>
    /// <param name="length">The number of bytes to write. 写入字节数。</param>
    /// <returns>The number of bytes written. 实际写入的字节数。</returns>
    long Write(byte[] data, int length);

    /// <summary>
    /// Writes bytes to the device with an operation timeout.
    /// 在指定超时时间内向设备写入字节数据。
    /// </summary>
    /// <param name="data">The data to write. 待写入数据。</param>
    /// <param name="length">The number of bytes to write. 写入字节数。</param>
    /// <param name="timeoutMs">Timeout in milliseconds. 超时时间（毫秒）。</param>
    /// <returns>The number of bytes written. 实际写入的字节数。</returns>
    long Write(byte[] data, int length, int timeoutMs);

    /// <summary>
    /// Sends or receives a USB control transfer.
    /// 发送或接收 USB 控制传输。
    /// </summary>
    /// <param name="setupPacket">The setup packet. setup 包。</param>
    /// <param name="buffer">The data buffer, or <c>null</c> for a zero-length transfer. 数据缓冲区，零长度传输可传 <c>null</c>。</param>
    /// <param name="offset">The buffer offset. 缓冲区偏移量。</param>
    /// <param name="length">The number of bytes to transfer. 传输字节数。</param>
    /// <param name="timeoutMs">Timeout in milliseconds. 超时时间（毫秒）。</param>
    /// <returns>The number of bytes transferred. 实际传输字节数。</returns>
    int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs);

    /// <summary>
    /// Resets the device or backend transport.
    /// 重置设备或后端传输层。
    /// </summary>
    void Reset();
}
