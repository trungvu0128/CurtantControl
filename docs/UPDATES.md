# GitHub updates

The app checks `trungvu0128/CurtantControl` GitHub Releases once each time its
window opens. The HTTP check runs asynchronously alongside camera startup, with
a 15-second limit. From 0.11.2, a brief “Checking for updates…” banner appears
at the top of the app. It becomes an update notice if a newer version is found
and disappears otherwise. Offline startup stays usable.
Only a newer non-draft/non-prerelease `vMAJOR.MINOR.PATCH` release with the exact
BusterARM32 full bundle and its SHA256 asset is offered. Source branch pushes
alone do not create an update.

From 0.11.1, Updates prefers a small ZIP named for the installed and target
versions when one is attached to the release. The worker verifies the installed
file hashes before closing the app. If the baseline differs, the app shows the
fallback and downloads the full bundle. A corrupt download stops the attempt.
Small updates save the replaced files in `app-files-backup-*` beside the app
folder and restore replaced files if a write fails. They run as `pi` without sudo.

User mode shows a dismissible new-version notice. Developer mode → **Updates**
shows installed/new versions, release notes, **Check again**, and **Download &
install**. User mode cannot install. Cancelling or download/preparation failure
keeps the current app. Camera/scene commands are blocked while the modal is open.

## First installation

Install the complete 0.11.1+ bundle with `sh install.sh` for a new device.
An existing matching 0.11.0 installation can use the 0.11.1 small ZIP. Download
the ZIP and its `.sha256`, close the app, then run on the Pi:

```sh
patch=CurtantControl-Update-v0.11.0-to-v0.11.1-pi-buster-arm.zip
sha256sum -c "$patch.sha256" &&
python3 -m zipfile -e "$patch" update-0.11.1 &&
python3 update-0.11.1/apply-update.py --apply-small "$patch"
```

Open the app from its desktop shortcut. Future compatible updates can be
installed in the app. No GitHub token or Git
installation is needed on the Pi; the repository and its releases are public.

Automatic installation targets Pi4, Raspbian/Debian10 Buster ARM32 and desktop
user `pi` with a user systemd session. With the new updater installed, a full
download can replace the entire application as `pi`. Updates checks the native
libraries, bundle system setup revision, installed button script, logo and
service override, and whether keycheck is active and enabled. If they match,
Updates saves the previous app folder and preserves the installer's supported
local settings. This path never calls sudo.

Changed system setup or an incomplete installation uses the existing system
installer with noninteractive sudo. If authorization is unavailable, preparation
stops with the old app open and the verified archive in cache. Devices on 0.12.6
can bootstrap the new worker with a compatible small patch. Their existing
worker still requires sudo for full bundles. Run `sudo -n -v` on
the Pi as `pi` to see the sudo error.
If sudo requests a password, verify access with `sudo -v`, then run `sh install.sh`
inside the extracted full bundle as `pi`. The installer prompts for sudo when
needed. If sudo says `pi` is unauthorized, use the Pi administrator to resolve
access first.

The full archive and its `.sha256` remain in the failed cache job. To reuse
them, close the app, open a Pi terminal as `pi`, and set `version` to the
version the app was downloading:

```sh
version=0.12.6
name="CurtantControl-Final-v$version-pi-buster-arm.tar.gz"
bundle=$(find "$HOME/.cache/CurtantControl/updates" -type f -name "$name" -print -quit)
if [ -n "$bundle" ]; then
    (cd "$(dirname "$bundle")" &&
     sha256sum -c "$name.sha256" && sudo -v &&
     tar -xzf "$name" && sh "${name%.tar.gz}/install.sh")
else
    printf '%s\n' 'Bundle not found in the update cache.' >&2
fi
```

If the worker records a negative sudo subprocess status, sudo was terminated
by a signal. For example, `-1` is `SIGHUP`. Read the update service journal to
identify the interruption and the command that was running.

## Installation transaction

1. Download the full archive and checksum over HTTPS from this repository.
   Check exact size/SHA256 and reject unexpected hosts/filenames/versions.
2. Start `apply-update.py` in a transient **user** systemd service, separate from
   keycheck.service. Extract to a unique cache job; reject traversal, links,
   special files, duplicates, archive-size violations and bad manifests.
3. Check the device and select the application-only or system setup path before
   signaling readiness. Application-only updates take the launch lock shared
   with physical buttons. Cancellation stops the worker before installation.
4. Wait for the original app to exit. For application-only updates, stage the
   entire payload with the verified setup module, preserve configuration and
   swap the directory. The previous app remains in `app-backup-*` beside the
   installation. Staging restores it if the final directory rename fails.
5. For system setup, use the existing full snapshot, setup and rollback
   workflow with noninteractive sudo. The user service relaunches the app.

Automatic setup uses existing OS dependencies; new OS dependencies require the
manual full installer. A failed restore, power loss or filesystem failure can
still require manual recovery. This is not an OS/firmware updater.

Jobs: `~/.cache/CurtantControl/updates/<id>/`; each includes the archive, extracted
bundle, status, and its `.rollback-target`. Durable result:
`~/.local/share/CurtantControl/update-status.json` (shown in Updates).
Logs: `journalctl --user -u 'curtantcontrol-update-*' --no-pager`.
Keep backups until the new version has been verified; they are not auto-deleted.

From 0.11.3, the app removes completed download jobs after reopening. Failed
and active jobs stay available for diagnosis. Full-install rollback tools and
their backup reference are copied to `~/.local/share/CurtantControl/recovery/`
before the installer files are removed. `sh install.sh` prints the recovery
command and cleans the verified files in its standard extracted directory and
the matching archive next to it. Manual small updates clean their ZIP, checksum
and matching extracted files. Configuration, snapshot archives and file backups
are outside the cleanup paths.

## Publish the next version

Commit and push the reviewed source first, then build with that clean commit:

```powershell
./scripts/Publish-DeviceFinal.ps1 -Version 0.11.1
python scripts/publish-github-release.py 0.11.1
```

The publisher uses the local Git Credential Manager account, never copies its
credential to Pi or source, and creates a draft until both archive/checksum
assets are uploaded and verified. It resumes a matching draft after interruption.
Pass `--notes-file PATH` to prepend a Markdown summary for the new version.
Published releases are not overwritten. Tags must be `v0.11.1` and assets:

```text
CurtantControl-Final-v0.11.1-pi-buster-arm.tar.gz
CurtantControl-Final-v0.11.1-pi-buster-arm.tar.gz.sha256
```

Package/source version comparison ignores the `-buster` build suffix and Git
revision. Do not publish an archive built from a different source commit.
Checksums provide corruption detection; trust is the chosen GitHub repository
and HTTPS, not a separate offline signing key.

To include a small update, keep the extracted full builds for both versions in
`artifacts/bundles` and run the following in place of the publish command above:

```powershell
python scripts/build-small-update.py 0.11.0 0.11.1
python scripts/publish-github-release.py 0.11.1 --small-from 0.11.0
```

The patch builder compares full builds and includes only changed app files.
Publish the full bundle alone if runtime, assets, keycheck or installer changes
require it.

Host tests cover feed filtering/versioning, HTTP/checksum failures, startup
single-check/nonblocking behavior, Developer gates, cancellation, prepare-before-
close, archive safety, readiness-before-parent-exit, and restore on install error.
The actual Pi systemd/sudo/X11 install/restart path still requires device testing.
