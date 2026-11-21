#!/bin/sh
set -eu
cd -- "$(dirname -- "$0")"
case "$(uname -m)" in
    aarch64|armv7l|armv8l) ;;
    *) echo "This release requires an ARM Raspberry Pi OS." >&2; exit 1 ;;
esac
if [ "$(getconf LONG_BIT)" != "@BITS@" ]; then
    echo "This release requires @BITS@-bit OS libraries. Use the package matching getconf LONG_BIT, not just uname -m." >&2
    exit 1
fi
if [ -z "${DISPLAY:-}" ]; then
    # Check an existing local X server using this user's normal authentication.
    # Do not assume that DISPLAY=:0 exists or that this user may access it.
    if command -v xdpyinfo >/dev/null 2>&1; then
        for socket in /tmp/.X11-unix/X[0-9]*; do
            [ -S "$socket" ] || continue
            candidate=":${socket##*/X}"
            if DISPLAY="$candidate" xdpyinfo >/dev/null 2>&1; then
                DISPLAY="$candidate"
                export DISPLAY
                break
            fi
        done
    fi
    if [ -z "${DISPLAY:-}" ]; then
        echo "No accessible X11 display. Open a terminal in the Pi desktop as its logged-in user. Install x11-utils for automatic local display detection." >&2
        exit 1
    fi
fi
if [ ! -x ./CurtantControl ]; then
    chmod +x ./CurtantControl
fi
exec ./CurtantControl "$@"
