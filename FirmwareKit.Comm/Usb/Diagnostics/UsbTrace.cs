namespace FirmwareKit.Comm.Usb.Diagnostics;

/// <summary>
/// Provides USB diagnostics logging and structured transfer events.
/// <para>提供 USB 诊断日志与结构化传输事件。</para>
/// </summary>
public static class UsbTrace
{
    /// <summary>
    /// Gets or sets whether plain text USB logs are enabled.
    /// <para>获取或设置是否启用纯文本 USB 日志。</para>
    /// </summary>
    public static bool IsEnabled { get; set; } =
        string.Equals(Environment.GetEnvironmentVariable("FIRMWAREKIT_USB_DEBUG"), "1", StringComparison.Ordinal);

    /// <summary>
    /// Gets or sets the logger that receives plain text diagnostics.
    /// <para>获取或设置接收纯文本诊断日志的记录器。</para>
    /// </summary>
    public static IUsbLogger Logger { get; set; } = new ConsoleUsbLogger();

    /// <summary>
    /// Occurs when a structured transfer event is emitted.
    /// <para>当结构化传输事件产生时触发。</para>
    /// </summary>
    public static event Action<UsbTransferEvent>? TransferObserved;

    /// <summary>
    /// Writes a plain text diagnostic message through <see cref="Logger"/>.
    /// <para>通过 <see cref="Logger"/> 写入纯文本诊断消息。</para>
    /// </summary>
    /// <param name="message">The diagnostic message. <para>诊断消息。</para></param>
    public static void Log(string message)
    {
        if (!IsEnabled || string.IsNullOrEmpty(message)) return;
        Logger.Log(message);
    }

    /// <summary>
    /// Emits a structured transfer event.
    /// <para>发送结构化传输事件。</para>
    /// </summary>
    /// <param name="evt">The transfer event. <para>传输事件。</para></param>
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
