namespace FirmwareKit.Comm.Usb.Diagnostics;

/// <summary>
/// Writes USB diagnostics to the error stream.
/// 将 USB 诊断日志写入错误输出流。
/// </summary>
public sealed class ConsoleUsbLogger : IUsbLogger
{
    /// <summary>
    /// Writes a diagnostic message to stderr.
    /// 将诊断日志写入标准错误输出。
    /// </summary>
    /// <param name="message">The diagnostic message. 诊断日志消息内容。</param>
    public void Log(string message)
    {
        Console.Error.WriteLine($"[USB] {message}");
    }
}
