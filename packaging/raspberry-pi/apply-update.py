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
import zipfile

SMALL_NAME = r'CurtantControl-Update-v(\d+\.\d+\.\d+)-to-v(\d+\.\d+\.\d+)-pi-buster-arm\.zip'
SMALL_FILES = {'CurtantControl.dll', 'CurtantControl.deps.json', 'CurtantControl.runtimeconfig.json',
               'VERSION.txt', 'apply-update.py'}


class IncompatibleUpdate(ValueError):
    pass


def file_hash(path):
    digest = hashlib.sha256()
    with path.open('rb') as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def preserve_recovery(base):
    pointer = base / '.rollback-target'
    if not pointer.is_file():
        raise ValueError('Rollback reference is missing. Installer files were kept.')
    snapshot = Path(pointer.read_text(encoding='utf-8').strip())
    if (not snapshot.is_absolute() or not snapshot.is_file()
            or not re.fullmatch(r'CurtantControl-Rollback-[\d-]+\.tar\.gz', snapshot.name)):
        raise ValueError('Rollback snapshot is missing. Installer files were kept.')

    import tempfile

    root = Path.home() / '.local/share/CurtantControl/recovery'
    root.mkdir(parents=True, exist_ok=True)
    recovery = root / snapshot.name[:-len('.tar.gz')]
    if not recovery.exists():
        with tempfile.TemporaryDirectory(prefix='.staging-', dir=str(root)) as staging:
            prepared = Path(staging) / 'recovery'
            prepared.mkdir()
            for name in ('rollback.sh', 'rollback.py', '.rollback-target'):
                shutil.copy2(str(base / name), str(prepared / name))
            prepared.rename(recovery)
    if not all((recovery / name).is_file() and file_hash(recovery / name) == file_hash(base / name)
               for name in ('rollback.sh', 'rollback.py', '.rollback-target')):
        raise ValueError('Could not verify recovery files. Installer files were kept.')
    print('Rollback: sh "%s"' % (recovery / 'rollback.sh'), flush=True)


def remove_known_files(base, files):
    if base.is_symlink() or not base.is_dir():
        return
    base = base.resolve()
    folders = {base}
    for name, expected in files.items():
        relative = PurePosixPath(name)
        if relative.is_absolute() or '..' in relative.parts or '\\' in name or not relative.parts:
            continue
        file = base.joinpath(*relative.parts)
        if not file.is_file() or file.resolve() != file or file_hash(file) != expected:
            continue
        file.unlink()
        folders.update(parent for parent in file.parents if base in parent.parents)
    for folder in sorted(folders, key=lambda p: len(p.parts), reverse=True):
        try:
            folder.rmdir()
        except OSError:
            pass  # Keep folders containing anything outside the verified package.


def clean_manual_install(base):
    if not re.fullmatch(r'CurtantControl-Final-v\d+\.\d+\.\d+-pi-buster-arm', base.name):
        return
    path = base / 'SHA256SUMS.json'
    manifest = path.read_bytes()
    files = json.loads(manifest)
    preserve_recovery(base)
    for name in ('SHA256SUMS.json', 'payloads/buster-arm/SHA256SUMS.json', 'keycheck/SHA256SUMS.json', '.rollback-target'):
        file = base / name
        if file.is_file():
            files[name] = file_hash(file)
    for bytecode in (base / 'keycheck/__pycache__').glob('key_check_new.cpython-*.pyc'):
        if bytecode.is_file() and not bytecode.is_symlink():
            files[bytecode.relative_to(base).as_posix()] = file_hash(bytecode)
    archive = base.with_name(base.name + '.tar.gz')
    if archive.is_file() and not archive.is_symlink():
        try:
            with tarfile.open(str(archive), 'r:gz') as package:
                with package.extractfile(base.name + '/SHA256SUMS.json') as source:
                    matches = source.read(len(manifest) + 1) == manifest
        except (KeyError, OSError, tarfile.TarError):
            matches = False
        if matches:
            checksum = Path(str(archive) + '.sha256')
            if checksum.is_file() and not checksum.is_symlink():
                try:
                    verify_checksum(archive)
                except ValueError:
                    pass
                else:
                    checksum.unlink()
            archive.unlink()
    remove_known_files(base, files)
    print('Installation files cleaned.', flush=True)


def finish_manual_small(archive):
    with zipfile.ZipFile(str(archive)) as package:
        files = {name: hashlib.sha256(package.read(name)).hexdigest() for name in package.namelist()}
    match = re.fullmatch(SMALL_NAME, archive.name)
    for name in ('update-' + match[2], archive.stem):
        remove_known_files(archive.parent / name, files)
    archive.unlink()
    Path(str(archive) + '.sha256').unlink()
    print('Downloaded update files cleaned.', flush=True)


def clean_completed_updates():
    cache = Path.home() / '.cache/CurtantControl/updates'
    if not cache.is_dir() or cache.resolve() != cache:
        return
    for job in cache.iterdir():
        if not re.fullmatch(r'(?:\.cleanup-)?[0-9a-f]{32}', job.name) or job.is_symlink() or not job.is_dir():
            continue
        try:
            if job.name.startswith('.cleanup-'):
                shutil.rmtree(str(job))
                continue
            result = job / 'status.json'
            status = json.loads(result.read_text(encoding='utf-8'))
            version = status.get('version', '')
            if status.get('phase') != 'complete' or not re.fullmatch(r'\d+\.\d+\.\d+', version):
                continue
            if time.time() - result.stat().st_mtime < 2:
                continue  # Give the old progress overlay a chance to see completion.
            full = job / 'extracted' / ('CurtantControl-Final-v%s-pi-buster-arm' % version)
            if full.is_dir():
                preserve_recovery(full)
            retired_job = job.with_name('.cleanup-' + job.name)
            job.rename(retired_job)
            shutil.rmtree(str(retired_job))
            print('Cleaned completed update ' + version, flush=True)
        except (OSError, ValueError, TypeError) as error:
            print('Update cleanup skipped: ' + str(error), file=sys.stderr, flush=True)


def report(path, phase, message, version):
    data = {'phase': phase, 'message': message, 'version': version}
    for target in (path, Path.home() / '.local/share/CurtantControl/update-status.json'):
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(target.name + '.tmp')
        temporary.write_text(json.dumps(data), encoding='utf-8')
        os.replace(str(temporary), str(target))
    print(phase + ': ' + message, flush=True)


def verify_checksum(archive):
    checksum = Path(str(archive) + '.sha256').read_text(encoding='utf-8')
    match = re.fullmatch(r'([a-fA-F0-9]{64})  ' + re.escape(archive.name) + r'\r?\n?', checksum)
    if not match or file_hash(archive) != match.group(1).lower():
        raise ValueError('Update checksum verification failed.')


def extract_verified(archive, version, job):
    expected_name = 'CurtantControl-Final-v%s-pi-buster-arm.tar.gz' % version
    if archive.name != expected_name:
        raise ValueError('Unexpected update filename.')
    verify_checksum(archive)
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


def verify_installed(app_dir, before):
    for name, expected in before.items():
        path = PurePosixPath(name)
        if (path.is_absolute() or not path.parts or '..' in path.parts or '\\' in name
                or not re.fullmatch(r'[a-f0-9]{64}', expected)):
            raise ValueError('Invalid small update manifest.')
        installed = app_dir.joinpath(*path.parts)
        if not installed.is_file() or installed.is_symlink() or file_hash(installed) != expected:
            raise IncompatibleUpdate('The installed files differ from the small update baseline.')


def prepare_small(archive, version, app_dir):
    match = re.fullmatch(SMALL_NAME, archive.name)
    if not match or match[2] != version or tuple(map(int, version.split('.'))) <= tuple(map(int, match[1].split('.'))):
        raise ValueError('Unexpected small update filename or version.')
    verify_checksum(archive)
    with zipfile.ZipFile(str(archive)) as package:
        names = package.namelist()
        if (len(names) != len(set(names)) or set(names) - SMALL_FILES - {'patch.json'}
                or sum(entry.file_size for entry in package.infolist()) > 10 * 1024 * 1024):
            raise ValueError('Unexpected small update contents.')
        manifest = json.loads(package.read('patch.json'))
        before, after = manifest['before'], manifest['after']
        if (manifest['from'] != match[1] or manifest['to'] != version
                or set(after) != set(names) - {'patch.json'} or not set(after) <= set(before)
                or not {'CurtantControl.dll', 'VERSION.txt'} <= set(after)
                or not {'CurtantControl', 'libcoreclr.so'} <= set(before)):
            raise ValueError('Invalid small update manifest.')
        files = {name: package.read(name) for name in after}
        if any(hashlib.sha256(content).hexdigest() != after[name] for name, content in files.items()):
            raise ValueError('Small update file checksum failed.')
        if files['VERSION.txt'].decode('utf-8').strip() != version + '-buster':
            raise ValueError('Small update payload version mismatch.')
    verify_installed(app_dir, before)
    return before, files


def install_small(app_dir, patch):
    import tempfile

    before, files = patch
    verify_installed(app_dir, before)
    if any(exe.resolve() == app_dir / 'CurtantControl' for exe in running_executables()):
        raise ValueError('Close CurtantControl before applying this update.')
    backup = Path(tempfile.mkdtemp(prefix='app-files-backup-', dir=str(app_dir.parent)))
    stage = backup / 'staged'
    stage.mkdir()
    for name, content in files.items():
        current = app_dir / name
        shutil.copy2(str(current), str(backup / name))
        with (stage / name).open('wb') as output:
            output.write(content)
            output.flush()
            os.fsync(output.fileno())
        shutil.copymode(str(current), str(stage / name))
    replaced = []
    try:
        for name in sorted(files, key=lambda name: name == 'VERSION.txt'):
            os.replace(str(stage / name), str(app_dir / name))
            replaced.append(name)
    except OSError:
        for name in reversed(replaced):
            os.replace(str(backup / name), str(app_dir / name))
        raise
    stage.rmdir()
    print('Previous files saved in ' + str(backup), flush=True)
    return backup


def running_executables():
    for info in Path('/proc').iterdir():
        if info.name.isdigit():
            try:
                yield Path(os.readlink(str(info / 'exe')))
            except OSError:
                pass


def lock_app_startup():
    import fcntl

    path = Path.home() / '.cache/curtantcontrol-startup.lock'
    path.parent.mkdir(parents=True, exist_ok=True)
    handle = path.open('a')
    try:
        fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        handle.close()
        raise ValueError('An app launch or update is already in progress.')
    return handle


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
    verify_platform()
    for name in ('CurtantControl', 'libcoreclr.so', 'libSkiaSharp.so', 'libHarfBuzzSharp.so'):
        result = subprocess.run(['ldd', str(base / 'payloads/buster-arm' / name)], stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, universal_newlines=True, timeout=15)
        if result.returncode or 'not found' in result.stdout:
            raise ValueError('A required system library is missing. Run the full installer manually.')


def verify_platform():
    if subprocess.check_output(['dpkg', '--print-architecture'], universal_newlines=True).strip() != 'armhf':
        raise ValueError('This update requires a 32-bit Pi OS.')
    if not Path('/proc/device-tree/model').read_text().startswith('Raspberry Pi 4'):
        raise ValueError('This update requires Raspberry Pi 4.')
    text = Path('/etc/os-release').read_text()
    if not re.search(r'(?m)^VERSION_ID="?10"?$', text) or not re.search(r'(?m)^ID="?(raspbian|debian)"?$', text):
        raise ValueError('This update requires Pi Buster.')


def app_directory():
    return Path.home() / '.local/share/CurtantControl/app'


def manual_small(archive):
    match = re.fullmatch(SMALL_NAME, archive.name)
    if not match or os.geteuid() == 0:
        raise ValueError('Run the small update archive as desktop user pi.')
    verify_platform()
    with lock_app_startup():
        app_dir = app_directory()
        install_small(app_dir, prepare_small(archive, match[2], app_dir))
    print('Small update installed. Open CurtantControl from the desktop.', flush=True)
    try:
        finish_manual_small(archive)
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        print('Update installed, but cleanup could not finish: ' + str(error), file=sys.stderr, flush=True)


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
    small = archive.suffix == '.zip'
    startup_lock = None
    try:
        report(status, 'preparing', 'Verifying update files', version)
        if small:
            startup_lock = lock_app_startup()
            verify_platform()
            patch = prepare_small(archive, version, app_directory())
        else:
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
        if small:
            install_small(app_directory(), patch)
        else:
            install(base, os.environ)
        report(status, 'complete', 'Update installed successfully', version)
    except Exception as error:
        report(status, 'incompatible' if isinstance(error, IncompatibleUpdate) and not can_restart else 'failed', str(error), version)
        if not can_restart:
            return 1
    finally:
        if startup_lock is not None:
            startup_lock.close()
    app = app_directory() / 'CurtantControl'
    if app.is_file():
        os.chdir(str(app.parent))
        # Keep the new app outside keycheck.service's cgroup. Restarting keycheck
        # during install cannot kill this worker; exec retains the user unit.
        os.execv(str(app), [str(app)])
    report(status, 'failed', 'App could not be restarted. Check the update log.', version)
    return 1


if __name__ == '__main__':
    if sys.argv[1:] == ['--clean-completed']:
        clean_completed_updates()
    elif len(sys.argv) == 3 and sys.argv[1] == '--clean-install':
        try:
            clean_manual_install(Path(sys.argv[2]).resolve())
        except (OSError, ValueError) as error:
            sys.exit('Installed successfully, but cleanup could not finish: ' + str(error))
    elif len(sys.argv) == 3 and sys.argv[1] == '--apply-small':
        try:
            manual_small(Path(sys.argv[2]).resolve())
        except (OSError, ValueError) as error:
            sys.exit(str(error))
    elif len(sys.argv) == 3 and sys.argv[1] == '--progress':
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
