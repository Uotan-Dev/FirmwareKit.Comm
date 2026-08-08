#!/usr/bin/env bash
#
# fetch-libusb.sh - download native libusb-1.0 runtimes for every supported
# platform into <output>/<target>/libusb-1.0.<ext>.
#
# Targets:
#   win-x64, win-arm64          Windows (official release .7z primary, MSYS2 fallback)
#   macos-x64, macos-arm64      macOS   (Homebrew bottles via ghcr.io)
#   linux-x64, linux-arm64,
#   linux-riscv64, linux-loong64 Linux  (Ubuntu/Debian pool .deb; loong64 from Debian)
#
# Optional mirrors for users behind the GFW (国内镜像源):
#   --mirror tuna|ustc          sets the Ubuntu/Debian/MSYS2 base mirrors
#   --github-mirror <url>       proxy for the official release .7z (e.g. https://ghproxy.com/)
#   --ghcr-mirror <url>         proxy for Homebrew bottles (e.g. https://ghcr.nju.edu.cn/)
#
# Requirements: bash, curl, and per-format extractors (7z or bsdtar for .7z,
# zstd-capable tar for .pkg.tar.zst, ar+tar for .deb). Unavailable extractors
# produce a clear per-target error and the target is skipped.

set -u

OUTPUT_DIR="native"
MIRROR="none"          # none|tuna|ustc
GITHUB_MIRROR=""       # e.g. https://ghproxy.com/  (must end without trailing slash)
GHCR_MIRROR=""         # e.g. https://ghcr.nju.edu.cn
TARGETS="all"
VERBOSE=0

LIBUSB_RELEASE_VER="1.0.30"
LIBUSB_RELEASE_7Z="libusb-${LIBUSB_RELEASE_VER}.7z"
MSYS2_AARCH64_PKG="mingw-w64-clang-aarch64-libusb-1.0.28-1-any.pkg.tar.zst"
MSYS2_AARCH64_SHA256="eb6ab9c8eea53f0c4c96173f991063e9a9874d4fe01c2a3130e106136bee0f89"
MSYS2_X64_PKG="mingw-w64-ucrt-x86_64-libusb-1.0.28-1-any.pkg.tar.zst"

# Homebrew libusb 1.0.30 bottles (verified via formulae.brew.sh)
BREW_VER="1.0.30"
BREW_BOTTLE_MACOS_X64_NAME="sonoma"
BREW_BOTTLE_MACOS_X64_SHA256="1387aea9bbed3a1e57884b5b43166fc83cfdae415e5f3803a8259ff77a4ba613"
BREW_BOTTLE_MACOS_ARM64_NAME="arm64_sequoia"
BREW_BOTTLE_MACOS_ARM64_SHA256="a8d271bd5d9e7065987960caa52a9130d7fe6321ff1bad751499e465d0413e38"

# Linux .deb candidate versions (Ubuntu noble=1.0.27-1, Debian trixie/sid=1.0.28-1)
DEB_CANDIDATE_VERSIONS=("1.0.28-1" "1.0.27-1" "1.0.26-1")

# Windows Schannel: avoid CRYPT_E_NO_REVOCATION_CHECK (0x80092012) failures behind
# flaky certificate-revocation paths. Only meaningful with the schannel backend.
case "$(uname -s 2>/dev/null)" in
  MINGW*|MSYS*|CYGWIN*) SSL_NO_REVOKE="--ssl-no-revoke" ;;
  *) SSL_NO_REVOKE="" ;;
esac

# ---- helpers ---------------------------------------------------------------

info() { printf '==> %s\n' "$*"; }
warn() { printf '!!  %s\n' "$*" >&2; }
die()  { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

msys2_base() {
  case "$MIRROR" in
    tuna) echo "https://mirrors.tuna.tsinghua.edu.cn/msys2/mingw" ;;
    ustc) echo "https://mirrors.ustc.edu.cn/msys2/mingw" ;;
    *)    echo "https://mirror.msys2.org/mingw" ;;
  esac
}

ubuntu_base() {
  case "$MIRROR" in
    tuna) echo "https://mirrors.tuna.tsinghua.edu.cn/ubuntu" ;;
    ustc) echo "https://mirrors.ustc.edu.cn/ubuntu" ;;
    *)    echo "https://archive.ubuntu.com/ubuntu" ;;
  esac
}

debian_base() {
  case "$MIRROR" in
    tuna) echo "https://mirrors.tuna.tsinghua.edu.cn/debian" ;;
    ustc) echo "https://mirrors.ustc.edu.cn/debian" ;;
    *)    echo "https://deb.debian.org/debian" ;;
  esac
}

github_release_url() {
  local asset="$1"
  if [ -n "$GITHUB_MIRROR" ]; then
    echo "${GITHUB_MIRROR%/}/https://github.com/libusb/libusb/releases/download/v${LIBUSB_RELEASE_VER}/${asset}"
  else
    echo "https://github.com/libusb/libusb/releases/download/v${LIBUSB_RELEASE_VER}/${asset}"
  fi
}

ghcr_base() {
  if [ -n "$GHCR_MIRROR" ]; then
    echo "${GHCR_MIRROR%/}"
  else
    echo "https://ghcr.io"
  fi
}

fetch_to() { # url dest
  local url="$1" dest="$2"
  if [ "$VERBOSE" -ge 1 ]; then info "download $url"; fi
  curl -fsSL $SSL_NO_REVOKE --max-time 300 -o "$dest" "$url" || return 1
  return 0
}

sha_verify() { # file expected_sha
  [ -z "$2" ] && return 0
  local actual
  actual=$(sha256sum "$1" 2>/dev/null | awk '{print $1}')
  [ "$actual" = "$2" ] || { warn "sha256 mismatch for $1 (got $actual)"; return 1; }
  return 0
}

need_tool() { # tool label
  if ! command -v "$1" >/dev/null 2>&1; then
    warn "missing tool: $1 ($2) - target skipped"
    return 1
  fi
  return 0
}

extract_7z() { # archive dest_dir  -> uses 7z or bsdtar
  local archive="$1" dest="$2"
  mkdir -p "$dest"
  if command -v 7z >/dev/null 2>&1; then
    ( cd "$dest" && 7z x -y "$archive" >/dev/null ) && return 0
  elif command -v bsdtar >/dev/null 2>&1; then
    ( cd "$dest" && bsdtar -xf "$archive" ) && return 0
  elif [ -x "/c/Program Files/7-Zip/7z.exe" ]; then
    ( cd "$dest" && "/c/Program Files/7-Zip/7z.exe" x -y "$archive" >/dev/null ) && return 0
  fi
  warn "no 7z/bsdtar available to extract $archive (install 7-Zip or bsdtar)"
  return 1
}

extract_tar_zst() { # archive dest_dir
  local archive="$1" dest="$2"
  mkdir -p "$dest"
  if tar --zstd -xf "$archive" -C "$dest" 2>/dev/null; then return 0; fi
  if command -v bsdtar >/dev/null 2>&1 && bsdtar -xf "$archive" -C "$dest" 2>/dev/null; then return 0; fi
  if command -v unzstd >/dev/null 2>&1; then
    ( cd "$dest" && unzstd -q -c "$archive" | tar -xf - ) && return 0
  fi
  warn "no zstd-capable tar/bsdtar/unzstd to extract $archive"
  return 1
}

extract_deb_so() { # deb dest_dir libdir_sub  -> copies libusb-1.0.so.0
  local deb="$1" dest="$2" libdir="$3" work
  need_tool ar "binutils" || return 1
  work=$(mktemp -d)
  ( cd "$work" && ar x "$deb" 2>/dev/null ) || { rm -rf "$work"; return 1; }
  local data
  data=$(find "$work" -name 'data.tar.*' | head -1)
  [ -n "$data" ] || { rm -rf "$work"; return 1; }
  mkdir -p "$dest"
  tar -xf "$data" -C "$dest" 2>/dev/null
  rm -rf "$work"
  # the real .so.0.<ver> plus the .so.0 symlink
  local so
  so=$(find "$dest/usr/$libdir" -name 'libusb-1.0.so.0.*' 2>/dev/null | head -1)
  if [ -n "$so" ]; then
    cp -f "$so" "$dest/libusb-1.0.so.0"
    return 0
  fi
  return 1
}

# ---- per-target fetchers ---------------------------------------------------

fetch_win_7z() { # out_dir subpath_in_7z
  local out="$1" sub="$2"
  local work archive
  work=$(mktemp -d)
  archive="$work/$LIBUSB_RELEASE_7Z"
  if ! fetch_to "$(github_release_url "$LIBUSB_RELEASE_7Z")" "$archive"; then
    rm -rf "$work"
    return 1
  fi
  if ! extract_7z "$archive" "$work/x"; then rm -rf "$work"; return 1; fi
  local dll
  dll=$(find "$work/x/$sub" -name 'libusb-1.0.dll' 2>/dev/null | head -1)
  if [ -n "$dll" ]; then
    mkdir -p "$out"
    cp -f "$dll" "$out/libusb-1.0.dll"
    rm -rf "$work"
    return 0
  fi
  rm -rf "$work"
  return 1
}

fetch_win_msys2() { # out_dir repo_dir pkg_name [sha256]
  local out="$1" repo="$2" pkg="$3" sha="${4:-}"
  local work archive
  work=$(mktemp -d)
  archive="$work/$pkg"
  if ! fetch_to "$(msys2_base)/$repo/$pkg" "$archive"; then rm -rf "$work"; return 1; fi
  sha_verify "$archive" "$sha" || { rm -rf "$work"; return 1; }
  if ! extract_tar_zst "$archive" "$work/x"; then rm -rf "$work"; return 1; fi
  local dll
  dll=$(find "$work/x" -name 'libusb-1.0.dll' 2>/dev/null | head -1)
  if [ -n "$dll" ]; then
    mkdir -p "$out"
    cp -f "$dll" "$out/libusb-1.0.dll"
    rm -rf "$work"
    return 0
  fi
  rm -rf "$work"
  return 1
}

target_win() { # arch
  local arch="$1"
  local out="$OUTPUT_DIR/win-$arch"
  if [ "$arch" = "x64" ]; then
    if fetch_win_7z "$out" "MinGW64"; then info "win-x64: official release .7z (MinGW64)"; return 0; fi
    if fetch_win_msys2 "$out" "ucrt64" "$MSYS2_X64_PKG"; then info "win-x64: MSYS2 ucrt64"; return 0; fi
  else
    if fetch_win_7z "$out" "VS2025"; then info "win-arm64: official release .7z (VS2025/ARM64)"; return 0; fi
    if fetch_win_msys2 "$out" "clangarm64" "$MSYS2_AARCH64_PKG" "$MSYS2_AARCH64_SHA256"; then
      info "win-arm64: MSYS2 clangarm64"; return 0
    fi
  fi
  warn "win-$arch: all sources failed (github release + MSYS2)"
  return 1
}

target_macos() { # arch (x64|arm64)
  local arch="$1" out="$OUTPUT_DIR/macos-$1"
  local bottle sha
  if [ "$arch" = "x64" ]; then bottle="$BREW_BOTTLE_MACOS_X64_NAME"; sha="$BREW_BOTTLE_MACOS_X64_SHA256"; else bottle="$BREW_BOTTLE_MACOS_ARM64_NAME"; sha="$BREW_BOTTLE_MACOS_ARM64_SHA256"; fi

  # ghcr blobs require an anonymous bearer token.
  local token url work archive
  token=$(curl -fsSL $SSL_NO_REVOKE --max-time 30 "https://ghcr.io/token?service=ghcr.io&scope=repository:homebrew/core/libusb:pull" 2>/dev/null | sed -E 's/.*"token":"([^"]+)".*/\1/')
  [ -n "$token" ] || { warn "macos-$arch: failed to obtain ghcr token"; return 1; }
  url="$(ghcr_base)/v2/homebrew/core/libusb/blobs/sha256:${sha}"
  work=$(mktemp -d)
  archive="$work/libusb.tar.gz"
  if ! curl -fsSL $SSL_NO_REVOKE --max-time 300 -H "Authorization: Bearer $token" -o "$archive" "$url"; then rm -rf "$work"; return 1; fi
  mkdir -p "$work/x"
  tar -xzf "$archive" -C "$work/x" 2>/dev/null || { rm -rf "$work"; return 1; }
  local dylib
  dylib=$(find "$work/x" -name 'libusb-1.0.dylib' -type f 2>/dev/null | head -1)
  if [ -n "$dylib" ]; then
    mkdir -p "$out"
    cp -f "$dylib" "$out/libusb-1.0.dylib"
    rm -rf "$work"
    info "macos-$arch: Homebrew bottle ($bottle)"
    return 0
  fi
  rm -rf "$work"
  warn "macos-$arch: bottle extraction failed"
  return 1
}

target_linux_deb() { # arch libdir_sub
  local arch="$1"
  local libdir="$2"
  local out="$OUTPUT_DIR/linux-$arch"
  local pool="pool/main/l/libusb-1.0" base url file work

  # 1) discover the version from the pool listing (mirror-aware)
  for base in "$(ubuntu_base)" "$(debian_base)"; do
    file=$(curl -fsSL $SSL_NO_REVOKE --max-time 30 "$base/$pool/" 2>/dev/null \
      | grep -oE "libusb-1\.0-0_[^\"<]*_${arch}\.deb" | sort -u | tail -1)
    [ -n "$file" ] && break
  done

  # 2) fall back to candidate versions
  if [ -z "$file" ]; then
    for ver in "${DEB_CANDIDATE_VERSIONS[@]}"; do
      for base in "$(ubuntu_base)" "$(debian_base)"; do
        f="libusb-1.0-0_${ver}_${arch}.deb"
        if curl -fsSI $SSL_NO_REVOKE --max-time 20 "$base/$pool/$f" >/dev/null 2>&1; then file="$f"; break 2; fi
      done
    done
  fi
  [ -n "$file" ] || { warn "linux-$arch: no .deb found (Ubuntu/Debian pool + candidates)"; return 1; }

  work=$(mktemp -d)
  for base in "$(ubuntu_base)" "$(debian_base)"; do
    url="$base/$pool/$file"
    if fetch_to "$url" "$work/pkg.deb" 2>/dev/null; then
      if extract_deb_so "$work/pkg.deb" "$out" "$libdir"; then
        rm -rf "$work"
        info "linux-$arch: $file"
        return 0
      fi
    fi
  done
  rm -rf "$work"
  warn "linux-$arch: download/extract failed for $file"
  return 1
}

target_linux_loong64() {
  # loong64 is not in Ubuntu's classic archive; fetch from Debian (trixie/sid).
  # AOSC OS also ships loongarch64 libusb debs - see
  #   https://packages.aosc.io/ (aosc-repo pool for loongarch64)
  # if Debian is unreachable.
  target_linux_deb "loong64" "lib/loongarch64-linux-gnu"
}

# ---- main ------------------------------------------------------------------

usage() {
  sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'
  echo
  echo "  --target <all|win-x64|win-arm64|macos-x64|macos-arm64|linux-x64|linux-arm64|linux-riscv64|linux-loong64>"
  echo "  --output <dir>          (default: native)"
  echo "  --mirror <none|tuna|ustc>  (default: none)"
  echo "  --github-mirror <url>   proxy for the official release .7z"
  echo "  --ghcr-mirror <url>     proxy for Homebrew bottles"
  echo "  --dry-run               print the plan without downloading"
  echo "  -v, --verbose"
}

DRY_RUN=0
while [ $# -gt 0 ]; do
  case "$1" in
    --target)      TARGETS="${2:-all}"; shift 2 ;;
    --output)      OUTPUT_DIR="${2:-native}"; shift 2 ;;
    --mirror)      MIRROR="${2:-none}"; shift 2 ;;
    --github-mirror) GITHUB_MIRROR="${2:-}"; shift 2 ;;
    --ghcr-mirror) GHCR_MIRROR="${2:-}"; shift 2 ;;
    --dry-run)     DRY_RUN=1; shift ;;
    -v|--verbose)  VERBOSE=1; shift ;;
    -h|--help)     usage; exit 0 ;;
    *) die "unknown option: $1 (see --help)" ;;
  esac
done

run_target() { # fn target_name [args...]
  local fn="$1" name="$2"
  shift 2
  if [ "$DRY_RUN" -eq 1 ]; then
    info "plan: $name -> $OUTPUT_DIR/$name/libusb-1.0.*"
    return 0
  fi
  info "fetching $name"
  "$fn" "$@"
}

case "$TARGETS" in
  all)
    run_target target_win "win-x64" x64
    run_target target_win "win-arm64" arm64
    run_target target_macos "macos-x64" x64
    run_target target_macos "macos-arm64" arm64
    run_target target_linux_deb "linux-x64" x64 "lib/x86_64-linux-gnu"
    run_target target_linux_deb "linux-arm64" arm64 "lib/aarch64-linux-gnu"
    run_target target_linux_deb "linux-riscv64" riscv64 "lib/riscv64-linux-gnu"
    run_target target_linux_loong64 "linux-loong64"
    ;;
  win-x64)  run_target target_win "win-x64" x64 ;;
  win-arm64) run_target target_win "win-arm64" arm64 ;;
  macos-x64) run_target target_macos "macos-x64" x64 ;;
  macos-arm64) run_target target_macos "macos-arm64" arm64 ;;
  linux-x64) run_target target_linux_deb "linux-x64" x64 "lib/x86_64-linux-gnu" ;;
  linux-arm64) run_target target_linux_deb "linux-arm64" arm64 "lib/aarch64-linux-gnu" ;;
  linux-riscv64) run_target target_linux_deb "linux-riscv64" riscv64 "lib/riscv64-linux-gnu" ;;
  linux-loong64) run_target target_linux_loong64 "linux-loong64" ;;
  *) die "unknown target: $TARGETS" ;;
esac

info "done. artifacts under: $OUTPUT_DIR/"
