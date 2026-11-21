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
python3 setup.py "$@"
# Manual post-install cleanup. The app updater runs this after restarting.
for arg do
    if [ "$arg" = '--check' ]; then
        exit 0
    fi
done
if [ -f .rollback-target ]; then
    helper="$HOME/.local/share/CurtantControl/app/apply-update.py"
    if [ -f "$helper" ]; then
        python3 "$helper" --clean-install "$PWD" || echo 'Installation completed. Some installer files were kept.' >&2
    fi
fi
