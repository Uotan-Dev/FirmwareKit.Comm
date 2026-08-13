using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace FirmwareKit.Comm.Backend.LibUsb;

/// <summary>
/// Makes Homebrew's libusb-1.0.dylib discoverable to LibUsbDotNet's P/Invokes on
/// macOS, without requiring the user to export <c>DYLD_FALLBACK_LIBRARY_PATH</c>.
/// <para>在 macOS 上使 Homebrew 的 libusb-1.0.dylib 可被 LibUsbDotNet 的
/// P/Invoke 发现，无需用户自行 export
/// <c>DYLD_FALLBACK_LIBRARY_PATH</c>。</para>
/// </summary>
/// <remarks>
/// <b>Problem.</b> LibUsbDotNet 3.0.224 registers its own
/// <see cref="NativeLibrary.SetDllImportResolver"/> and probes for
/// <c>"libusb-1.0"</c> by bare SONAME. On a stock macOS host Homebrew installs
/// the dylib under <c>/opt/homebrew/lib</c> (ARM) or <c>/usr/local/lib</c>
/// (Intel) — neither is in .NET's default <c>NativeLibrary.TryLoad</c> search,
/// nor in dyld's default fallback (which is <c>/usr/local/lib:/lib:/usr/lib</c>
/// but .NET's <c>NativeLibrary</c> on macOS does not consult dyld's fallback
/// for bare SONAMEs). LibUsbDotNet's <c>NativeMethods</c> static constructor
/// therefore throws <c>DllNotFoundException</c> before any consumer can
/// intervene, because <c>typeof(LibUsbDotNet...)</c> triggers the static
/// constructor at JIT time — earlier than any <c>AssemblyLoad</c> hook or
/// <c>ModuleInitializer</c> can register a competing resolver on the
/// LibUsbDotNet assembly.
/// <para><b>问题。</b>LibUsbDotNet 3.0.224 自注册
/// <see cref="NativeLibrary.SetDllImportResolver"/> 并以裸 SONAME
/// <c>"libusb-1.0"</c> 探测。原生 macOS 主机上 Homebrew 将 dylib 安装在
/// <c>/opt/homebrew/lib</c>（ARM）或 <c>/usr/local/lib</c>（Intel）——两者均不在
/// .NET 默认的 <c>NativeLibrary.TryLoad</c> 搜索路径，也不在 dyld 默认回退
/// （<c>/usr/local/lib:/lib:/usr/lib</c>，但 .NET 的 <c>NativeLibrary</c> 在 macOS
/// 上不就裸 SONAME 咨询 dyld 回退）之列。LibUsbDotNet 的 <c>NativeMethods</c>
/// 静态构造因此在任何消费者介入之前抛 <c>DllNotFoundException</c>，因为
/// <c>typeof(LibUsbDotNet...)</c> 在 JIT 阶段就触发静态构造——早于任何
/// <c>AssemblyLoad</c> 钩子或 <c>ModuleInitializer</c> 能在 LibUsbDotNet 程序集
/// 上注册竞争解析器。</para>
///
/// <b>Solution.</b> On macOS, if Homebrew/MacPorts libusb exists at a known path
/// but the bare SONAME is not resolvable, set
/// <c>DYLD_FALLBACK_LIBRARY_PATH</c> to that directory and <c>execve</c> the
/// current process with the augmented environment. dyld re-reads
/// <c>DYLD_FALLBACK_LIBRARY_PATH</c> on the new process image, so LibUsbDotNet's
/// own resolver then resolves <c>"libusb-1.0"</c> via the Homebrew directory.
/// The relaunch is guarded by a marker environment variable so it happens at
/// most once per process tree.
/// <para><b>方案。</b>macOS 上，若 Homebrew/MacPorts 的 libusb 存在于已知路径但
/// 裸 SONAME 不可解析，则设置 <c>DYLD_FALLBACK_LIBRARY_PATH</c> 为该目录，并以
/// 增强后的环境 <c>execve</c> 当前进程。dyld 在新进程映像上重读
/// <c>DYLD_FALLBACK_LIBRARY_PATH</c>，故 LibUsbDotNet 自身的解析器随后即可通过
/// Homebrew 目录解析 <c>"libusb-1.0"</c>。重启由哨兵环境变量守卫，每个进程树至多
/// 发生一次。</para>
///
/// <b>Why not <c>setenv</c> in-process?</b> dyld reads <c>DYLD_*</c> exactly
/// once at process start; <c>setenv</c> after start is invisible to subsequent
/// <c>dlopen</c>/<c>NativeLibrary.TryLoad</c> calls (verified by probe).
/// <c>execve</c> is the only way to feed a new <c>DYLD_*</c> value to dyld.
/// <para><b>为何不在进程内 <c>setenv</c>？</b>dyld 仅在进程启动时读取一次
/// <c>DYLD_*</c>；启动后 <c>setenv</c> 对后续 <c>dlopen</c>/
/// <c>NativeLibrary.TryLoad</c> 调用不可见（已由探针验证）。
/// <c>execve</c> 是向 dyld 递交新 <c>DYLD_*</c> 值的唯一方式。</para>
///
/// <b>Safety.</b> The relaunch only fires on macOS when Homebrew/MacPorts libusb
/// is present and the bare SONAME is not already resolvable. On Linux/Windows
/// the loader is a no-op. The marker <c>FIRMWAREKIT_COMM_LIBUSB_RELAUNCHED</c>
/// prevents infinite loops. <c>execve</c> preserves the managed entry point and
/// all <c>Main</c> arguments; only the environment is augmented.
/// <para><b>安全性。</b>重启仅在 macOS 上、且 Homebrew/MacPorts libusb 存在且裸
/// SONAME 当前不可解析时触发。Linux/Windows 上加载器为空操作。哨兵
/// <c>FIRMWAREKIT_COMM_LIBUSB_RELAUNCHED</c> 防止无限循环。<c>execve</c> 保留托管
/// 入口与全部 <c>Main</c> 参数；仅环境被增强。</para>
/// </remarks>
internal static class LibUsbNativeLoader
{
    // Search order: Homebrew ARM, Homebrew Intel, MacPorts. The first existing
    // file's directory becomes DYLD_FALLBACK_LIBRARY_PATH.
    // <para>搜索顺序：Homebrew ARM、Homebrew Intel、MacPorts。首个存在文件所在
    // 目录成为 DYLD_FALLBACK_LIBRARY_PATH。</para>
    private static readonly string[] HomebrewPaths = new string[]
    {
        "/opt/homebrew/lib/libusb-1.0.dylib",
        "/usr/local/lib/libusb-1.0.dylib",
        "/opt/local/lib/libusb-1.0.dylib",
    };

    // Marker that the relaunch already happened. Preserved across execve because
    // we explicitly pass it in the new environment.
    // <para>标记重启已发生。跨越 execve 保留，因为我们显式在新环境中传递它。</para>
    private const string RelaunchMarker = "FIRMWAREKIT_COMM_LIBUSB_RELAUNCHED";

#if NET5_0_OR_GREATER
    [ModuleInitializer]
    internal static void InitializeModule()
#else
    // ModuleInitializer is only available on net5+; on netstandard2.0/net8.0 the
    // caller must invoke RelaunchIfNeededIfMacos() explicitly from a net5+ entry
    // point. The net10.0 TFM (used by the CLI and tests) hits the ModuleInitializer.
    // <para>ModuleInitializer 仅在 net5+ 可用；netstandard2.0/net8.0 上调用方须从
    // net5+ 入口点显式调用 RelaunchIfNeededIfMacos()。net10.0 TFM（CLI 与测试用）
    // 命中 ModuleInitializer。</para>
    internal static void RelaunchIfNeededIfMacos()
#endif
    {
        // Only macOS needs the DYLD fallback trick. Linux/Windows load libusb
        // via the system loader's default search.
        // <para>仅 macOS 需要 DYLD 回退技巧。Linux/Windows 通过系统加载器默认
        // 搜索加载 libusb。</para>
        if (!IsMacOS())
        {
            return;
        }

        // If the bare SONAME already resolves (libusb in dyld shared cache, or
        // DYLD_FALLBACK_LIBRARY_PATH already set by the user), nothing to do.
        // <para>若裸 SONAME 当前可解析（libusb 位于 dyld 共享缓存，或用户已设
        // DYLD_FALLBACK_LIBRARY_PATH），则无需动作。</para>
        if (TryLoadLibusbSoname())
        {
            return;
        }

        // If we already relaunched, do not loop — fall through and let the
        // caller's libusb P/Invoke surface the DllNotFoundException honestly.
        // <para>若已重启过，不再循环——落入调用方，让其 libusb P/Invoke 如实抛出
        // DllNotFoundException。</para>
        if (Environment.GetEnvironmentVariable(RelaunchMarker) == "1")
        {
            return;
        }

        // Find the first Homebrew/MacPorts libusb that physically exists.
        // <para>查找首个物理存在的 Homebrew/MacPorts libusb。</para>
        string? libDir = null;
        foreach (string path in HomebrewPaths)
        {
            if (File.Exists(path))
            {
                libDir = Path.GetDirectoryName(path);
                break;
            }
        }

        // No Homebrew libusb installed — nothing we can do; let the P/Invoke fail.
        // <para>未安装 Homebrew libusb——无能为力；让 P/Invoke 失败。</para>
        if (libDir is null)
        {
            return;
        }

        // NEVER execve-relaunch inside a test host. vstest/testhost loads
        // FirmwareKit.Comm.dll through the test adapter; a relaunch replaces the
        // test process image and kills the run (crash exit code 3). Unit tests use
        // fakes/mocks and never need real libusb preloading. The execve path is
        // only for application entry points (CLI, tools).
        // <para>绝不在测试宿主内 execve 重启。vstest/testhost 经测试适配器加载
        // FirmwareKit.Comm.dll；重启会替换测试进程映像并杀死运行（崩溃退出码 3）。
        // 单元测试使用 fake/mock，从不需真实 libusb 预加载。execve 路径仅用于应用
        // 入口点（CLI、工具）。</para>
        if (IsRunningInTestHost())
        {
            // Diagnostics: surface WHY the test-host branch triggered so we can
            // confirm the guard fired (stderr, visible in vstest console output).
            // <para>诊断：暴露测试宿主分支为何触发，以确认守卫生效（stderr，在 vstest
            // 控制台输出中可见）。</para>
            try
            {
                Console.Error.WriteLine($"[LibUsbNativeLoader] test-host detected (FriendlyName='{AppDomain.CurrentDomain.FriendlyName}', argv0='{(Environment.GetCommandLineArgs().Length > 0 ? Environment.GetCommandLineArgs()[0] : "<none>")}'); skipping execve relaunch.");
            }
            catch
            {
                // stderr write is best-effort diagnostics only.
                // <para>stderr 写入仅为尽力诊断。</para>
            }
            return;
        }

        RelaunchWithDyldFallback(libDir);
    }

    // Detects a vstest/testhost test process: the test adapter names the entry
    // assembly "testhost.dll" (or "testhost") and the AppDomain friendly name
    // contains "testhost". Both checks are cheap and conservative — no false
    // positives for CLI invocations.
    // <para>检测 vstest/testhost 测试进程：测试适配器将入口程序集命名为
    // "testhost.dll"（或 "testhost"），AppDomain 友好名包含 "testhost"。两项检查
    // 均廉价且保守——对 CLI 调用无误报。</para>
    private static bool IsRunningInTestHost()
    {
        try
        {
            string friendly = AppDomain.CurrentDomain.FriendlyName;
            if (friendly.IndexOf("testhost", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string[] cmd = Environment.GetCommandLineArgs();
            if (cmd.Length > 0)
            {
                string entry = Path.GetFileName(cmd[0]);
                if (entry.IndexOf("testhost", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Best-effort detection; on failure assume an application host.
            // <para>尽力检测；失败时假定为应用宿主。</para>
        }
        return false;
    }

    private static void RelaunchWithDyldFallback(string libDir)
    {
        // Build the new environment: copy the current environ, set
        // DYLD_FALLBACK_LIBRARY_PATH=libDir and the relaunch marker.
        // <para>构建新环境：复制当前 environ，设置
        // DYLD_FALLBACK_LIBRARY_PATH=libDir 与重启哨兵。</para>
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string val)
            {
                env[key] = val;
            }
        }

        // Prepend libDir so Homebrew's directory wins even if the user already
        // had a DYLD_FALLBACK_LIBRARY_PATH (multiple dirs are ':'-separated on macOS).
        // <para>前置 libDir，即使用户已有 DYLD_FALLBACK_LIBRARY_PATH 也使 Homebrew
        // 目录胜出（macOS 上多目录以 ':' 分隔）。</para>
        string existing = Environment.GetEnvironmentVariable("DYLD_FALLBACK_LIBRARY_PATH") ?? string.Empty;
        env["DYLD_FALLBACK_LIBRARY_PATH"] = string.IsNullOrEmpty(existing)
            ? libDir
            : libDir + ":" + existing;
        env[RelaunchMarker] = "1";

        // execve replaces the process image. The `path` argument is the binary
        // to exec (the absolute dotnet); argv[] is what the new image sees as
        // its program name + arguments. When the host was launched as
        // `dotnet Foo.dll args...`, .NET's GetCommandLineArgs() reports
        // argv[0]=Foo.dll, argv[1..]=args (NOT argv[0]=dotnet). So we must
        // PREPEND the dotnet binary as the new argv[0] and shift the existing
        // slots right, giving execve the `[dotnet, Foo.dll, args...]` shape it
        // needs to re-host the managed entry point.
        // <para>execve 替换进程映像。`path` 参数为要执行的二进制（绝对 dotnet）；
        // argv[] 是新映像视作的程序名 + 参数。宿主以 `dotnet Foo.dll args...` 启动时，
        // .NET 的 GetCommandLineArgs() 报 argv[0]=Foo.dll、argv[1..]=args（而非
        // argv[0]=dotnet）。故须前置 dotnet 二进制为新 argv[0]，原槽位右移，给 execve
        // `[dotnet, Foo.dll, args...]` 形以重新宿主托管入口点。</para>
        string procPath = GetProcessPath() ?? throw new InvalidOperationException("Process path could not be determined (Environment.ProcessPath is null on this TFM).");
        string[] cmdLine = Environment.GetCommandLineArgs();
        string[] argv = new string[cmdLine.Length + 1];
        argv[0] = procPath; // dotnet binary as argv[0]
        for (int i = 0; i < cmdLine.Length; i++) argv[i + 1] = cmdLine[i];

        // Build C-string arrays for execve: argv is NULL-terminated, envp likewise.
        // <para>为 execve 构建 C 字符串数组：argv 以 NULL 终止，envp 同样。</para>
        IntPtr[] argvPtrs = new IntPtr[argv.Length + 1];
        for (int i = 0; i < argv.Length; i++)
        {
            argvPtrs[i] = Marshal.StringToHGlobalAnsi(argv[i]);
        }

        string[] envStrings = new string[env.Count];
        int idx = 0;
        foreach (var kvp in env)
        {
            envStrings[idx++] = kvp.Key + "=" + kvp.Value;
        }

        IntPtr[] envPtrs = new IntPtr[envStrings.Length + 1];
        for (int i = 0; i < envStrings.Length; i++)
        {
            envPtrs[i] = Marshal.StringToHGlobalAnsi(envStrings[i]);
        }

        try
        {
            int r = Execve(procPath, argvPtrs, envPtrs);
            // execve only returns on failure; fall through and let the P/Invoke fail.
            // <para>execve 仅在失败时返回；落入调用方，让其 P/Invoke 失败。</para>
            _ = r;
        }
        finally
        {
            foreach (IntPtr p in argvPtrs) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
            foreach (IntPtr p in envPtrs) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
        }
    }

    [DllImport("libSystem.dylib", EntryPoint = "execve", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Execve(string path, IntPtr[] argv, IntPtr[] envp);

    // ---- Cross-TFM helpers (netstandard2.0 lacks OperatingSystem.IsMacOS,
    // NativeLibrary.TryLoad, and Environment.ProcessPath) ----
    // <para>跨 TFM 助手（netstandard2.0 缺 OperatingSystem.IsMacOS、
    // NativeLibrary.TryLoad 与 Environment.ProcessPath）。</para>

    private static bool IsMacOS()
    {
#if NET5_0_OR_GREATER
        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
#else
        // netstandard2.0 fallback: read sysname via uname() — "Darwin" => macOS.
        // <para>netstandard2.0 回退：通过 uname() 读 sysname——"Darwin" => macOS。</para>
        var buf = new byte[256];
        int r = uname(buf);
        if (r != 0) return false;
        int len = Array.IndexOf(buf, (byte)0);
        string sysname = len > 0 ? System.Text.Encoding.ASCII.GetString(buf, 0, len) : string.Empty;
        return sysname == "Darwin";
#endif
    }

    private static bool TryLoadLibusbSoname()
    {
        // Probe whether the bare "libusb-1.0" SONAME already resolves. On net5+
        // use NativeLibrary.TryLoad; on netstandard2.0 use dlopen(RTLD_NOLOAD).
        // <para>探测裸 "libusb-1.0" SONAME 当前是否可解析。net5+ 用
        // NativeLibrary.TryLoad；netstandard2.0 用 dlopen(RTLD_NOLOAD)。</para>
#if NET5_0_OR_GREATER
        return System.Runtime.InteropServices.NativeLibrary.TryLoad("libusb-1.0", out _);
#else
        const int RTLD_NOLOAD = 0x10;
        try
        {
            IntPtr h = MacDlopenProbe("libusb-1.0", RTLD_NOLOAD);
            return h != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
#endif
    }

    private static string? GetProcessPath()
    {
        // Environment.ProcessPath only exists on net5+. On netstandard2.0 fall
        // back to reading /proc/self/exe (Linux) — but the relaunch path only
        // fires on macOS, where Environment.ProcessPath is always available via
        // the net5+ TFM. The netstandard2.0 TFM never enters the relaunch path
        // because IsMacOS() gates it, so returning null here is safe.
        // <para>Environment.ProcessPath 仅 net5+ 存在。netstandard2.0 上回退为
        // 读 /proc/self/exe（Linux）——但重启路径仅在 macOS 触发，而 macOS 上
        // Environment.ProcessPath 经 net5+ TFM 始终可用。netstandard2.0 TFM
        // 因 IsMacOS() 守卫永不进入重启路径，故此处返回 null 安全。</para>
#if NET5_0_OR_GREATER
        return Environment.ProcessPath;
#else
        return null;
#endif
    }

#if !NET5_0_OR_GREATER
    [DllImport("libSystem.dylib", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr MacDlopenProbe(string path, int mode);

    [DllImport("libc", EntryPoint = "uname", CallingConvention = CallingConvention.Cdecl)]
    private static extern int uname(byte[] buf);
#endif
}
