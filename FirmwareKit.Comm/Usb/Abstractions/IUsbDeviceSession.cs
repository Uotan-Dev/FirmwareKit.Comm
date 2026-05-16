namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Represents an opened USB device session.
/// <para>表示一个已打开的 USB 设备会话。</para>
/// </summary>
public interface IUsbDeviceSession : IDisposable
{
    /// <summary>
    /// Gets the default timeout used by this session, if the caller omits one.
    /// <para>获取该会话在调用方未显式指定超时时使用的默认超时。</para>
    /// </summary>
    int DefaultTimeoutMs { get; }

    /// <summary>
    /// Gets the device metadata.
    /// <para>获取设备元数据。</para>
    /// </summary>
    UsbDeviceInfo DeviceInfo { get; }

    /// <summary>
    /// Reads up to the specified number of bytes.
    /// <para>读取最多指定字节数的数据。</para>
    /// </summary>
    /// <param name="length">Maximum number of bytes to read. <para>最多读取的字节数。</para></param>
    /// <returns>The bytes read from the device. <para>从设备读取到的字节数组。</para></returns>
    byte[] Read(int length);

    /// <summary>
    /// Reads up to the specified number of bytes with an operation timeout.
    /// <para>在指定超时时间内读取最多指定字节数的数据。</para>
    /// </summary>
    /// <param name="length">Maximum number of bytes to read. <para>最多读取的字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The bytes read from the device. <para>从设备读取到的字节数组。</para></returns>
    byte[] Read(int length, int timeoutMs);

    /// <summary>
    /// Reads bytes into a caller-provided buffer.
    /// <para>将数据读取到调用方提供的缓冲区。</para>
    /// </summary>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>读取字节数。</para></param>
    /// <returns>The number of bytes read. <para>实际读取的字节数。</para></returns>
    int ReadInto(byte[] buffer, int offset, int length);

    /// <summary>
    /// Reads bytes into a caller-provided buffer with an operation timeout.
    /// <para>在指定超时时间内将数据读取到调用方提供的缓冲区。</para>
    /// </summary>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>读取字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The number of bytes read. <para>实际读取的字节数。</para></returns>
    int ReadInto(byte[] buffer, int offset, int length, int timeoutMs);

    /// <summary>
    /// Writes bytes to the device.
    /// <para>向设备写入字节数据。</para>
    /// </summary>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <returns>The number of bytes written. <para>实际写入的字节数。</para></returns>
    long Write(byte[] data, int length);

    /// <summary>
    /// Writes bytes to the device with an operation timeout.
    /// <para>在指定超时时间内向设备写入字节数据。</para>
    /// </summary>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The number of bytes written. <para>实际写入的字节数。</para></returns>
    long Write(byte[] data, int length, int timeoutMs);

    /// <summary>
    /// Sends or receives a USB control transfer.
    /// <para>发送或接收 USB 控制传输。</para>
    /// </summary>
    /// <param name="setupPacket">The setup packet. <para>setup 包。</para></param>
    /// <param name="buffer">The data buffer, or <c>null</c> for a zero-length transfer. <para>数据缓冲区，零长度传输可传 <c>null</c>。</para></param>
    /// <param name="offset">The buffer offset. <para>缓冲区偏移量。</para></param>
    /// <param name="length">The number of bytes to transfer. <para>传输字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The number of bytes transferred. <para>实际传输字节数。</para></returns>
    int ControlTransfer(UsbSetupPacket setupPacket, byte[]? buffer, int offset, int length, int timeoutMs);

    /// <summary>
    /// Resets the device or backend transport.
    /// <para>重置设备或后端传输层。</para>
    /// </summary>
    void Reset();
}
