namespace FirmwareKit.Comm.Usb.Diagnostics;

internal static class UsbTrace
{
    public static bool IsEnabled { get; set; } =
        string.Equals(Environment.GetEnvironmentVariable("FIRMWAREKIT_USB_DEBUG"), "1", StringComparison.Ordinal);

    public static IUsbLogger Logger { get; set; } = new ConsoleUsbLogger();

    public static void Log(string message)
    {
        if (!IsEnabled || string.IsNullOrEmpty(message)) return;
        Logger.Log(message);
    }
}
