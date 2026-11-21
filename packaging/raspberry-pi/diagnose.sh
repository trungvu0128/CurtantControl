#!/bin/sh
# Read-only: does not toggle GPIOs, open cameras, or call service mutation routes.
set -u
cd -- "$(dirname -- "$0")"
echo '=== CurtantControl diagnostics ==='
cat VERSION.txt
cat /etc/os-release
uname -m
getconf LONG_BIT
getconf GNU_LIBC_VERSION
id
printf 'DISPLAY=%s SESSION=%s WAYLAND=%s\n' "${DISPLAY:-}" "${XDG_SESSION_TYPE:-}" "${WAYLAND_DISPLAY:-}"
echo '=== GPIO devices ==='
ls -l /dev/gpiochip* /dev/gpiomem /dev/video* 2>/dev/null || true
echo '=== Dependencies ==='
dpkg-query -W 'libgpiod*' 'gpiod' 'ffmpeg' 'v4l-utils' 2>/dev/null || true
for file in CurtantControl libcoreclr.so libSkiaSharp.so libHarfBuzzSharp.so; do
    echo "--- $file"
    ldd "./$file" 2>&1 || true
done
echo '=== Camera tools ==='
command -v ffmpeg raspivid rpicam-vid libcamera-vid v4l2-ctl || true
if command -v v4l2-ctl >/dev/null 2>&1; then v4l2-ctl --list-devices 2>&1 || true; fi
if command -v vcgencmd >/dev/null 2>&1; then vcgencmd get_camera 2>&1 || true; fi
echo 'Configuration content and process arguments omitted to avoid exposing private URLs.'
echo 'GPIO readings are command signals, not physical curtain position feedback.'
