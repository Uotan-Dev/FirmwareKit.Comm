namespace FirmwareKit.Comm.Usb.Diagnostics;

/// <summary>
/// Receives USB diagnostic log messages.
/// 接收 USB 诊断日志消息。
/// </summary>
public interface IUsbLogger
{
    /// <summary>
    /// Writes a diagnostic message.
    /// 写入一条诊断日志消息。
    /// </summary>
    /// <param name="message">The diagnostic message. 诊断日志消息内容。</param>
    void Log(string message);
}
