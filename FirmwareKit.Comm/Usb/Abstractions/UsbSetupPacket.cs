namespace FirmwareKit.Comm.Usb.Abstractions;

/// <summary>
/// Describes a USB control transfer setup packet.
/// 描述 USB 控制传输的 setup 包。
/// </summary>
public struct UsbSetupPacket
{
    /// <summary>
    /// Gets or sets the request type byte.
    /// 获取或设置请求类型字节。
    /// </summary>
    public byte RequestType { get; set; }

    /// <summary>
    /// Gets or sets the request byte.
    /// 获取或设置请求字节。
    /// </summary>
    public byte Request { get; set; }

    /// <summary>
    /// Gets or sets the value field.
    /// 获取或设置 value 字段。
    /// </summary>
    public ushort Value { get; set; }

    /// <summary>
    /// Gets or sets the index field.
    /// 获取或设置 index 字段。
    /// </summary>
    public ushort Index { get; set; }

    /// <summary>
    /// Gets or sets the length field.
    /// 获取或设置 length 字段。
    /// </summary>
    public ushort Length { get; set; }
}