#!/usr/bin/env python3
"""Isolated user-service update worker. Never installs until the old app has exited."""
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import tarfile
import time


def file_hash(path):
    digest = hashlib.sha256()
    with path.open('rb') as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def report(path, phase, message, version):
    data = {'phase': phase, 'message': message, 'version': version}
    for target in (path, Path.home() / '.local/share/CurtantControl/update-status.json'):
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(target.name + '.tmp')
        temporary.write_text(json.dumps(data), encoding='utf-8')
        os.replace(str(temporary), str(target))
    print(phase + ': ' + message, flush=True)


def extract_verified(archive, version, job):
    expected_name = 'CurtantControl-Final-v%s-pi-buster-arm.tar.gz' % version
    if archive.name != expected_name:
        raise ValueError('Unexpected update filename.')
    checksum = Path(str(archive) + '.sha256').read_text(encoding='utf-8')
    match = re.fullmatch(r'([a-fA-F0-9]{64})  ' + re.escape(expected_name) + r'\r?\n?', checksum)
    if not match or file_hash(archive) != match.group(1).lower():
        raise ValueError('Update checksum verification failed.')
    top = 'CurtantControl-Final-v%s-pi-buster-arm' % version
    destination = job / 'extracted'
    if destination.exists():
        raise ValueError('Update extraction directory already exists.')
    with tarfile.open(str(archive), 'r:gz') as bundle:
        members = bundle.getmembers()
        if len(members) > 2000 or sum(m.size for m in members) > 700 * 1024 * 1024:
            raise ValueError('Update archive exceeds size limit.')
        if shutil.disk_usage(str(job)).free < sum(m.size for m in members) * 2 + 100 * 1024 * 1024:
            raise ValueError('Not enough free space to prepare and back up the update.')
        seen = set()
        for member in members:
            p = PurePosixPath(member.name)
            if p.is_absolute() or '..' in p.parts or not p.parts or p.parts[0] != top or member.name in seen:
                raise ValueError('Unsafe update archive path.')
            if not (member.isfile() or member.isdir()):
                raise ValueError('Update archive cannot contain links or special files.')
            seen.add(member.name)
        destination.mkdir()
        for member in members:
            bundle.extract(member, str(destination))
    base = destination / top
    for folder in (base, base / 'payloads/buster-arm', base / 'keycheck'):
        manifest = json.loads((folder / 'SHA256SUMS.json').read_text(encoding='utf-8'))
        if not manifest:
            raise ValueError('Empty update manifest.')
        for name, expected in manifest.items():
            p = PurePosixPath(name)
            file = folder / name
            if p.is_absolute() or '..' in p.parts or not file.is_file() or file.is_symlink():
                raise ValueError('Invalid update manifest path.')
            if file_hash(file) != expected:
                raise ValueError('Update file checksum failed: ' + name)
    if (base / 'VERSION.txt').read_text().strip() != version:
        raise ValueError('Update version mismatch.')
    for name in ('setup.py', 'rollback.py', 'keycheck/install.sh', 'payloads/buster-arm/CurtantControl'):
        if not (base / name).is_file():
            raise ValueError('Incomplete update bundle.')
    return base


def wait_parent(pid, seconds=30, cancelled=None):
    def check_cancelled():
        if cancelled is not None and cancelled.exists():
            raise ValueError('Update cancelled. No installation was performed.')
    deadline = time.monotonic() + seconds
    while Path('/proc/%d' % pid).exists():
        check_cancelled()
        if time.monotonic() >= deadline:
            raise ValueError('The app did not finish closing. No installation was performed.')
        time.sleep(.2)
    check_cancelled()


def install(base, environment):
    # The desktop service has no terminal: force non-interactive sudo for every
    # installer subprocess rather than waiting invisibly for a password prompt.
    wrapper = base.parent / 'bin'
    wrapper.mkdir(exist_ok=True)
    sudo = wrapper / 'sudo'
    sudo.write_text('#!/bin/sh\nexec /usr/bin/sudo -n "$@"\n')
    sudo.chmod(0o755)
    environment = dict(environment, PATH=str(wrapper) + os.pathsep + environment.get('PATH', ''))
    try:
        subprocess.run(['/usr/bin/python3', str(base / 'setup.py'), '--skip-packages'], env=environment, check=True)
    except Exception:
        pointer = base / '.rollback-target'
        if pointer.is_file():
            backup = pointer.read_text().strip()
            subprocess.run(['/usr/bin/python3', str(base / 'rollback.py'), 'restore', backup], env=environment, check=True)
            subprocess.run(['/usr/bin/sudo', '-n', 'systemctl', 'restart', 'keycheck.service'], check=True)
        raise


def verify_device(base):
    if subprocess.check_output(['dpkg', '--print-architecture'], universal_newlines=True).strip() != 'armhf':
        raise ValueError('This update requires a 32-bit Pi OS.')
    if not Path('/proc/device-tree/model').read_text().startswith('Raspberry Pi 4'):
        raise ValueError('This update requires Raspberry Pi 4.')
    text = Path('/etc/os-release').read_text()
    if not re.search(r'(?m)^VERSION_ID="?10"?$', text) or not re.search(r'(?m)^ID="?(raspbian|debian)"?$', text):
        raise ValueError('This update requires Pi Buster.')
    for name in ('CurtantControl', 'libcoreclr.so', 'libSkiaSharp.so', 'libHarfBuzzSharp.so'):
        result = subprocess.run(['ldd', str(base / 'payloads/buster-arm' / name)], stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, universal_newlines=True, timeout=15)
        if result.returncode or 'not found' in result.stdout:
            raise ValueError('A required system library is missing. Run the full installer manually.')


def main():
    if len(sys.argv) != 5:
        sys.exit('Usage: apply-update.py ARCHIVE VERSION PARENT_PID STATUS')
    archive, version, pid, status = Path(sys.argv[1]).resolve(), sys.argv[2], int(sys.argv[3]), Path(sys.argv[4]).resolve()
    cache = (Path.home() / '.cache/CurtantControl/updates').resolve()
    if not re.fullmatch(r'\d+\.\d+\.\d+', version) or pid <= 1 or cache not in archive.parents or status.parent != archive.parent:
        raise ValueError('Invalid update job.')
    if os.geteuid() == 0 or subprocess.check_output(['id', '-un'], universal_newlines=True).strip() != 'pi':
        raise ValueError('Updates must run as desktop user pi.')
    can_restart = False
    try:
        report(status, 'preparing', 'Verifying update files', version)
        subprocess.run(['/usr/bin/sudo', '-n', '-v'], check=True, timeout=10)
        base = extract_verified(archive, version, archive.parent)
        verify_device(base)
        report(status, 'ready', 'Ready; waiting for the current app to close', version)
        wait_parent(pid, cancelled=archive.parent / 'cancelled')
        can_restart = True
        report(status, 'installing', 'Backing up and installing update', version)
        try:
            subprocess.Popen([sys.executable, str(Path(__file__).resolve()), '--progress', str(status)],
                             stdin=subprocess.DEVNULL, start_new_session=True)
        except OSError:
            pass
        install(base, os.environ)
        report(status, 'complete', 'Update installed successfully', version)
    except Exception as error:
        report(status, 'failed', str(error), version)
        if not can_restart:
            return 1
    app = Path.home() / '.local/share/CurtantControl/app/CurtantControl'
    if app.is_file():
        os.chdir(str(app.parent))
        # Keep the new app outside keycheck.service's cgroup. Restarting keycheck
        # during install cannot kill this worker; exec retains the user unit.
        os.execv(str(app), [str(app)])
    report(status, 'failed', 'App could not be restarted. Check the update log.', version)
    return 1


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--progress':
        try:
            import importlib.util
            spec = importlib.util.spec_from_file_location('keycheck_overlay', '/home/pi/r1/key_check_new.py')
            overlay = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(overlay)
            root, frame, label = overlay.create_overlay('Updating Curtant Control…')
            def observe():
                try:
                    state = json.loads(Path(sys.argv[2]).read_text())
                    if state['phase'] == 'complete':
                        root.destroy()
                        return
                    if state['phase'] == 'failed':
                        label.config(text='Update failed. Check Updates or the update log.')
                        root.after(15000, root.destroy)
                        return
                    label.config(text='Updating Curtant Control…\nPlease wait.')
                except (ValueError, OSError):
                    pass
                root.after(500, observe)
            root.bind('<Escape>', lambda event: root.destroy())
            root.after(500, observe)
            root.after(600000, root.destroy)
            root.mainloop()
        except Exception as error:
            print('Update progress popup unavailable: ' + str(error), flush=True)
    else:
        sys.exit(main())
