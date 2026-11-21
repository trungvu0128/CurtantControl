#!/usr/bin/env python3
"""Save/restore the current Pi app, legacy Python app, buttons and startup files."""
import argparse
import datetime
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import signal
import subprocess
import sys
import tarfile
import tempfile
import time
import threading
from contextlib import contextmanager

PATHS = (
    'home/pi/r1',
    'home/pi/.local/share/CurtantControl/app',
    'home/pi/.config/autostart',
    'home/pi/.config/lxsession/LXDE-pi',
    'home/pi/.local/share/applications/com.curtantcontrol.Photobooth.desktop',
    'home/pi/Desktop/CurtantControl.desktop',
    'etc/systemd/system/keycheck.service',
    'etc/systemd/system/keycheck.service.d',
    'etc/systemd/system/camrazr-legacy-backend.service',
    'etc/rc.local',
)
STATE = 'home/pi/.local/share/CurtantControl'


@contextmanager
def progress(label):
    print(label + ' ...', flush=True)
    stopped = threading.Event()
    started = time.monotonic()
    def heartbeat():
        while not stopped.wait(5):
            print('%s ... %ds (still working)' % (label, time.monotonic() - started), flush=True)
    worker = threading.Thread(target=heartbeat, daemon=True)
    worker.start()
    try:
        yield
    finally:
        stopped.set()
        worker.join()


def service(*args, **kwargs):
    print('Service: systemctl ' + ' '.join(args), flush=True)
    try:
        return subprocess.run(['systemctl'] + list(args), timeout=45, **kwargs)
    except subprocess.TimeoutExpired:
        raise ValueError('Service command exceeded 45 seconds: systemctl ' + ' '.join(args)
                         + '. No further restore steps were performed. Check journalctl -u keycheck.service.')


def legacy_files(names):
    required = ['home/pi/r1/key_check1.py', 'home/pi/r1/model_1/app_model_1.py']
    for name in required:
        if name not in names:
            raise ValueError('Missing original Chromium system file: /' + name)
    launchers = [n for n in names if PurePosixPath(n).parent == PurePosixPath('home/pi/r1')
                 and (PurePosixPath(n).name.startswith('cr_') or PurePosixPath(n).name == 'camrazr-client.sh')
                 and n.endswith('.sh')]
    preferred = 'home/pi/r1/cr_192_168_137_3.sh'
    if preferred in launchers:
        return preferred
    if len(launchers) != 1:
        raise ValueError('Cannot identify the old Chromium launcher. Keep the original cr_*.sh in /home/pi/r1.')
    return launchers[0]


def configure_legacy(root, launcher):
    home = root / 'home/pi'
    # Disable only CurtantControl launch entries, preserving unrelated desktop apps.
    directory = home / '.config/autostart'
    directory.mkdir(parents=True, exist_ok=True)
    for path in directory.glob('*.desktop'):
        text = path.read_text(encoding='utf-8')
        if any(line.startswith('Exec=') and any(token in line for token in (
                '/CurtantControl/app/', 'key_check_new.py', 'key_check1.py', 'app_model_1.py'))
               for line in text.splitlines()):
            path.rename(path.with_name(path.name + '.disabled-legacy-' + stamp()))
    # Existing old kiosk entries remain in place but are hidden, to avoid duplicate
    # startup alongside the canonical entry created below.
    for path in directory.glob('*.desktop'):
        text = path.read_text(encoding='utf-8')
        if any(line.startswith('Exec=') and ('/home/pi/r1/cr_' in line or
               '/home/pi/r1/camrazr-client.sh' in line or
               ('--kiosk' in line and 'chrom' in line)) for line in text.splitlines()):
            path.rename(path.with_name(path.name + '.disabled-legacy-' + stamp()))
    desktop = directory / 'camrazr-legacy-restored.desktop'
    desktop.write_text('[Desktop Entry]\nType=Application\nName=Camera Razr legacy Chromium\n'
                       'Exec=/bin/sh /' + launcher + '\nTerminal=false\nX-GNOME-Autostart-enabled=true\n', encoding='utf-8')
    # Remove duplicate app/kiosk/keycheck invocations from rc.local and LXDE.
    startup = [root / 'etc/rc.local'] + list((home / '.config/lxsession').glob('*/autostart'))
    for path in startup:
        if not path.is_file():
            continue
        text = path.read_text(encoding='utf-8')
        lines = []
        for line in text.splitlines(keepends=True):
            if not line.lstrip().startswith('#') and any(token in line for token in (
                    '/CurtantControl/app/', 'key_check_new.py', 'key_check1.py',
                    '/home/pi/r1/cr_', '/home/pi/r1/camrazr-client.sh', 'app_model_1.py')):
                line = '# CurtantControl legacy rollback manages startup: ' + line
            lines.append(line)
        if ''.join(lines) != text:
            path.write_text(''.join(lines), encoding='utf-8')
    unit = root / 'etc/systemd/system/keycheck.service'
    unit.parent.mkdir(parents=True, exist_ok=True)
    dropins = unit.with_name('keycheck.service.d')
    if dropins.exists():
        dropins.rename(dropins.with_name(dropins.name + '.disabled-legacy-' + stamp()))
    if unit.is_symlink():
        unit.unlink()
    unit.write_text('[Unit]\nDescription=Legacy Chromium keycheck\nAfter=display-manager.service\n'
                    '[Service]\nType=simple\nUser=pi\nGroup=pi\nWorkingDirectory=/home/pi/r1\n'
                    'Environment=DISPLAY=:0\nEnvironment=XAUTHORITY=/home/pi/.Xauthority\n'
                    'ExecStart=/usr/bin/python3 -u /home/pi/r1/key_check1.py\nRestart=on-failure\nRestartSec=3\n'
                    '[Install]\nWantedBy=graphical.target\n', encoding='utf-8')
    # A separate service owns the old Python backend; the browser launcher stays original.
    backend = unit.with_name('camrazr-legacy-backend.service')
    backend.write_text('[Unit]\nDescription=Legacy Camera Razr Python backend\nAfter=network.target\n'
                       '[Service]\nUser=pi\nWorkingDirectory=/home/pi/r1/model_1\n'
                       'Environment=DISPLAY=:0\nEnvironment=XAUTHORITY=/home/pi/.Xauthority\n'
                       'ExecStart=/usr/bin/python3 /home/pi/r1/model_1/app_model_1.py\n'
                       'Restart=on-failure\nRestartSec=3\n[Install]\nWantedBy=graphical.target\n', encoding='utf-8')
    return desktop, backend


def stop_current_apps(uid, proc=Path('/proc')):
    pending = []
    for entry in proc.iterdir():
        if not entry.name.isdigit():
            continue
        try:
            if entry.stat().st_uid not in (0, uid):
                continue
            args = [os.fsdecode(a) for a in (entry / 'cmdline').read_bytes().split(b'\0') if a]
            is_app = args and Path(args[0]).name == 'CurtantControl'
            is_python = any(a in ('/home/pi/r1/key_check_new.py', '/home/pi/r1/key_check1.py',
                                  '/home/pi/r1/model_1/app_model_1.py') for a in args)
            if is_app or is_python:
                os.kill(int(entry.name), signal.SIGTERM)
                pending.append(entry)
        except (ProcessLookupError, FileNotFoundError):
            pass
    deadline = time.monotonic() + 8
    while pending and time.monotonic() < deadline:
        alive = []
        for p in pending:
            try:
                if ') Z' not in (p / 'stat').read_text():
                    alive.append(p)
            except FileNotFoundError:
                pass
        pending = alive
        if pending:
            time.sleep(0.1)
    if pending:
        raise ValueError('Application did not stop. Close it before retrying rollback.')


def rollback_legacy(root, backup, account):
    with progress('Checking original Chromium files' + (': ' + str(backup) if backup is not None else ' in /home/pi/r1')):
        if backup is not None:
            with tarfile.open(str(backup), 'r:gz') as archive:
                validate(archive)
                launcher = legacy_files([m.name for m in archive.getmembers() if m.isfile()])
        else:
            launcher = legacy_files([str(p.relative_to(root)).replace(os.sep, '/')
                                     for p in (root / 'home/pi/r1').rglob('*') if p.is_file()])
    # A prior install may have changed the unit/drop-ins without reloading yet.
    service('daemon-reload', check=True)
    enabled = service('is-enabled', '--quiet', 'keycheck.service').returncode == 0
    stop_existing_services()
    with progress('Stopping current app'):
        stop_current_apps(account.pw_uid)
    safety = root / ('home/pi/CurtantControl-Before-Legacy-' + stamp() + '.tar.gz')
    save(root, safety, enabled)
    os.chown(str(safety), account.pw_uid, account.pw_gid)
    safety.chmod(0o600)
    if backup is not None:
        restore(root, backup)
    print('Restoring Chromium startup and original keycheck configuration ...', flush=True)
    desktop, backend = configure_legacy(root, launcher)
    # Files created as root must remain accessible to the desktop account.
    desktop.chmod(0o644)
    os.chown(str(desktop), account.pw_uid, account.pw_gid)
    for directory in (desktop.parent, desktop.parent.parent):
        os.chown(str(directory), account.pw_uid, account.pw_gid)
        directory.chmod(0o755)
    for p in (backend, root / 'etc/systemd/system/keycheck.service'):
        p.chmod(0o644)
    service('daemon-reload', check=True)
    service('enable', 'keycheck.service', 'camrazr-legacy-backend.service', check=True)
    service('start', 'camrazr-legacy-backend.service', 'keycheck.service', check=True)
    env = ['DISPLAY=' + os.environ.get('DISPLAY', ':0'), 'XAUTHORITY=/home/pi/.Xauthority', 'HOME=/home/pi']
    log = open(str(root / 'home/pi/camrazr-legacy-launch.log'), 'ab')
    subprocess.Popen(['runuser', '-u', 'pi', '--', 'env'] + env + ['/bin/sh', '/' + launcher],
                     cwd=str(root / 'home/pi'), stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    log.close()
    print('Legacy Chromium launcher started with ORIGINAL key_check1.py and Python backend.')
    print('Legacy startup enabled; current setup saved: ' + str(safety))


def stop_existing_services():
    for name in ('keycheck.service', 'camrazr-legacy-backend.service'):
        state = service('show', '--property=LoadState', '--value', name,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True)
        if state.stdout.strip() == 'not-found':
            continue
        if state.returncode != 0 or not state.stdout.strip():
            raise ValueError('Cannot inspect service %s: %s' % (name, state.stderr.strip()))
        service('stop', name, check=True)


def prepare_install(account):
    service('daemon-reload', check=True)
    enabled = service('is-enabled', '--quiet', 'keycheck.service').returncode == 0
    stop_existing_services()
    with progress('Stopping old/new app before installation'):
        stop_current_apps(account.pw_uid)
    return enabled


def targets(root):
    # Capture user configuration/state without recursively copying app-backup-*.
    return list(PATHS) + sorted(str(p.relative_to(root)).replace(os.sep, '/')
                               for p in (root / STATE).glob('*.json'))


def running_apps(proc=Path('/proc')):
    for p in proc.glob('[0-9]*/cmdline'):
        try:
            args = p.read_bytes().split(b'\0')
            if args and (Path(os.fsdecode(args[0])).name == 'CurtantControl'
                         or any(Path(os.fsdecode(a)).name == 'app_model_1.py' for a in args)):
                return True
        except OSError:
            pass
    return False


def stamp():
    return datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')


def save(root, output, enabled):
    names = targets(root)
    present = [n for n in names if os.path.lexists(str(root / n))]
    metadata = {'format': 1, 'paths': names, 'present': present,
                'keycheck_enabled': enabled}
    # Preserve symlinks rather than following a link outside the backed-up tree.
    with progress('Saving current files to ' + str(output)), tarfile.open(str(output), 'x:gz', dereference=False) as archive:
        import io
        data = json.dumps(metadata).encode('utf-8')
        member = tarfile.TarInfo('rollback.json')
        member.size = len(data)
        archive.addfile(member, io.BytesIO(data))
        for name in present:
            archive.add(str(root / name), arcname=name)
    return output


def validate(archive):
    metadata = json.load(archive.extractfile('rollback.json'))
    names = metadata.get('paths', [])
    if metadata.get('format') != 1 or not names or len(names) != len(set(names)):
        raise ValueError('Invalid backup metadata.')
    for name in names:
        p = PurePosixPath(name)
        if name not in PATHS and not (str(p.parent) == STATE and p.suffix == '.json'):
            raise ValueError('Unexpected backup target: ' + name)
    present = metadata.get('present', [])
    if not set(present).issubset(names):
        raise ValueError('Invalid present-path list.')
    members = archive.getmembers()
    links = {m.name for m in members if m.issym() or m.islnk()}
    seen = set()
    for m in members:
        p = PurePosixPath(m.name)
        if m.name in seen or p.is_absolute() or '..' in p.parts:
            raise ValueError('Unsafe or duplicate archive path.')
        seen.add(m.name)
        if m.name == 'rollback.json':
            continue
        if not any(m.name == n or m.name.startswith(n + '/') for n in present):
            raise ValueError('Archive entry outside backup targets.')
        if not (m.isfile() or m.isdir() or m.issym() or m.islnk()):
            raise ValueError('Unsupported archive entry.')
        if any(str(parent) in links for parent in p.parents):
            raise ValueError('Archive entry traverses a link.')
        if m.islnk():
            source = archive.getmember(m.linkname)
            if not source.isfile():
                raise ValueError('Invalid hard link.')
    if any(n not in seen for n in present):
        raise ValueError('Backup is incomplete.')
    return metadata


def restore(root, backup):
    with progress('Extracting and restoring ' + str(backup)), tarfile.open(str(backup), 'r:gz') as archive:
        metadata = validate(archive)
        # Fully decompress/validate before moving any live file. Links go last.
        with tempfile.TemporaryDirectory(prefix='curtant-rollback-') as temp:
            stage = Path(temp)
            members = [m for m in archive.getmembers() if m.name != 'rollback.json']
            for m in sorted(members, key=lambda m: m.issym() or m.islnk()):
                archive.extract(m, str(stage))
            suffix = '.before-rollback-' + stamp()
            # Also move newly introduced JSON state aside when absent in the snapshot.
            for name in sorted(set(metadata['paths']) | set(targets(root))):
                destination = root / name
                if destination.parent.resolve() != destination.parent.absolute():
                    raise ValueError('Live parent is a symlink: ' + str(destination.parent))
            moved = []
            installed = []
            try:
                for name in sorted(set(metadata['paths']) | set(targets(root))):
                    destination = root / name
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    if os.path.lexists(str(destination)):
                        previous = destination.with_name(destination.name + suffix)
                        destination.rename(previous)
                        moved.append((destination, previous))
                    if name in metadata['present']:
                        shutil.move(str(stage / name), str(destination))
                        installed.append(destination)
            except Exception:
                for destination in reversed(installed):
                    if destination.is_dir() and not destination.is_symlink():
                        shutil.rmtree(str(destination))
                    else:
                        destination.unlink()
                for destination, previous in reversed(moved):
                    previous.rename(destination)
                raise
    return metadata


def main():
    print('CurtantControl rollback tool starting ...', flush=True)
    import pwd
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['backup', 'backup-install', 'stop-apps', 'restore', 'check', 'legacy'])
    parser.add_argument('archive', nargs='?')
    args = parser.parse_args()
    if not sys.platform.startswith('linux'):
        raise ValueError('Run this file on the Raspberry Pi.')
    if args.action in ('restore', 'check') and not args.archive:
        parser.error('Specify the backup .tar.gz file.')
    backup = Path(args.archive).resolve() if args.archive else (None if args.action in ('legacy', 'stop-apps') else Path('/home/pi/CurtantControl-Rollback-' + stamp() + '.tar.gz'))
    if args.action == 'check':
        with tarfile.open(str(backup), 'r:gz') as archive:
            validate(archive)
            for m in archive.getmembers():
                if m.isfile():
                    stream = archive.extractfile(m)
                    while stream.read(1024 * 1024):
                        pass
        print('Backup OK: ' + str(backup))
        return
    if os.geteuid() != 0:
        print('Requesting administrator access ...', flush=True)
        subprocess.run(['sudo', '/usr/bin/python3', '-u', str(Path(__file__).resolve()),
                        args.action] + ([str(backup)] if backup is not None else []), check=True)
        return
    os.umask(0o077)
    if args.action == 'legacy':
        rollback_legacy(Path('/'), backup, pwd.getpwnam('pi'))
        return
    install_enabled = None
    if args.action in ('backup-install', 'stop-apps'):
        install_enabled = prepare_install(pwd.getpwnam('pi'))
        if args.action == 'stop-apps':
            return
    if running_apps():
        raise ValueError('Close CurtantControl and app_model_1.py before continuing.')
    root = Path('/')
    if args.action in ('backup', 'backup-install'):
        enabled = install_enabled if install_enabled is not None else service('is-enabled', '--quiet', 'keycheck.service').returncode == 0
        save(root, backup, enabled)
        account = pwd.getpwnam('pi')
        os.chown(str(backup), account.pw_uid, account.pw_gid)
        backup.chmod(0o600)
        print('Backup saved: ' + str(backup))
        print('Keep this archive AND rollback.py. You can now install the update.')
    else:
        with tarfile.open(str(backup), 'r:gz') as archive:
            validate(archive)
        # Preserve the current installation too, before restoring the older snapshot.
        safety = Path('/home/pi/CurtantControl-Before-Restore-' + stamp() + '.tar.gz')
        enabled = subprocess.run(['systemctl', 'is-enabled', '--quiet', 'keycheck.service']).returncode == 0
        save(root, safety, enabled)
        account = pwd.getpwnam('pi')
        os.chown(str(safety), account.pw_uid, account.pw_gid)
        safety.chmod(0o600)
        subprocess.run(['systemctl', 'stop', 'keycheck.service'], check=False)
        metadata = restore(root, backup)
        subprocess.run(['systemctl', 'daemon-reload'], check=True)
        subprocess.run(['systemctl', 'enable' if metadata['keycheck_enabled'] else 'disable',
                        'keycheck.service'], check=metadata['keycheck_enabled'])
        print('Restored app, Python files, configuration and startup. Restart the Pi now.')
        print('Current version also saved: ' + str(safety))


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, KeyError, tarfile.TarError, subprocess.CalledProcessError) as error:
        sys.exit('ROLLBACK FAILED: ' + str(error))
