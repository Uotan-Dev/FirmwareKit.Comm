using FirmwareKit.Comm.Usb.Abstractions;

namespace FirmwareKit.Comm.Usb.Core;

/// <summary>
/// Wraps a set of device sessions and disposes them together.
/// 封装一组设备会话并统一释放。
/// </summary>
public sealed class UsbSessionCollection : IDisposable
{

    /// <summary>
    /// Initializes a new session collection.
    /// 初始化新的会话集合。
    /// </summary>
    /// <param name="sessions">The sessions to wrap. 需要封装的会话集合。</param>
    public UsbSessionCollection(IReadOnlyList<IUsbDeviceSession> sessions)
    {
        Sessions = sessions;
    }

    /// <summary>
    /// Gets the wrapped sessions.
    /// 获取已封装的会话列表。
    /// </summary>
    public IReadOnlyList<IUsbDeviceSession> Sessions { get; }

    /// <summary>
    /// Returns an enumerator over the wrapped sessions.
    /// 返回封装会话的枚举器。
    /// </summary>
    /// <returns>An enumerator over the sessions. 会话枚举器。</returns>
    public IEnumerator<IUsbDeviceSession> GetEnumerator() => Sessions.GetEnumerator();

    /// <summary>
    /// Disposes every wrapped session.
    /// 释放所有封装会话。
    /// </summary>
    public void Dispose()
    {
        foreach (var session in Sessions)
        {
            session.Dispose();
        }
    }
}
