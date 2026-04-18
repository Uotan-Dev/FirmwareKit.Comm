namespace FirmwareKit.Comm.Usb.Diagnostics;

/// <summary>
/// Ignores USB diagnostic log messages.
/// 忽略 USB 诊断日志消息。
/// </summary>
public sealed class NullUsbLogger : IUsbLogger
{
    /// <summary>
    /// Gets the shared instance.
    /// 获取共享实例。
    /// </summary>
    public static readonly NullUsbLogger Instance = new();

    private NullUsbLogger()
    {
    }

    /// <summary>
    /// Ignores the diagnostic message.
    /// 忽略该诊断日志消息。
    /// </summary>
    /// <param name="message">The diagnostic message. 诊断日志消息内容。</param>
    public void Log(string message)
    {
    }
}
