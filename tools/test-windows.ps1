<#
.SYNOPSIS
Real-device automated test for FirmwareKit.Comm on Windows.

.DESCRIPTION
Builds the solution, runs the unit tests, then exercises the CLI against real
hardware: backend enumeration, JSON device listing, a read-only device selftest
(open session + GET_DESCRIPTOR control transfer + short ReadExact) and a monitor
hot-plug smoke test. Deliberately performs NO writes and NO resets.

.PARAMETER Vid
Optional vendor id filter (hex, e.g. 0x18D1) for enumeration/selftest.

.PARAMETER PidFilter
Optional product id filter (hex, e.g. 0xD00D). Named PidFilter because $Pid is a
read-only PowerShell automatic variable (the process id).

.PARAMETER Duration
Selftest loop duration in seconds (0 = single pass).

.PARAMETER Api
Backend to test: auto (default), native, libusb.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\test-windows.ps1 -Vid 0x18D1 -PidFilter 0xD00D

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\test-windows.ps1 -Api libusb -Duration 30
#>
[CmdletBinding()]
param(
    [string]$Vid = "",
    [string]$PidFilter = "",
    [int]$Duration = 0,
    [ValidateSet("auto", "native", "libusb")]
    [string]$Api = "auto"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$failures = 0

function Write-Check {
    param([string]$Name, [bool]$Ok, [string]$Detail = "")
    $mark = if ($Ok) { "PASS" } else { "FAIL" }
    if ($Detail) {
        Write-Host "$mark`t$Name - $Detail"
    }
    else {
        Write-Host "$mark`t$Name"
    }
}

Write-Host "== FirmwareKit.Comm real-device test (Windows) =="

# --- 0. Environment check ------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "FAIL`tdotnet SDK not found (install .NET 10 SDK)."
    exit 1
}

# --- 1. Build -------------------------------------------------------------
Write-Host "Building Release..."
dotnet build FirmwareKit.Comm.slnx -c Release --nologo | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL`tbuild"
    exit 1
}
Write-Check "build" $true

# --- 2. Unit tests --------------------------------------------------------
dotnet test FirmwareKit.Comm.slnx -c Release --no-build --nologo | Out-Null
Write-Check "unit tests" ($LASTEXITCODE -eq 0)
if ($LASTEXITCODE -ne 0) { $failures++ }

# --- 3. Backend registration ---------------------------------------------
$apis = dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- apis
Write-Check "apis" ($LASTEXITCODE -eq 0 -and $apis) "($($apis -join ', '))"
if ($LASTEXITCODE -ne 0 -or -not $apis) { $failures++ }

# --- 4. Enumeration (JSON) ------------------------------------------------
$devices = dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- all-devices --api $Api --json
Write-Check "all-devices --json" ($LASTEXITCODE -eq 0)
if ($LASTEXITCODE -ne 0) { $failures++ } else { Write-Host $devices }

# --- 4b. Repeated enumeration stability -----------------------------------
# Regression guard: a device must stay visible across repeated enumerations —
# it must NOT vanish after the first pass (feedback issue: enumeration lost the
# device while/after a session was opened). Compare the vid:pid:interface set
# across two consecutive passes.
# <para>回归守护：设备在重复枚举中必须持续可见——不得在首轮后消失（反馈问题：枚举在
# 会话打开期间/之后丢失设备）。比较连续两轮的 vid:pid:interface 集合。</para>
if ($LASTEXITCODE -eq 0) {
    $devices2 = dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- all-devices --api $Api --json
    if ($LASTEXITCODE -eq 0) {
        $set1 = @($devices | ConvertFrom-Json | ForEach-Object { "$($_.vid):$($_.pid):$($_.interfaceClass)/$($_.interfaceSubClass)/$($_.interfaceProtocol)" } | Sort-Object) -join ';'
        $set2 = @($devices2 | ConvertFrom-Json | ForEach-Object { "$($_.vid):$($_.pid):$($_.interfaceClass)/$($_.interfaceSubClass)/$($_.interfaceProtocol)" } | Sort-Object) -join ';'
        if ($set1 -eq $set2) {
            Write-Check "repeated enumeration stable ($(@($set1 -split ';')).Count devices both passes)" $true
        }
        else {
            Write-Host "FAIL`trepeated enumeration: device set changed between passes"
            $failures++
        }
    }
    else {
        Write-Host "FAIL`trepeated enumeration: second pass failed"
        $failures++
    }
}

# --- 5. Device selftest (read-only) --------------------------------------
$selftestArgs = @("selftest", "--api", $Api)
if ($Vid) { $selftestArgs += "--vid"; $selftestArgs += $Vid }
if ($PidFilter) { $selftestArgs += "--pid"; $selftestArgs += $PidFilter }
if ($Duration -gt 0) { $selftestArgs += "--duration"; $selftestArgs += $Duration }

$stOut = dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- $selftestArgs 2>&1
$stOut | ForEach-Object { Write-Host $_ }
if ($stOut -match "SKIP") {
    Write-Host "SKIP`tselftest: no matching device attached (attach hardware and re-run)."
}
elseif ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL`tselftest"
    $failures++
}
else {
    Write-Check "selftest" $true
}

# --- 6. Monitor hot-plug smoke (runs 3 s, then stops) ---------------------
$p = Start-Process dotnet -ArgumentList @(
    "run", "--project", "FirmwareKit.Comm.CLI", "-c", "Release", "--no-build", "--",
    "monitor", "--api", $Api, "--interval", "1"
) -PassThru -NoNewWindow
Start-Sleep -Seconds 3
if ($p.HasExited) {
    Write-Host "FAIL`tmonitor exited early (code $($p.ExitCode))."
    $failures++
}
else {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Write-Check "monitor (3 s hot-plug smoke)" $true
    Write-Host "NOTE`tmanually plug/unplug a device while 'monitor' runs to verify +Added/-Removed events."
}

# --- Summary --------------------------------------------------------------
Write-Host "=== result: $failures failure(s) ==="
exit $failures
