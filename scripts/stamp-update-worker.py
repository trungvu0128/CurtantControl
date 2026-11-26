"""Bind a packaged updater to the system setup shipped in its full bundle."""
from pathlib import Path
import runpy
import sys


def stamp(bundle):
    worker = bundle / 'payloads/buster-arm/apply-update.py'
    revision = runpy.run_path(str(worker))['system_revision'](bundle)
    data = worker.read_bytes()
    marker = b"SYSTEM_REVISION = 'unstamped'"
    if data.count(marker) != 1:
        raise ValueError('Expected one unstamped worker in the final bundle.')
    worker.write_bytes(data.replace(marker, b"SYSTEM_REVISION = '" + revision.encode('ascii') + b"'"))


if __name__ == '__main__':
    stamp(Path(sys.argv[1]))
