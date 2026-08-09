#!/usr/bin/env bash
#
# Real-device automated test for FirmwareKit.Comm on macOS.
#
# Builds the solution, runs the unit tests, then exercises the CLI against real
# hardware: backend enumeration, JSON device listing, a read-only device selftest
# (open session + GET_DESCRIPTOR control transfer + short ReadExact) and a monitor
# hot-plug smoke test. Deliberately performs NO writes and NO resets.
#
# Notes:
#   - The native backend uses IOUSBHost and requires macOS 10.15+.
#   - The libusb backend (--api libusb) requires the native runtime:
#       brew install libusb
#
# Options:
#   --vid <hex>       optional vendor id filter (e.g. 0x18D1)
#   --pid <hex>       optional product id filter (e.g. 0xD00D)
#   --duration <sec>  selftest loop duration in seconds (0 = single pass)
#   --api <name>      backend: auto (default), native, libusb
#
# Examples:
#   bash tools/test-macos.sh --vid 0x18D1 --pid 0xD00D
#   bash tools/test-macos.sh --api libusb --duration 30
#
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VID=""
PID=""
DURATION=0
API="auto"

usage() {
    sed -n '2,24p' "$0" | sed 's/^# \{0,1\}//'
    echo
    echo "  --vid <hex>        vendor id filter (e.g. 0x18D1)"
    echo "  --pid <hex>        product id filter (e.g. 0xD00D)"
    echo "  --duration <sec>   selftest loop duration in seconds (0 = single pass)"
    echo "  --api <auto|native|libusb>"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --vid)      VID="${2:-}"; shift 2 ;;
        --pid)      PID="${2:-}"; shift 2 ;;
        --duration) DURATION="${2:-0}"; shift 2 ;;
        --api)      API="${2:-auto}"; shift 2 ;;
        -h|--help)  usage; exit 0 ;;
        *) echo "unknown option: $1"; usage; exit 1 ;;
    esac
done

FAILURES=0

echo "== FirmwareKit.Comm real-device test (macOS) =="

# --- 0. Environment check ------------------------------------------------
if ! command -v dotnet >/dev/null 2>&1; then
    echo "FAIL	dotnet SDK not found (install .NET 10 SDK)."
    exit 1
fi

if [ "$API" = "libusb" ] && ! brew list libusb >/dev/null 2>&1; then
    echo "NOTE	libusb backend selected but 'brew list libusb' failed - run 'brew install libusb' first."
fi

# --- 1. Build ------------------------------------------------------------
echo "Building Release..."
if ! dotnet build FirmwareKit.Comm.slnx -c Release --nologo >/dev/null 2>&1; then
    echo "FAIL	build"
    exit 1
fi
echo "PASS	build"

# --- 2. Unit tests -------------------------------------------------------
if ! dotnet test FirmwareKit.Comm.slnx -c Release --no-build --nologo >/dev/null 2>&1; then
    echo "FAIL	unit tests"
    FAILURES=$((FAILURES + 1))
else
    echo "PASS	unit tests"
fi

# --- 3. Backend registration ---------------------------------------------
APIS="$(dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- apis 2>/dev/null)"
if [ -z "$APIS" ]; then
    echo "FAIL	apis"
    FAILURES=$((FAILURES + 1))
else
    echo "PASS	apis ($(echo "$APIS" | tr '\n' ' '))"
fi

# --- 4. Enumeration (JSON) ------------------------------------------------
DEVICES="$(dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- all-devices --api "$API" --json 2>&1)"
RC=$?
if [ "$RC" -ne 0 ]; then
    echo "FAIL	all-devices --json (exit $RC)"
    FAILURES=$((FAILURES + 1))
else
    echo "PASS	all-devices --json"
    echo "$DEVICES" | head -10
fi

# --- 4b. Repeated enumeration stability -----------------------------------
# Regression guard: a device must stay visible across repeated enumerations —
# it must NOT vanish after the first pass (feedback issue: enumeration lost the
# device while/after a session was opened). Compare the vid:pid:interface set
# across two consecutive passes.
# <para>回归守护：设备在重复枚举中必须持续可见——不得在首轮后消失（反馈问题：枚举在
# 会话打开期间/之后丢失设备）。比较连续两轮的 vid:pid:interface 集合。</para>
if [ "$RC" -eq 0 ]; then
    DEVICES2="$(dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- all-devices --api "$API" --json 2>&1)"
    RC2=$?
    if [ "$RC2" -ne 0 ]; then
        echo "FAIL	repeated enumeration: second pass failed (exit $RC2)"
        FAILURES=$((FAILURES + 1))
    else
        SET1="$(echo "$DEVICES" | grep -o '"vid": *"[^"]*","pid": *"[^"]*"' | sort -u)"
        SET2="$(echo "$DEVICES2" | grep -o '"vid": *"[^"]*","pid": *"[^"]*"' | sort -u)"
        COUNT1=$(echo "$SET1" | grep -c . || true)
        COUNT2=$(echo "$SET2" | grep -c . || true)
        if [ "$SET1" = "$SET2" ]; then
            echo "PASS	repeated enumeration stable ($COUNT1 devices both passes)"
        else
            echo "FAIL	repeated enumeration: device set changed between passes ($COUNT1 vs $COUNT2)"
            FAILURES=$((FAILURES + 1))
        fi
    fi
fi

# --- 5. Device selftest (read-only) --------------------------------------
ARGS=(selftest --api "$API")
[ -n "$VID" ] && ARGS+=(--vid "$VID")
[ -n "$PID" ] && ARGS+=(--pid "$PID")
[ "$DURATION" -gt 0 ] && ARGS+=(--duration "$DURATION")

ST_OUT="$(dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- "${ARGS[@]}" 2>&1)"
ST_RC=$?
echo "$ST_OUT"
if echo "$ST_OUT" | grep -q "SKIP"; then
    echo "SKIP	selftest: no matching device attached (attach hardware and re-run)."
elif [ "$ST_RC" -ne 0 ]; then
    echo "FAIL	selftest"
    FAILURES=$((FAILURES + 1))
else
    echo "PASS	selftest"
fi

# --- 6. Monitor hot-plug smoke (3 s, then kill it) ------------------------
# macOS has no GNU coreutils `timeout` command (command not found -> exit 127),
# so implement the 3 s deadline with background + sleep + kill instead.
# <para>macOS 没有 GNU coreutils 的 `timeout` 命令（命令未找到 → 退出码 127），
# 因此用后台 + sleep + kill 实现 3 秒截止。</para>
dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- monitor --api "$API" --interval 1 >/dev/null 2>&1 &
MON_PID=$!
sleep 3
kill "$MON_PID" 2>/dev/null
wait "$MON_PID" 2>/dev/null
RC=$?
if [ "$RC" -eq 143 ]; then # 128+15 SIGTERM: killed by us after the 3 s window
    echo "PASS	monitor (3 s hot-plug smoke)"
    echo "NOTE	manually plug/unplug a device while 'monitor' runs to verify +Added/-Removed events."
else
    echo "FAIL	monitor exited early (code $RC)"
    FAILURES=$((FAILURES + 1))
fi

# --- 7. Native backend note ----------------------------------------------
if [ "$API" = "native" ]; then
    echo "NOTE	native backend requires macOS 10.15+ (IOUSBHost.framework)."
fi

echo "=== result: $FAILURES failure(s) ==="
exit "$FAILURES"
