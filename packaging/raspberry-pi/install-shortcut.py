#!/usr/bin/env python3
"""Install per-user desktop/menu shortcuts without root permissions."""
from pathlib import Path
import argparse
import datetime
import os
import shlex
import shutil
import subprocess
import sys


def entry_value(value):
    return str(value).replace("\\", "\\\\").replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t")


def exec_argument(value):
    value = str(value).replace("%", "%%")
    for character in ('\\', '"', '`', '$'):
        value = value.replace(character, '\\' + character)
    return entry_value('"' + value + '"')


def desktop_entry(app_dir):
    return (
        "[Desktop Entry]\nType=Application\nVersion=1.0\n"
        "Name=CurtantControl\nComment=Photobooth curtain control and camera preview\n"
        f"Exec=/bin/sh {exec_argument(app_dir / 'start.sh')}\n"
        f"Icon={entry_value(app_dir / 'curtant-control.svg')}\n"
        f"Path={entry_value(app_dir)}\n"
        "Terminal=false\nCategories=Utility;\nStartupNotify=false\n"
    )


def related_entry(text):
    section = ''
    for line in text.splitlines():
        if line.startswith('['):
            section = line.strip()
        if section == '[Desktop Entry]' and line.startswith('Exec='):
            try:
                args = shlex.split(line[5:])
            except ValueError:
                return False
            return bool(args) and (Path(args[0]).name == 'CurtantControl'
                or (len(args) >= 2 and args[0] in ('sh','/bin/sh') and Path(args[1]).name == 'start.sh'
                    and 'CurtantControl' in args[1])
                or ('/home/pi/r1/key_check_new.py' in args and '--start-app' in args))
    return False


def keep_one_shortcut(directory, filename, app_dir, backup_dir):
    directory.mkdir(parents=True, exist_ok=True)
    canonical = directory / filename
    desired = desktop_entry(app_dir)
    if canonical.exists():
        original = canonical.read_text(encoding='utf-8')
        if not related_entry(original):
            raise ValueError('Shortcut filename belongs to another command: ' + str(canonical))
        # Keep the existing Starting helper configured by keycheck.
        if '/home/pi/r1/key_check_new.py' in original and '--start-app' in original:
            desired = original
        if original != desired:
            backup_dir.mkdir(parents=True, exist_ok=True)
            shutil.copy2(str(canonical), str(backup_dir / (filename + '.' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f'))))
            canonical.write_text(desired, encoding='utf-8')
    else:
        canonical.write_text(desired, encoding='utf-8')
    canonical.chmod(0o755)
    # Old installers left executable .desktop.bak-* files on the Desktop/menu.
    # Move matching copies outside these folders; unrelated launchers stay put.
    for candidate in directory.iterdir():
        if candidate == canonical or not candidate.is_file() or '.desktop' not in candidate.name:
            continue
        try:
            matches = related_entry(candidate.read_text(encoding='utf-8'))
        except (UnicodeError, OSError):
            continue
        if matches:
            backup_dir.mkdir(parents=True, exist_ok=True)
            destination = backup_dir / (candidate.name + '.' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
            shutil.move(str(candidate), str(destination))
            print('Moved duplicate shortcut to backup: ' + str(destination))
    return canonical


def main():
    if not sys.platform.startswith("linux"):
        raise SystemExit("Run this installer on the Raspberry Pi, not on Windows.")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app-dir', type=Path)
    args = parser.parse_args()
    app_dir = (args.app_dir or Path(__file__).resolve().parent).resolve()
    for name in ("start.sh", "CurtantControl", "curtant-control.svg"):
        if not (app_dir / name).is_file():
            raise SystemExit(f"Missing {name}. Extract the complete release first.")

    data_dir = Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share")))
    applications = data_dir / "applications"
    applications.mkdir(parents=True, exist_ok=True)
    backup_dir = data_dir / 'CurtantControl/shortcut-backups'
    menu_entry = keep_one_shortcut(applications, 'com.curtantcontrol.Photobooth.desktop', app_dir, backup_dir)

    desktop = Path.home() / "Desktop"
    if shutil.which("xdg-user-dir"):
        result = subprocess.run(["xdg-user-dir", "DESKTOP"], capture_output=True, text=True, check=False)
        if result.returncode == 0 and result.stdout.strip():
            desktop = Path(result.stdout.strip())
    if desktop == Path.home():
        print(f"Desktop folder is disabled. Application-menu shortcut installed: {menu_entry}")
        return
    desktop.mkdir(parents=True, exist_ok=True)
    desktop_entry_path = keep_one_shortcut(desktop, 'CurtantControl.desktop', app_dir, backup_dir)
    print(f"Installed desktop shortcut: {desktop_entry_path}")
    print(f"Installed application-menu shortcut: {menu_entry}")
    print("Double-click CurtantControl on the desktop. If prompted, choose Execute/Allow Launching.")
    print("Keep the application folder in this location; rerun this installer after moving/upgrading it.")


if __name__ == "__main__":
    main()
