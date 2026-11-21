#!/usr/bin/env python3
"""Build a Pi patch only when two full releases differ in replaceable app files."""
import hashlib
import json
from pathlib import Path
import re
import sys
import zipfile

PAYLOAD = 'payloads/buster-arm/'
REPLACEABLE = {'CurtantControl.dll', 'CurtantControl.deps.json', 'CurtantControl.runtimeconfig.json',
               'VERSION.txt', 'apply-update.py'}
LOCAL_CONFIG = {'photobooth.json', 'colors.json', 'razr2-options.json', 'razr2-names.json', 'razr2-colors.json'}
MANUAL_CLEANUP = b'# Manual post-install cleanup. The app updater runs this after restarting.\n'


def file_digest(path):
    digest = hashlib.sha256()
    with path.open('rb') as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def files_for_patch(old, new):
    def scan(root):
        return {p.relative_to(root).as_posix(): file_digest(p) for p in root.rglob('*')
                if p.is_file() and p.name != 'SHA256SUMS.json' and p.suffix not in ('.md', '.pdb')
                and p.relative_to(root).as_posix() != 'VERSION.txt'}
    before, after = scan(old), scan(new)
    changed = {name for name in before.keys() | after.keys() if before.get(name) != after.get(name)}
    if 'install.sh' in changed and 'install.sh' in before and 'install.sh' in after:
        # The manual wrapper is not installed on Pi. Allow its cleanup tail while
        # still comparing every line that affects how setup is launched.
        def setup_body(bundle):
            script = (bundle / 'install.sh').read_bytes().replace(b'\r\n', b'\n')
            script = script.partition(MANUAL_CLEANUP)[0]
            return script.replace(b'\nexec python3 setup.py "$@"\n', b'\npython3 setup.py "$@"\n')
        if setup_body(old) == setup_body(new):
            changed.remove('install.sh')
    allowed = {PAYLOAD + name for name in REPLACEABLE}
    if changed - allowed or any(name not in before or name not in after for name in changed):
        raise ValueError('Full update required. Files outside the patch set changed: ' + ', '.join(sorted(
            (changed - allowed) | (changed - before.keys()) | (changed - after.keys()))))
    return ({name[len(PAYLOAD):]: digest for name, digest in before.items()
             if name.startswith(PAYLOAD) and name[len(PAYLOAD):] not in LOCAL_CONFIG},
            {name[len(PAYLOAD):]: after[name] for name in sorted(changed)})


def build(bundles, output, source_version, version):
    for value in (source_version, version):
        if not re.fullmatch(r'\d+\.\d+\.\d+', value):
            raise ValueError('Use MAJOR.MINOR.PATCH.')
    if tuple(map(int, source_version.split('.'))) >= tuple(map(int, version.split('.'))):
        raise ValueError('Target must be newer than baseline.')
    old, new = (bundles / ('CurtantControl-Final-v%s-pi-buster-arm' % v) for v in (source_version, version))
    for folder, expected in ((old, source_version), (new, version)):
        if (folder / 'VERSION.txt').read_text().strip() != expected:
            raise ValueError('Bundle version mismatch.')
    before, changed = files_for_patch(old, new)
    if not {'CurtantControl.dll', 'VERSION.txt'} <= changed.keys():
        raise ValueError('Missing changed application or version file.')
    manifest = {'from': source_version, 'to': version, 'before': before, 'after': changed}
    output.mkdir(parents=True, exist_ok=True)
    archive = output / ('CurtantControl-Update-v%s-to-v%s-pi-buster-arm.zip' % (source_version, version))
    with zipfile.ZipFile(str(archive), 'w', compression=zipfile.ZIP_DEFLATED) as package:
        package.writestr('patch.json', json.dumps(manifest, sort_keys=True, indent=2) + '\n')
        for name in changed:
            package.write(str(new / PAYLOAD / name), name)
    with zipfile.ZipFile(str(archive)) as package:
        if package.testzip() or any(hashlib.sha256(package.read(n)).hexdigest() != h for n, h in changed.items()):
            raise ValueError('Small update ZIP verification failed.')
    checksum = Path(str(archive) + '.sha256')
    checksum.write_bytes((file_digest(archive) + '  ' + archive.name + '\n').encode('ascii'))
    return archive, checksum


if __name__ == '__main__':
    if len(sys.argv) != 3:
        sys.exit('Usage: build-small-update.py BASE_VERSION NEW_VERSION')
    artifacts = Path(__file__).resolve().parents[1] / 'artifacts'
    try:
        print(build(artifacts / 'bundles', artifacts / 'updates', *sys.argv[1:])[0])
    except (OSError, ValueError) as error:
        sys.exit(str(error))
