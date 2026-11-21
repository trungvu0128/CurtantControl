#!/usr/bin/env python3
"""Per-user online installer. No third-party Python modules required."""
import argparse
import datetime
import hashlib
import json
import os
import re
from pathlib import Path
import shlex
import shutil
import subprocess
import sys
import tempfile
from urllib.parse import urlsplit


def run(args, **kwargs):
    return subprocess.run(args, check=True, **kwargs)


def os_info(path=Path('/etc/os-release')):
    values = {}
    for line in path.read_text().splitlines():
        if '=' in line and not line.startswith('#'):
            key, value = line.split('=', 1)
            values[key] = ''.join(shlex.split(value))
    return values


def select_payload(info, arch):
    if info.get('ID') not in ('debian', 'raspbian'):
        raise ValueError('This bundle supports Raspberry Pi OS / Debian 10, 12 and 13 only.')
    version = info.get('VERSION_ID')
    if version == '10' and arch == 'armhf':
        return 'buster-arm'
    if version in ('12', '13') and arch in ('armhf', 'arm64'):
        return 'modern-' + ('arm' if arch == 'armhf' else 'arm64')
    raise ValueError('No matching payload for OS %s / %s. No system changes made.' % (version, arch))


def packages(info, include_keycheck=False):
    common = ['ffmpeg', 'v4l-utils', 'x11-utils', 'xdg-user-dirs', 'python3',
              'libx11-6', 'libice6', 'libsm6', 'libfontconfig1', 'libfreetype6',
              'libgssapi-krb5-2', 'zlib1g', 'libstdc++6', 'libgcc-s1', 'gpiod']
    version = info['VERSION_ID']
    if version == '10':
        common.remove('libgcc-s1')
        common += ['libgcc1', 'libicu63', 'libssl1.1', 'libgpiod2']
    elif version == '12':
        common += ['libicu72', 'libssl3', 'libgpiod2']
    else:
        common += ['libicu76', 'libssl3t64', 'libgpiod3']
    if include_keycheck:
        common += ['python3-rpi.gpio', 'wmctrl', 'python3-tk', 'python3-pil', 'python3-pil.imagetk']
    return common


def repair_buster_source_text(text):
    """Only migrate active Aliyun Raspbian Buster entries, preserving options/components."""
    pattern = re.compile(r'^(\s*deb(?:-src)?\s+(?:\[[^\]]*\]\s+)?)(\S+)(\s+)(buster(?:/\S+)?)(?=\s|$)', re.MULTILINE)
    def replace(match):
        uri = urlsplit(match.group(2))
        if (uri.scheme not in ('http', 'https') or uri.hostname != 'mirrors.aliyun.com'
                or uri.path.rstrip('/') not in ('/raspbian/raspbian', '/raspbian')):
            return match.group(0)
        return match.group(1) + 'https://legacy.raspbian.org/raspbian/' + match.group(3) + match.group(4)
    return pattern.sub(replace, text)


def repair_buster_sources(info, apt_root=Path('/etc/apt')):
    if info.get('ID') not in ('raspbian', 'debian') or info.get('VERSION_ID') != '10':
        return []
    paths = [apt_root / 'sources.list'] + sorted((apt_root / 'sources.list.d').glob('*.list'))
    changed = []
    for path in paths:
        if not path.is_file() or path.is_symlink():
            continue
        original = path.read_bytes()
        updated = repair_buster_source_text(original.decode('utf-8')).encode('utf-8')
        if original == updated:
            continue
        stamp = datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')
        backup = path.with_name(path.name + '.curtant-backup-' + stamp)
        shutil.copy2(str(path), str(backup))
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(dir=str(path.parent), prefix='.curtant-apt-', delete=False) as output:
                temporary = Path(output.name)
                output.write(updated)
            temporary.chmod(path.stat().st_mode & 0o777)
            os.replace(str(temporary), str(path))
        finally:
            if temporary is not None and temporary.exists():
                temporary.unlink()
        print('Updated deprecated Aliyun Buster mirror: %s (backup: %s)' % (path, backup), flush=True)
        changed.append(path)
    return changed


def verify_payload(payload):
    manifest = json.loads((payload / 'SHA256SUMS.json').read_text())
    if not manifest:
        raise ValueError('Empty payload manifest.')
    for relative, expected in manifest.items():
        entry = payload / relative
        if Path(relative).is_absolute() or '..' in Path(relative).parts or entry.is_symlink():
            raise ValueError('Unsafe manifest entry: ' + relative)
        if not entry.is_file() or hashlib.sha256(entry.read_bytes()).hexdigest() != expected:
            raise ValueError('Payload checksum failed: ' + relative)


def stage_install(payload, destination):
    """Stage completely first; preserve config and previous version for rollback."""
    if destination.is_symlink():
        raise ValueError('Installation destination cannot be a symlink.')
    destination = destination.resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    previous_config = destination / 'photobooth.json'
    config = previous_config.read_bytes() if previous_config.exists() else None
    previous_palette = destination / 'colors.json'
    palette_config = previous_palette.read_bytes() if previous_palette.exists() else None
    previous_names = destination / 'razr2-names.json'
    names_config = previous_names.read_bytes() if previous_names.exists() else None
    previous_camera_colors = destination / 'razr2-colors.json'
    camera_colors_config = previous_camera_colors.read_bytes() if previous_camera_colors.exists() else None
    previous_camera_options = destination / 'razr2-options.json'
    camera_options_config = previous_camera_options.read_bytes() if previous_camera_options.exists() else None
    if camera_options_config is not None:
        json.loads(camera_options_config)
    if camera_colors_config is not None:
        json.loads(camera_colors_config)
    if names_config is not None:
        json.loads(names_config)
    if palette_config is not None:
        json.loads(palette_config)
    if config is not None:
        json.loads(config)  # Refuse to replace a broken config silently.
    stage = Path(tempfile.mkdtemp(prefix='.curtant-stage-', dir=str(destination.parent)))
    backup = None
    try:
        for entry in payload.iterdir():
            if entry.is_dir():
                shutil.copytree(str(entry), str(stage / entry.name))
            else:
                shutil.copy2(str(entry), str(stage / entry.name))
        if config is not None:
            (stage / 'photobooth.json').write_bytes(config)
        if palette_config is not None:
            (stage / 'colors.json').write_bytes(palette_config)
        if names_config is not None:
            (stage / 'razr2-names.json').write_bytes(names_config)
        if camera_colors_config is not None:
            (stage / 'razr2-colors.json').write_bytes(camera_colors_config)
        if camera_options_config is not None:
            (stage / 'razr2-options.json').write_bytes(camera_options_config)
        for name in ('CurtantControl', 'start.sh', 'diagnose.sh'):
            (stage / name).chmod(0o755)
        if destination.exists():
            stamp = datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')
            backup = destination.with_name('app-backup-' + stamp)
            destination.rename(backup)
        try:
            stage.rename(destination)
        except Exception:
            if backup is not None:
                backup.rename(destination)
            raise
        return backup
    finally:
        if stage.exists():
            shutil.rmtree(str(stage))  # Only the mkdtemp-created staging directory.


def app_running(destination):
    for entry in Path('/proc').iterdir():
        if not entry.name.isdigit():
            continue
        try:
            if (entry / 'exe').resolve() == destination / 'CurtantControl':
                return True
        except (OSError, RuntimeError):
            pass
    return False


def ensure_rollback_backup(base):
    """Keep the first baseline for this extracted bundle, including failed-install retries."""
    pointer = base / '.rollback-target'
    helper = base / 'rollback.py'
    if pointer.exists():
        archive = Path(pointer.read_text(encoding='utf-8').strip())
        if not archive.is_absolute() or not archive.is_file():
            raise ValueError('Previous rollback backup is missing; restore it before retrying setup.')
        run([sys.executable, str(helper), 'check', str(archive)])
        run([sys.executable, str(helper), 'stop-apps'])
        print('Keeping original rollback baseline: ' + str(archive), flush=True)
        return archive
    archive = Path.home() / ('CurtantControl-Rollback-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f') + '.tar.gz')
    run([sys.executable, str(helper), 'backup-install', str(archive)])
    run([sys.executable, str(helper), 'check', str(archive)])
    temporary = base / '.rollback-target.tmp'
    temporary.write_text(str(archive) + '\n', encoding='utf-8')
    os.replace(str(temporary), str(pointer))
    print('Rollback ready: sh "%s"' % (base / 'rollback.sh'), flush=True)
    return archive


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Preflight only; no changes or sudo')
    parser.add_argument('--skip-packages', action='store_true', help='Use already installed OS dependencies')
    parser.add_argument('--repair-apt', action='store_true', help=argparse.SUPPRESS)
    args = parser.parse_args()
    if args.repair_apt:
        if not sys.platform.startswith('linux') or os.geteuid() != 0:
            raise ValueError('Repository repair is invoked by setup through sudo.')
        repair_buster_sources(os_info())
        return
    if not sys.platform.startswith('linux') or os.geteuid() == 0:
        raise ValueError('Run sh install.sh on the Pi as its desktop user, without sudo.')
    info = os_info()
    arch = subprocess.check_output(['dpkg', '--print-architecture'], universal_newlines=True).strip()
    model_path = Path('/proc/device-tree/model')
    model = model_path.read_text().strip('\x00') if model_path.exists() else ''
    if not model.startswith('Raspberry Pi 4'):
        raise ValueError('This bundle targets Raspberry Pi 4. Detected: ' + (model or 'unknown board'))
    key = select_payload(info, arch)
    base = Path(__file__).resolve().parent
    payload = base / 'payloads' / key
    verify_payload(payload)
    include_keycheck = (base / 'keycheck/install.sh').is_file()
    if include_keycheck:
        if subprocess.check_output(['id', '-un'], universal_newlines=True).strip() != 'pi':
            raise ValueError('The integrated button bundle is configured for desktop user pi.')
        verify_payload(base / 'keycheck')
    destination = Path.home() / '.local/share/CurtantControl/app'
    if app_running(destination) and (args.check or not (include_keycheck and (base / 'rollback.py').is_file())):
        raise ValueError('Close CurtantControl before installing or updating.')
    print('Device: %s; %s; %s' % (model, info.get('PRETTY_NAME'), arch), flush=True)
    print('Selected: %s -> %s' % (key, destination), flush=True)
    print('OS packages: ' + ' '.join(packages(info, include_keycheck)), flush=True)
    if args.check:
        print('Preflight passed. No changes made. Network/package availability and native execution not tested.')
        return
    if include_keycheck and (base / 'rollback.py').is_file():
        # Must precede apt repair, group changes, app replacement and kiosk migration.
        ensure_rollback_backup(base)
    if not args.skip_packages:
        try:
            if info['VERSION_ID'] == '10':
                run(['sudo', sys.executable, str(base / 'setup.py'), '--repair-apt'])
            run(['sudo', 'apt-get', 'update', '--allow-releaseinfo-change'])
            run(['sudo', 'apt-get', 'install', '-y'] + packages(info, include_keycheck))
        except subprocess.CalledProcessError:
            raise ValueError('Dependency installation failed. Fix the reported apt repository/network error and rerun. '
                             'Only the obsolete Aliyun Raspbian Buster mirror is migrated; signature checks stay enabled.')
    # Verify native dependencies before replacing an existing working installation.
    for name in ('CurtantControl', 'libcoreclr.so', 'libSkiaSharp.so', 'libHarfBuzzSharp.so'):
        result = subprocess.run(['ldd', str(payload / name)], stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, universal_newlines=True)
        if result.returncode != 0 or 'not found' in result.stdout:
            raise ValueError('Native dependency check failed for %s:\n%s' % (name, result.stdout))
    groups = subprocess.check_output(['id', '-nG'], universal_newlines=True).split()
    changed = False
    for group in ('gpio', 'video'):
        import grp
        try:
            grp.getgrnam(group)
        except KeyError:
            print('Group %s is absent; check OS GPIO/camera device permissions.' % group)
            continue
        if group not in groups:
            run(['sudo', 'usermod', '-aG', group, subprocess.check_output(['id', '-un'], universal_newlines=True).strip()])
            changed = True
    if app_running(destination):
        raise ValueError('CurtantControl started during dependency setup; close it and rerun.')
    backup = stage_install(payload, destination)
    if include_keycheck:
        run([sys.executable, str(base / 'legacy_kiosk.py')])
    run([sys.executable, str(destination / 'install-shortcut.py')])
    if include_keycheck:
        run(['/bin/sh', str(base / 'keycheck/install.sh'), '--skip-packages'])
        print('Physical buttons, centered popups and desktop autostart installed.')
    print('\nInstalled. Open CurtantControl from the desktop/menu. Allow Launching if prompted.')
    print('Configuration: ' + str(destination / 'photobooth.json'))
    print('Diagnostics: sh "%s"' % (destination / 'diagnose.sh'))
    if backup:
        print('Previous installation/config retained: ' + str(backup))
    if changed:
        print('IMPORTANT: log out and back in before hardware/camera testing to apply group permissions.')
    if info['VERSION_ID'] == '10':
        print('Legacy Buster build: unsupported OS; bundled .NET 8 support ends 2026-11-10.')
    installed_config = json.loads((destination / 'photobooth.json').read_text())
    print('Relay mode: ' + ('simulation' if installed_config.get('SimulateRelays', True) else 'hardware (configured GPIO profile)'))
    print('USB / existing MJPEG service supported. Modern CSI libcamera/rpicam is not implemented.')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print('SETUP FAILED: ' + str(error), file=sys.stderr)
        sys.exit(1)
