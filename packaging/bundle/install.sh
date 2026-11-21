#!/bin/sh
set -eu
cd -- "$(dirname -- "$0")"
if [ "$(id -u)" = 0 ]; then
    echo "Run as your desktop user, without sudo. Setup requests sudo only for OS packages and groups." >&2
    exit 1
fi
if ! command -v python3 >/dev/null 2>&1; then
    echo "Python 3 is required. Install it with: sudo apt update && sudo apt install python3" >&2
    exit 1
fi
exec python3 setup.py "$@"
