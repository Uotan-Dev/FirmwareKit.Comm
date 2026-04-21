namespace FirmwareKit.Comm.Usb.Abstractions;

internal static class UsbAsyncExecution
{
    public static Task<T> Run<T>(Func<T> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return action();
        }, cancellationToken);
    }

    public static Task Run(Action action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
        }, cancellationToken);
    }
}