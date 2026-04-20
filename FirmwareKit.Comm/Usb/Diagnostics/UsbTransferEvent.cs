namespace FirmwareKit.Comm.Usb.Diagnostics;

/// <summary>
/// Describes the kind of transfer operation.
/// 描述传输操作类型。
/// </summary>
public enum UsbTransferOperation
{
    /// <summary>
    /// Read operation.
    /// 读取操作。
    /// </summary>
    Read = 0,

    /// <summary>
    /// Write operation.
    /// 写入操作。
    /// </summary>
    Write = 1,

    /// <summary>
    /// Reset operation.
    /// 重置操作。
    /// </summary>
    Reset = 2,

    /// <summary>
    /// Device enumeration operation.
    /// 设备枚举操作。
    /// </summary>
    Enumerate = 3
}

/// <summary>
/// Describes how a transfer operation finished.
/// 描述传输操作结束状态。
/// </summary>
public enum UsbTransferOutcome
{
    /// <summary>
    /// Transfer completed successfully.
    /// 传输成功完成。
    /// </summary>
    Success = 0,

    /// <summary>
    /// Transfer stopped due to timeout.
    /// 传输因超时结束。
    /// </summary>
    Timeout = 1,

    /// <summary>
    /// Transfer completed with fewer bytes than requested.
    /// 传输字节数少于请求值。
    /// </summary>
    ShortTransfer = 2,

    /// <summary>
    /// Transfer failed with a fatal error.
    /// 传输遇到致命错误。
    /// </summary>
    FatalError = 3,

    /// <summary>
    /// Transfer could not start because backend/session was not ready.
    /// 后端或会话未就绪，无法开始传输。
    /// </summary>
    NotReady = 4,

    /// <summary>
    /// Transfer was canceled.
    /// 传输被取消。
    /// </summary>
    Canceled = 5
}

/// <summary>
/// Represents a structured USB transfer event.
/// 表示结构化 USB 传输事件。
/// </summary>
public sealed class UsbTransferEvent
{
    /// <summary>
    /// Gets or sets backend name.
    /// 获取或设置后端名称。
    /// </summary>
    public string Backend { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets device path.
    /// 获取或设置设备路径。
    /// </summary>
    public string DevicePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets transfer operation kind.
    /// 获取或设置传输操作类型。
    /// </summary>
    public UsbTransferOperation Operation { get; set; }

    /// <summary>
    /// Gets or sets requested bytes.
    /// 获取或设置请求字节数。
    /// </summary>
    public int RequestedBytes { get; set; }

    /// <summary>
    /// Gets or sets transferred bytes.
    /// 获取或设置实际传输字节数。
    /// </summary>
    public int TransferredBytes { get; set; }

    /// <summary>
    /// Gets or sets timeout in milliseconds.
    /// 获取或设置超时（毫秒）。
    /// </summary>
    public int TimeoutMs { get; set; }

    /// <summary>
    /// Gets or sets retry count.
    /// 获取或设置重试次数。
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets native error code if available.
    /// 获取或设置原生错误码（若可用）。
    /// </summary>
    public int? NativeErrorCode { get; set; }

    /// <summary>
    /// Gets or sets elapsed time in milliseconds.
    /// 获取或设置耗时（毫秒）。
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Gets or sets operation outcome.
    /// 获取或设置操作结果。
    /// </summary>
    public UsbTransferOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets optional message.
    /// 获取或设置可选消息。
    /// </summary>
    public string? Message { get; set; }
}
