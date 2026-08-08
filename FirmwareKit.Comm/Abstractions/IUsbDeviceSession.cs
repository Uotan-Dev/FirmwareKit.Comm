namespace FirmwareKit.Comm.Abstractions;

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
    /// Gets the bulk IN endpoint address bound to this session (bit 7 set), or 0 when the
    /// backend does not expose endpoint-level binding.
    /// <para>获取绑定到本会话的批量 IN 端点地址（bit 7 置位），后端不暴露端点绑定时为 0。</para>
    /// Protocol layers that must target a specific endpoint pair (e.g. Rockchip loader on
    /// 0x82/0x02 instead of the first bulk pair) can verify the bound endpoints here.
    /// <para>必须针对特定端点对的协议层（例如使用 0x82/0x02 而非第一对 bulk 的
    /// Rockchip loader）可在此校验实际绑定的端点。</para>
    /// </summary>
    byte EndpointIn { get; }

    /// <summary>
    /// Gets the bulk OUT endpoint address bound to this session (bit 7 clear), or 0 when the
    /// backend does not expose endpoint-level binding.
    /// <para>获取绑定到本会话的批量 OUT 端点地址（bit 7 清零），后端不暴露端点绑定时为 0。</para>
    /// </summary>
    byte EndpointOut { get; }

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
    /// Reads bytes into a caller-provided buffer and reports the transfer outcome.
    /// <para>将数据读取到调用方提供的缓冲区并报告传输结果。</para>
    /// Unlike <see cref="ReadInto(byte[],int,int,int)"/>, which collapses timeouts and short
    /// packets into a bare byte count, this returns a <see cref="UsbReadResult"/> that
    /// distinguishes the two — protocol layers (fastboot/EDL/bootrom) use it to detect the
    /// USB short-packet message boundary and to tell "no data yet" from "transfer ended".
    /// <para>与把超时和短包折叠为裸字节数的 <see cref="ReadInto(byte[],int,int,int)"/> 不同，
    /// 此方法返回区分二者的 <see cref="UsbReadResult"/>——协议层（fastboot/EDL/bootrom）
    /// 用它检测 USB 短包消息边界，并区分"暂无数据"与"传输已结束"。</para>
    /// </summary>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">The number of bytes to read. <para>读取字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The read result with byte count and outcome flags. <para>包含字节数与结果标志的读取结果。</para></returns>
    UsbReadResult ReadPacket(byte[] buffer, int offset, int length, int timeoutMs);

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
    /// Writes bytes to the device starting at the specified offset, with an operation timeout.
    /// <para>在指定超时时间内从指定偏移量开始向设备写入字节数据。</para>
    /// This overload avoids copying a sub-range out of a large buffer (e.g. streaming a
    /// firmware image in chunks), which the <see cref="UsbStream"/> adapter relies on.
    /// <para>此重载避免从大缓冲区复制子范围（例如分块流式传输固件镜像），
    /// <see cref="UsbStream"/> 适配器依赖该能力。</para>
    /// </summary>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="offset">The offset into <paramref name="data"/> at which to start. <para><paramref name="data"/> 中的起始偏移量。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The number of bytes written. <para>实际写入的字节数。</para></returns>
    long Write(byte[] data, int offset, int length, int timeoutMs);

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
    /// Sets the active alternate setting of an interface (standard SET_INTERFACE request).
    /// <para>设置接口的活动备用设置（标准 SET_INTERFACE 请求）。</para>
    /// Used by protocol layers that must select a specific interface alternate setting
    /// (e.g. CDC-ACM data interface, RNDIS). Check <see cref="UsbApiCapabilities.SupportsInterfaceConfigSwitching"/>
    /// before use; backends without support throw <see cref="NotSupportedException"/>.
    /// <para>供必须选择特定接口备用设置的协议层使用（如 CDC-ACM 数据接口、RNDIS）。
    /// 使用前请检查 <see cref="UsbApiCapabilities.SupportsInterfaceConfigSwitching"/>；
    /// 不支持的后端将抛出 <see cref="NotSupportedException"/>。</para>
    /// </summary>
    /// <param name="interfaceNumber">The interface number. <para>接口编号。</para></param>
    /// <param name="altSetting">The alternate setting to activate. <para>要激活的备用设置。</para></param>
    void SetInterfaceAltSetting(byte interfaceNumber, byte altSetting);

    /// <summary>
    /// Sets the active device configuration (standard SET_CONFIGURATION request).
    /// <para>设置设备的活动配置（标准 SET_CONFIGURATION 请求）。</para>
    /// </summary>
    /// <param name="configuration">The configuration value to activate. <para>要激活的配置值。</para></param>
    void SetConfiguration(byte configuration);

    /// <summary>
    /// Reads bytes from an interrupt endpoint into a caller-provided buffer.
    /// <para>从中断端点将数据读取到调用方提供的缓冲区。</para>
    /// Some bootrom/status-reporting endpoints expose interrupt IN pipes; protocol layers use
    /// this to poll device state without disturbing the bulk session endpoints. Backends that
    /// cannot perform interrupt transfers throw <see cref="NotSupportedException"/>.
    /// <para>部分 bootrom/状态上报端点使用中断 IN 管道；协议层可用此接口轮询设备状态而无需
    /// 干扰 bulk 会话端点。无法执行中断传输的后端抛出 <see cref="NotSupportedException"/>。</para>
    /// </summary>
    /// <param name="endpointAddress">The interrupt IN endpoint address (bit 7 set). <para>中断 IN 端点地址（bit 7 置位）。</para></param>
    /// <param name="buffer">The destination buffer. <para>目标缓冲区。</para></param>
    /// <param name="offset">The destination offset. <para>目标偏移量。</para></param>
    /// <param name="length">Maximum number of bytes to read. <para>最多读取的字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The read result with byte count and outcome flags. <para>包含字节数与结果标志的读取结果。</para></returns>
    UsbReadResult ReadInterrupt(byte endpointAddress, byte[] buffer, int offset, int length, int timeoutMs);

    /// <summary>
    /// Writes bytes to an interrupt endpoint.
    /// <para>向中断端点写入字节数据。</para>
    /// </summary>
    /// <param name="endpointAddress">The interrupt OUT endpoint address (bit 7 clear). <para>中断 OUT 端点地址（bit 7 清零）。</para></param>
    /// <param name="data">The data to write. <para>待写入数据。</para></param>
    /// <param name="offset">The offset into <paramref name="data"/> at which to start. <para><paramref name="data"/> 中的起始偏移量。</para></param>
    /// <param name="length">The number of bytes to write. <para>写入字节数。</para></param>
    /// <param name="timeoutMs">Timeout in milliseconds. <para>超时时间（毫秒）。</para></param>
    /// <returns>The number of bytes written. <para>实际写入的字节数。</para></returns>
    long WriteInterrupt(byte endpointAddress, byte[] data, int offset, int length, int timeoutMs);

    /// <summary>
    /// Resets the device or backend transport.
    /// <para>重置设备或后端传输层。</para>
    /// </summary>
    void Reset();
}
