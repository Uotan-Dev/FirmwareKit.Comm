using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;

namespace FirmwareKit.Comm.Usb.Core;

internal sealed class UsbDeviceMonitor : IDisposable
{
    private readonly Func<IReadOnlyList<UsbDeviceInfo>> _enumerator;
    private readonly Action<IReadOnlyList<UsbDeviceChange>> _onChanged;
    private readonly Action<Exception>? _onError;
    private readonly TimeSpan _pollInterval;
    private readonly object _gate = new();
    private Timer? _timer;
    private Dictionary<string, UsbDeviceInfo> _lastSnapshot = new(StringComparer.OrdinalIgnoreCase);
    private bool _isPolling;
    private bool _disposed;

    public UsbDeviceMonitor(
        Func<IReadOnlyList<UsbDeviceInfo>> enumerator,
        Action<IReadOnlyList<UsbDeviceChange>> onChanged,
        Action<Exception>? onError,
        TimeSpan pollInterval,
        bool fireInitialSnapshot)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _onError = onError;
        _pollInterval = pollInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : pollInterval;

        IReadOnlyList<UsbDeviceInfo> initialDevices;
        try
        {
            initialDevices = _enumerator();
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
            UsbTrace.Log($"UsbDeviceMonitor initial enumerate failed: {ex.GetType().Name}: {ex.Message}");
            initialDevices = Array.Empty<UsbDeviceInfo>();
        }

        var initial = BuildMap(initialDevices);
        _lastSnapshot = initial;
        if (fireInitialSnapshot && initial.Count > 0)
        {
            var initialChanges = initial.Values
                .Select(device => new UsbDeviceChange { Kind = UsbDeviceChangeKind.Added, Device = device })
                .ToList();
            try
            {
                _onChanged(initialChanges);
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
                UsbTrace.Log($"UsbDeviceMonitor initial callback failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        _timer = new Timer(static state => ((UsbDeviceMonitor)state!).Poll(), this, _pollInterval, _pollInterval);
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
            _timer?.Dispose();
            _timer = null;
            _lastSnapshot.Clear();
        }
    }

    private void Poll()
    {
        lock (_gate)
        {
            if (_disposed || _isPolling)
            {
                return;
            }

            _isPolling = true;
        }

        try
        {
            var currentSnapshot = BuildMap(_enumerator());
            List<UsbDeviceChange>? changes = null;

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                foreach (var pair in currentSnapshot)
                {
                    if (_lastSnapshot.ContainsKey(pair.Key))
                    {
                        continue;
                    }

                    changes ??= new List<UsbDeviceChange>();
                    changes.Add(new UsbDeviceChange
                    {
                        Kind = UsbDeviceChangeKind.Added,
                        Device = pair.Value
                    });
                }

                foreach (var pair in _lastSnapshot)
                {
                    if (currentSnapshot.ContainsKey(pair.Key))
                    {
                        continue;
                    }

                    changes ??= new List<UsbDeviceChange>();
                    changes.Add(new UsbDeviceChange
                    {
                        Kind = UsbDeviceChangeKind.Removed,
                        Device = pair.Value
                    });
                }

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
                    UsbTrace.Log($"UsbDeviceMonitor change callback failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
            UsbTrace.Log($"UsbDeviceMonitor poll failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            lock (_gate)
            {
                _isPolling = false;
            }
        }
    }

    private static Dictionary<string, UsbDeviceInfo> BuildMap(IReadOnlyList<UsbDeviceInfo> devices)
    {
        var map = new Dictionary<string, UsbDeviceInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            map[BuildIdentityKey(device)] = device;
        }

        return map;
    }

    private static string BuildIdentityKey(UsbDeviceInfo device)
    {
        if (!string.IsNullOrWhiteSpace(device.DeviceKey))
        {
            return device.DeviceKey;
        }

        return UsbDeviceIdentity.BuildKey(device);
    }
}
