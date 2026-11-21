#!/usr/bin/env python3
"""Disable the old photobooth Chromium kiosk startup and close its kiosk processes."""
import argparse
import datetime
import os
from pathlib import Path
import re
import signal
import subprocess
import sys


def legacy_command(command):
    # Match the known old kiosk launchers, or an explicit Chromium kiosk command.
    if re.search(r'/home/pi/r1/(?:cr_[\w.\-]+|camrazr-client)\.sh(?:[\s\'";]|$)', command):
        return True
    return bool(re.search(r'\b(?:chromium-browser|chromium|google-chrome)\b', command)
                and re.search(r'--kiosk(?:[\s\'";]|$)', command))


def legacy_startup_command(command):
    return legacy_command(command) or bool(re.search(
        r'/home/pi/r1/(?:key_check1\.py|model_1/app_model_1\.py)(?:[\s\'";]|$)', command))


def rewrite_lines(text):
    lines = text.splitlines(keepends=True)
    changed = False
    for i, line in enumerate(lines):
        if line.strip() and not line.lstrip().startswith('#') and legacy_startup_command(line):
            lines[i] = '# CurtantControl disabled old kiosk: ' + line
            changed = True
    return ''.join(lines) if changed else None


def replace_with_backup(path, revised):
    stamp = datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    backup = path.with_name(path.name + '.before-curtant-kiosk-' + stamp)
    import shutil
    shutil.copy2(str(path), str(backup))
    temporary = path.with_name('.' + path.name + '.curtant-' + stamp)
    try:
        temporary.write_bytes(revised.encode('utf-8'))
        temporary.chmod(path.stat().st_mode & 0o777)
        os.replace(str(temporary), str(path))
    finally:
        if temporary.exists():
            temporary.unlink()
    print('Disabled old kiosk startup: %s (backup: %s)' % (path, backup), flush=True)


def disable_user_startup(home):
    for path in sorted((home / '.config/autostart').glob('*.desktop')):
        if path.is_symlink():
            continue
        text = path.read_text(encoding='utf-8')
        section = ''
        found = False
        for line in text.splitlines():
            if line.startswith('['):
                section = line.strip()
            if section == '[Desktop Entry]' and line.startswith('Exec=') and legacy_startup_command(line[5:]):
                found = True
        if found:
            lines = text.splitlines()
            # Hidden=true overrides a system desktop entry with the same filename too.
            start = lines.index('[Desktop Entry]') + 1
            end = next((i for i in range(start, len(lines)) if lines[i].startswith('[')), len(lines))
            updated = lines[:start] + [line for line in lines[start:end]
                       if not line.startswith(('Hidden=', 'X-GNOME-Autostart-enabled='))]
            updated += ['Hidden=true', 'X-GNOME-Autostart-enabled=false'] + lines[end:]
            revised = '\n'.join(updated) + '\n'
            if revised != text:
                replace_with_backup(path, revised)
    for path in (home / '.config/lxsession').glob('*/autostart'):
        if path.is_file() and not path.is_symlink():
            revised = rewrite_lines(path.read_text(encoding='utf-8'))
            if revised is not None:
                replace_with_backup(path, revised)


def kiosk_process(args):
    return bool(args and Path(args[0]).name in ('chromium', 'chromium-browser', 'google-chrome', 'chrome')
                and '--kiosk' in args and not any(a.startswith('--type=') for a in args))


def close_kiosks(proc=Path('/proc')):
    for entry in proc.iterdir():
        if not entry.name.isdigit():
            continue
        try:
            if entry.stat().st_uid != os.getuid():
                continue
            args = [os.fsdecode(a) for a in (entry / 'cmdline').read_bytes().split(b'\0') if a]
            if kiosk_process(args):
                os.kill(int(entry.name), signal.SIGTERM)
                print('Closed old Chromium kiosk PID ' + entry.name, flush=True)
        except ProcessLookupError:
            pass
        except FileNotFoundError:
            pass


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--system-startup', action='store_true', help=argparse.SUPPRESS)
    args = parser.parse_args()
    if not sys.platform.startswith('linux'):
        raise ValueError('Run on the Raspberry Pi.')
    if args.system_startup:
        if os.geteuid() != 0:
            raise ValueError('System startup migration requires sudo.')
        subprocess.run(['systemctl', 'daemon-reload'], check=True, timeout=45)
        state = subprocess.run(['systemctl','show','--property=LoadState','--value','camrazr-legacy-backend.service'],
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True, timeout=45)
        if state.stdout.strip() != 'not-found':
            if state.returncode != 0 or not state.stdout.strip():
                raise ValueError('Cannot inspect legacy backend service: ' + state.stderr)
            subprocess.run(['systemctl','disable','--now','camrazr-legacy-backend.service'], check=True, timeout=45)
        path = Path('/etc/rc.local')
        if path.is_file() and not path.is_symlink():
            revised = rewrite_lines(path.read_text(encoding='utf-8'))
            if revised is not None:
                replace_with_backup(path, revised)
        return
    if os.geteuid() == 0 or subprocess.check_output(['id', '-un'], universal_newlines=True).strip() != 'pi':
        raise ValueError('Run as pi without sudo: python3 legacy_kiosk.py')
    disable_user_startup(Path.home())
    subprocess.run(['sudo', '/usr/bin/python3', str(Path(__file__).resolve()), '--system-startup'], check=True)
    close_kiosks()
    print('Old kiosk/backend startup disabled. Ordinary browser windows and legacy Python files are preserved.')


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        sys.exit('KIOSK MIGRATION FAILED: ' + str(error))
