using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Backend.Windows;
using FirmwareKit.Comm.Usb.Diagnostics;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Usb.Core;

/// <summary>
/// Event-driven USB device monitoring on Windows via a hidden message-only window that
/// registers <c>RegisterDeviceNotification</c> for the known interface GUIDs and re-runs
/// enumeration + diff on every <c>WM_DEVICECHANGE</c> (arrival/removal) event.
/// <para>Windows 上的事件驱动 USB 设备监视：通过隐藏消息窗口为已知接口 GUID 注册
/// <c>RegisterDeviceNotification</c>，每次收到 <c>WM_DEVICECHANGE</c>（插入/移除）事件时
/// 重新枚举并做快照对比。</para>
/// Falls back to <see cref="UsbDeviceMonitor"/> polling at the caller when the hidden window
/// cannot be created (e.g. headless/restricted sessions).
/// <para>无法创建隐藏窗口（例如受限会话）时，由调用方回退到 <see cref="UsbDeviceMonitor"/> 轮询。</para>
/// </summary>
internal sealed class UsbWindowsHotplugMonitor : IDisposable
{
    private readonly Func<IReadOnlyList<UsbDeviceInfo>> _enumerator;
    private readonly Action<IReadOnlyList<UsbDeviceChange>> _onChanged;
    private readonly Action<Exception>? _onError;
    private readonly object _gate = new();
    private readonly Win32API.WndProcDelegate _wndProc; // keep the delegate alive for the native callback
    private Thread? _thread;
    private IntPtr _hwnd;
    private Dictionary<string, UsbDeviceInfo> _lastSnapshot = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public UsbWindowsHotplugMonitor(
        Func<IReadOnlyList<UsbDeviceInfo>> enumerator,
        Action<IReadOnlyList<UsbDeviceChange>> onChanged,
        Action<Exception>? onError)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _onError = onError;
        _wndProc = WndProc;

        _lastSnapshot = UsbDeviceMonitor.BuildMap(_enumerator());

        _thread = new Thread(MessagePump)
        {
            IsBackground = true,
            Name = "FirmwareKit.Comm Windows USB hotplug"
        };
        _thread.Start();
    }

    private void MessagePump()
    {
        string className = "FirmwareKitCommHotplugWindow";
        var wc = new Win32API.WNDCLASSW
        {
            lpfnWndProc = _wndProc,
            lpszClassName = className,
            hInstance = Win32API.GetModuleHandleW(null)
        };

        try
        {
            if (Win32API.RegisterClassW(ref wc) == 0 && Marshal.GetLastWin32Error() != 1410 /* ERROR_CLASS_ALREADY_EXISTS */)
            {
                throw new InvalidOperationException($"RegisterClassW failed: {Marshal.GetLastWin32Error()}");
            }

            _hwnd = Win32API.CreateWindowExW(0, className, string.Empty, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException($"CreateWindowExW failed: {Marshal.GetLastWin32Error()}");
            }

            foreach (var guid in WinUSBFinder.KnownInterfaceGUIDs)
            {
                var filter = new Win32API.DEV_BROADCAST_DEVICEINTERFACE_W
                {
                    dbcc_size = (uint)Marshal.OffsetOf(typeof(Win32API.DEV_BROADCAST_DEVICEINTERFACE_W), "dbcc_name"),
                    dbcc_devicetype = Win32API.DBT_DEVTYP_DEVICEINTERFACE,
                    dbcc_classguid = WinUSBFinder.ToApiGuid(guid)
                };
                IntPtr filterPtr = Marshal.AllocHGlobal(Marshal.SizeOf(filter));
                try
                {
                    Marshal.StructureToPtr(filter, filterPtr, false);
                    _ = Win32API.RegisterDeviceNotificationW(_hwnd, filterPtr, Win32API.DEVICE_NOTIFY_WINDOW_HANDLE);
                }
                finally
                {
                    Marshal.FreeHGlobal(filterPtr);
                }
            }

            while (Win32API.GetMessageW(out var msg, IntPtr.Zero, 0, 0))
            {
                _ = Win32API.TranslateMessage(ref msg);
                _ = Win32API.DispatchMessageW(ref msg);
            }
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
            UsbTrace.Log($"UsbWindowsHotplugMonitor message pump failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (_hwnd != IntPtr.Zero)
            {
                _ = Win32API.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32API.WM_DEVICECHANGE &&
            (wParam == (IntPtr)Win32API.DBT_DEVICEARRIVAL || wParam == (IntPtr)Win32API.DBT_DEVICEREMOVECOMPLETE))
        {
            TriggerPoll();
        }

        return Win32API.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void TriggerPoll()
    {
        // Never block the message pump: re-enumerate and diff on the thread pool.
        _ = Task.Run(() =>
        {
            try
            {
                PollOnce();
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
                UsbTrace.Log($"UsbWindowsHotplugMonitor poll failed: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    private void PollOnce()
    {
        var currentSnapshot = UsbDeviceMonitor.BuildMap(_enumerator());
        List<UsbDeviceChange>? changes;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            changes = UsbDeviceMonitor.ComputeChanges(currentSnapshot, _lastSnapshot);
            _lastSnapshot = currentSnapshot;
        }

        if (changes is { Count: > 0 })
        {
            try
            {
                _onChanged(changes);
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
                UsbTrace.Log($"UsbWindowsHotplugMonitor change callback failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        var hwnd = _hwnd;
        _hwnd = IntPtr.Zero;
        if (hwnd != IntPtr.Zero)
        {
            _ = Win32API.PostMessageW(hwnd, Win32API.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _thread?.Join(TimeSpan.FromSeconds(2));
        }

        _lastSnapshot.Clear();
    }
}
