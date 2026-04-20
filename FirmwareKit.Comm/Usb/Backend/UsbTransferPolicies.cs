namespace FirmwareKit.Comm.Usb.Backend;

internal static class UsbTransferPolicies
{
    // Keep platform defaults explicit while centralizing shared policy knobs.
    public const int DefaultTimeoutMs = 5000;
    public const int WinUsbDefaultTimeoutMs = 60000;
    public const int MaxChunkSize = 1024 * 1024;
    public const int LinuxUsbFsMaxBulkSize = 16384;
    public const int LinuxMaxRetries = 5;

    public static int NormalizeTimeout(int timeoutMs, int defaultTimeoutMs)
    {
        return timeoutMs > 0 ? timeoutMs : defaultTimeoutMs;
    }
}
