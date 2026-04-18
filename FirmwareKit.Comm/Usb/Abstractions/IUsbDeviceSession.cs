namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Represents an opened USB device session.
/// 表示一个已打开的 USB 设备会话。
/// </summary>
public interface IUsbDeviceSession : IDisposable
{
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
    /// Reads bytes into a caller-provided buffer.
    /// 将数据读取到调用方提供的缓冲区。
    /// </summary>
    /// <param name="buffer">The destination buffer. 目标缓冲区。</param>
    /// <param name="offset">The destination offset. 目标偏移量。</param>
    /// <param name="length">The number of bytes to read. 读取字节数。</param>
    /// <returns>The number of bytes read. 实际读取的字节数。</returns>
    int ReadInto(byte[] buffer, int offset, int length);

    /// <summary>
    /// Writes bytes to the device.
    /// 向设备写入字节数据。
    /// </summary>
    /// <param name="data">The data to write. 待写入数据。</param>
    /// <param name="length">The number of bytes to write. 写入字节数。</param>
    /// <returns>The number of bytes written. 实际写入的字节数。</returns>
    long Write(byte[] data, int length);

    /// <summary>
    /// Resets the device or backend transport.
    /// 重置设备或后端传输层。
    /// </summary>
    void Reset();
}
