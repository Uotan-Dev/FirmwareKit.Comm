namespace FirmwareKit.Comm.Usb.Diagnostics;

/// <summary>
/// Provides USB diagnostics logging and structured transfer events.
/// 提供 USB 诊断日志与结构化传输事件。
/// </summary>
public static class UsbTrace
{
    /// <summary>
    /// Gets or sets whether plain text USB logs are enabled.
    /// 获取或设置是否启用纯文本 USB 日志。
    /// </summary>
    public static bool IsEnabled { get; set; } =
        string.Equals(Environment.GetEnvironmentVariable("FIRMWAREKIT_USB_DEBUG"), "1", StringComparison.Ordinal);

    /// <summary>
    /// Gets or sets the logger that receives plain text diagnostics.
    /// 获取或设置接收纯文本诊断日志的记录器。
    /// </summary>
    public static IUsbLogger Logger { get; set; } = new ConsoleUsbLogger();

    /// <summary>
    /// Occurs when a structured transfer event is emitted.
    /// 当结构化传输事件产生时触发。
    /// </summary>
    public static event Action<UsbTransferEvent>? TransferObserved;

    /// <summary>
    /// Writes a plain text diagnostic message through <see cref="Logger"/>.
    /// 通过 <see cref="Logger"/> 写入纯文本诊断消息。
    /// </summary>
    /// <param name="message">The diagnostic message. 诊断消息。</param>
    public static void Log(string message)
    {
        if (!IsEnabled || string.IsNullOrEmpty(message)) return;
        Logger.Log(message);
    }

    /// <summary>
    /// Emits a structured transfer event.
    /// 发送结构化传输事件。
    /// </summary>
    /// <param name="evt">The transfer event. 传输事件。</param>
    public static void EmitTransfer(UsbTransferEvent evt)
    {
        if (evt == null)
        {
            return;
        }

        var handler = TransferObserved;
        if (handler == null)
        {
            return;
        }

        try
        {
            handler(evt);
        }
        catch (Exception ex)
        {
            Log($"TransferObserved callback failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
