using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Usb.Backend.OpenHarmony;

internal static class OpenHarmonyUsbAPI
{
    public const string Libc = "libc";

    [StructLayout(LayoutKind.Sequential)]
    public struct usbdevfs_bulktransfer
    {
        public uint ep;
        public uint len;
        public uint timeout;
        public IntPtr data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct usbdevfs_ctrltransfer
    {
        public byte bRequestType;
        public byte bRequest;
        public ushort wValue;
        public ushort wIndex;
        public ushort wLength;
        public uint timeout;
        public IntPtr data;
    }

    public const uint USBDEVFS_BULK = 0xC0105502;
    public static uint USBDEVFS_BULK_X86_64 = 0xC0185502;
    public static uint USBDEVFS_BULK_X86 = 0xC0105502;

    public static uint USBDEVFS_CLAIMINTERFACE = 0x8004550F;
    public static uint USBDEVFS_RELEASEINTERFACE = 0x80045510;
    public static uint USBDEVFS_DISCONNECT = 0x5516;
    public static uint USBDEVFS_RESET = 0x5514;

    public static uint USBDEVFS_CONTROL_X86_64 = 0xC0185500;
    public static uint USBDEVFS_CONTROL_X86 = 0xC0105500;
    public const uint USBDEVFS_CONTROL = 0xC0105500;

    [DllImport(Libc, SetLastError = true)]
    public static extern int open(string pathname, int flags);

    [DllImport(Libc, SetLastError = true)]
    public static extern int close(int fd);

    [DllImport(Libc, SetLastError = true)]
    public static extern IntPtr read(int fd, IntPtr buf, UIntPtr count);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, UIntPtr request, IntPtr arg);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, UIntPtr request, ref int arg);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, UIntPtr request, ref usbdevfs_bulktransfer arg);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, UIntPtr request, ref usbdevfs_ctrltransfer arg);

    public const int O_RDWR = 2;
    public const int O_CLOEXEC = 0x80000;

    [DllImport(Libc, SetLastError = true)]
    public static extern IntPtr opendir(string name);

    [DllImport(Libc, SetLastError = true)]
    public static extern int closedir(IntPtr dirp);

    [DllImport(Libc, SetLastError = true)]
    public static extern IntPtr readdir(IntPtr dirp);

    [StructLayout(LayoutKind.Sequential)]
    public struct Dirent64
    {
        public ulong d_ino;
        public long d_off;
        public ushort d_reclen;
        public byte d_type;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string d_name;
    }

    public const int EINTR = 4;
    public const int EAGAIN = 11;
    public const int ENODEV = 19;
    public const int EPIPE = 32;
    public const int ESHUTDOWN = 108;
    public const int ETIMEDOUT = 110;
    public const int EPROTO = 71;
    public const int EACCES = 13;
    public const int EBUSY = 16;

    internal static bool IsOpenHarmonyPlatform()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return false;
            }

            if (File.Exists("/etc/ohos-release"))
            {
                return true;
            }

            if (File.Exists("/system/etc/ohos-release"))
            {
                return true;
            }

            try
            {
                var osRelease = File.ReadAllText("/etc/os-release");
                if (osRelease.IndexOf("OpenHarmony", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                if (Directory.Exists("/system/lib/hdf") ||
                    Directory.Exists("/system/lib64/hdf"))
                {
                    if (File.Exists("/dev/hdf/usb_host"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsHarmonyOSPlatform()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return false;
            }

            try
            {
                var osRelease = File.ReadAllText("/etc/os-release");
                if (osRelease.IndexOf("HarmonyOS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch
            {
            }

            if (File.Exists("/etc/hmos-release") || File.Exists("/system/etc/hmos-release"))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
