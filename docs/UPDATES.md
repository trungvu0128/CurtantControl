# GitHub updates

The app checks `trungvu0128/CurtantControl` GitHub Releases once each time its
window opens. The HTTP check runs asynchronously alongside camera startup, with
a15s limit. Offline startup stays usable. There is no background auto-install.
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
user `pi`. It requires the user's systemd session bus and existing noninteractive
sudo permission. It never changes sudoers. Windows can check but cannot install
the Pi bundle. Missing dependencies/permissions are rejected before app close;
the full manual installer is the fallback.

## Installation transaction

1. Download the full archive and checksum over HTTPS from this repository.
   Check exact size/SHA256 and reject unexpected hosts/filenames/versions.
2. Start `apply-update.py` in a transient **user** systemd service, separate from
   keycheck.service. Extract to a unique cache job; reject traversal, links,
   special files, duplicates, archive-size violations and bad manifests.
3. Verify Pi/OS/architecture, native libraries, disk space and sudo before signaling
   readiness. Keep old app open until ready. Cancellation writes a durable marker
   and stops the worker; the worker checks this marker before installation.
4. Close the app gracefully. Worker waits for its original PID to exit (30s max).
   Only then run full setup with `--skip-packages`, preserving the normal full
   backup, configuration/PIN/state and keycheck/shortcut migration. A centered
   progress popup appears during this phase when Tk is available.
5. On installer failure, attempt snapshot restore and keycheck restart. Restart
   the installed/restored app by exec in the user unit. No forced app kill or
   install while the old process still holds files/hardware.

Automatic setup uses existing OS dependencies; new OS dependencies require the
manual full installer. A failed restore, power loss or filesystem failure can
still require manual recovery. This is not an OS/firmware updater.

Jobs: `~/.cache/CurtantControl/updates/<id>/`; each includes the archive, extracted
bundle, status, and its `.rollback-target`. Durable result:
`~/.local/share/CurtantControl/update-status.json` (shown in Updates).
Logs: `journalctl --user -u 'curtantcontrol-update-*' --no-pager`.
Keep backups until the new version has been verified; they are not auto-deleted.

## Publish the next version

Commit and push the reviewed source first, then build with that clean commit:

```powershell
./scripts/Publish-DeviceFinal.ps1 -Version 0.11.1
python scripts/publish-github-release.py 0.11.1
```

The publisher uses the local Git Credential Manager account, never copies its
credential to Pi or source, and creates a draft until both archive/checksum
assets are uploaded and verified. It resumes a matching draft after interruption.
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
