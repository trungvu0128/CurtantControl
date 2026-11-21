#!/bin/sh
set -eu
cd -- "$(dirname -- "$0")"
test "$(id -un)" = pi || { echo 'Run as pi without sudo.'; exit 1; }
app=/home/pi/.local/share/CurtantControl/app
test -f "$app/CurtantControl" || { echo 'Installed app not found.'; exit 1; }
backup=/home/pi/.local/share/CurtantControl/shortcut-backups
mkdir -p "$backup"
if test -f "$app/install-shortcut.py"; then
    cp -p "$app/install-shortcut.py" "$backup/install-shortcut.py.$(date +%Y%m%d-%H%M%S)"
fi
cp install-shortcut.py "$app/install-shortcut.py"
python3 "$app/install-shortcut.py"
python3 -c 'from configure_autostart import configure_shortcuts; configure_shortcuts()'
echo 'Done. One CurtantControl shortcut on Desktop; backups kept outside Desktop.'
