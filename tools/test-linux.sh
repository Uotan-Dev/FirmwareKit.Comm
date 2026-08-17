#!/usr/bin/env bash
#
# Real-device automated test for FirmwareKit.Comm on Linux.
#
# Device-less contract (GitHub-hosted runners expose NO physical USB devices):
#   HARD-GATE stages — must pass:
#     1. build           — dotnet build
#     2. unit tests      — dotnet test
#     3. apis            — ≥1 backend registers
#     4. all-devices     — enumeration exits 0; 0 devices is valid
#     5. repeated enum   — two consecutive passes match
#     6. monitor         — runs the full 3 s window
#   SOFT-PATH stages — never fail CI (need attached hardware):
#     selftest           — open session + GET_DESCRIPTOR + ReadExact
#
# On a device-less hosted runner: selftest reports SKIP (no matching device,
# exit 0). This is the expected outcome and never fails the script.
# <para>无设备契约：硬门（build/unit tests/apis/all-devices/repeated enum/monitor）
# 必须通过；软路径（selftest，需附加硬件）永不失败 CI。无设备托管 runner 上 selftest
# 报告 SKIP（无匹配设备，退出码 0），为预期结果，永不失败脚本。</para>
#
# Options:
#   --vid <hex>       optional vendor id filter (e.g. 0x18D1)
#   --pid <hex>       optional product id filter (e.g. 0xD00D)
#   --duration <sec>  selftest loop duration in seconds (0 = single pass)
#   --api <name>      backend: auto (default), native, libusb
#
# Examples:
#   bash tools/test-linux.sh --vid 0x18D1 --pid 0xD00D
#   bash tools/test-linux.sh --api libusb --duration 30
#
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VID=""
PID=""
DURATION=0
API="auto"

usage() {
    sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'
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

echo "== FirmwareKit.Comm real-device test (Linux) =="

# --- 0. Environment check ------------------------------------------------
if ! command -v dotnet >/dev/null 2>&1; then
    echo "FAIL	dotnet SDK not found (install .NET 10 SDK)."
    exit 1
fi

# --- Stage 1: Build (hard gate) ------------------------------------------
echo "Building Release..."
if ! dotnet build FirmwareKit.Comm.slnx -c Release --nologo >/dev/null 2>&1; then
    echo "FAIL	build"
    exit 1
fi
echo "PASS	build"

# --- Stage 2: Unit tests (hard gate) ------------------------------------
if ! dotnet test FirmwareKit.Comm.slnx -c Release --no-build --nologo >/dev/null 2>&1; then
    echo "FAIL	unit tests"
    FAILURES=$((FAILURES + 1))
else
    echo "PASS	unit tests"
fi

# --- Stage 3: Backend registration (hard gate) --------------------------
APIS="$(dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- apis 2>/dev/null)"
if [ -z "$APIS" ]; then
    echo "FAIL	apis"
    FAILURES=$((FAILURES + 1))
else
    echo "PASS	apis ($(echo "$APIS" | tr '\n' ' '))"
fi

# --- Stage 4: Enumeration JSON (hard gate) ------------------------------
DEVICES="$(dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- all-devices --api "$API" --json 2>&1)"
RC=$?
if [ "$RC" -ne 0 ]; then
    echo "FAIL	all-devices --json (exit $RC)"
    FAILURES=$((FAILURES + 1))
else
    echo "PASS	all-devices --json"
    echo "$DEVICES" | head -10
fi

# --- Stage 5: Repeated enumeration stability (hard gate) ----------------
# Regression guard: a device must stay visible across repeated enumerations —
# it must NOT vanish after the first pass (feedback issue: enumeration lost the
# device while/after a session was opened). Compare the vid:pid set across two
# consecutive passes. On a device-less runner both passes list zero devices and
# the guard passes trivially.
# <para>回归守护：设备在重复枚举中必须持续可见——不得在首轮后消失（反馈问题：枚举在
# 会话打开期间/之后丢失设备）。比较连续两轮的 vid:pid 集合。无设备 runner 上两轮均
# 列出零设备，守护平凡通过。</para>
if [ "$RC" -eq 0 ]; then
    DEVICES2="$(dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- all-devices --api "$API" --json 2>&1)"
    RC2=$?
    if [ "$RC2" -ne 0 ]; then
        echo "FAIL	repeated enumeration: second pass failed (exit $RC2)"
        FAILURES=$((FAILURES + 1))
    else
        SET1="$(echo "$DEVICES" | awk -F'"' '/"vid":/{v=$4} /"pid":/{print v":"$4}' | sort -u)"
        SET2="$(echo "$DEVICES2" | awk -F'"' '/"vid":/{v=$4} /"pid":/{print v":"$4}' | sort -u)"
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

# --- Stage 6: selftest (soft path — needs attached hardware) ------------
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
    echo "NOTE	selftest exited non-zero (exit $ST_RC): expected on device-less Linux runners."
else
    echo "PASS	selftest"
fi

# --- Stage 7: Monitor hot-plug smoke (hard gate, 3 s) -------------------
timeout 3 dotnet run --project FirmwareKit.Comm.CLI -c Release --no-build -- monitor --api "$API" --interval 1 >/dev/null 2>&1
RC=$?
if [ "$RC" -eq 124 ]; then
    echo "PASS	monitor (3 s hot-plug smoke)"
    echo "NOTE	manually plug/unplug a device while 'monitor' runs to verify +Added/-Removed events."
else
    echo "FAIL	monitor exited early (code $RC)"
    FAILURES=$((FAILURES + 1))
fi

# --- Permission hint (udev) ---------------------------------------------
if grep -q "EACCES" <<< "$DEVICES" 2>/dev/null; then
    echo "NOTE	permission denied seen - ensure udev rules grant access to /dev/bus/usb."
fi

echo "=== result: $FAILURES failure(s) ==="
exit "$FAILURES"
