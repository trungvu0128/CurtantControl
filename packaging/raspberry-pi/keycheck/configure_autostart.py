#!/usr/bin/env python3
"""Route existing direct CurtantControl desktop autostart through its startup UI."""
from datetime import datetime
from pathlib import Path
import shlex
import subprocess
import os
import shutil

APP = '/home/pi/.local/share/CurtantControl/app/CurtantControl'
LAUNCH = '/usr/bin/python3 /home/pi/r1/key_check_new.py --start-app'


def is_our_command(value):
    try:
        parts = shlex.split(value)
    except ValueError:
        return False
    return bool(parts) and (parts[0] == APP or parts == shlex.split(LAUNCH)
        or (len(parts) == 2 and parts[0] in ('/bin/sh', 'sh') and parts[1] == str(Path(APP).parent / 'start.sh')))


def rewrite_desktop(text):
    section = ''
    lines = text.splitlines()
    changed = False
    for index, line in enumerate(lines):
        if line.startswith('['):
            section = line.strip()
        if section == '[Desktop Entry]' and line.startswith('Exec=') and is_our_command(line[5:]):
            lines[index] = 'Exec=' + LAUNCH
            changed = True
    return '\n'.join(lines) + '\n' if changed else None


def configure(directory):
    directory.mkdir(parents=True, exist_ok=True)
    updated = []
    stamp = datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    for path in sorted(directory.glob('*.desktop')):
        original = path.read_text(encoding='utf-8')
        revised = rewrite_desktop(original)
        if revised is not None:
            if revised != original:
                path.with_name(path.name + '.bak-' + stamp).write_bytes(original.encode('utf-8'))
                path.write_bytes(revised.encode('utf-8'))
            updated.append(path)
    if not updated:
        path = directory / 'curtantcontrol-starting.desktop'
        if path.exists():
            raise RuntimeError('Autostart filename already contains an unrelated entry; inspect ' + str(path))
        path.write_bytes(('''[Desktop Entry]
Type=Application
Name=CurtantControl
Exec=''' + LAUNCH + '''
Terminal=false
X-GNOME-Autostart-enabled=true
''').encode('utf-8'))
        updated.append(path)
    for path in updated:
        print('Startup indicator configured: ' + str(path))


def configure_shortcuts(home=None, desktop=None):
    home = home or Path.home()
    data = Path(os.environ.get('XDG_DATA_HOME', str(home / '.local/share')))
    if desktop is None:
        result = subprocess.run(['xdg-user-dir', 'DESKTOP'], capture_output=True, text=True, check=False)
        desktop = Path(result.stdout.strip()) if result.returncode == 0 and result.stdout.strip() else home / 'Desktop'
    paths = [data / 'applications/com.curtantcontrol.Photobooth.desktop']
    if desktop != home:
        paths.append(desktop / 'CurtantControl.desktop')
    stamp = datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    for path in paths:
        if not path.is_file():
            continue
        original = path.read_text(encoding='utf-8')
        revised = rewrite_desktop(original)
        if revised is not None and revised != original:
            backup_dir = data / 'CurtantControl/shortcut-backups'
            backup_dir.mkdir(parents=True, exist_ok=True)
            shutil.copy2(str(path), str(backup_dir / (path.name + '.bak-' + stamp)))
            path.write_bytes(revised.encode('utf-8'))
            print('Desktop/menu startup indicator configured: ' + str(path))


if __name__ == '__main__':
    configure(Path.home() / '.config' / 'autostart')
    configure_shortcuts()
