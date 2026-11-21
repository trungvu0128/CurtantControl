# CurtantControl installation bundle

For Raspberry Pi 4 with a graphical Raspberry Pi OS desktop:

- Buster / Debian 10, 32-bit: legacy .NET 8 candidate.
- Bookworm / Debian 12 or Trixie / Debian 13, 32-bit or 64-bit: .NET 10.
- Other releases, Raspberry Pi 5 and OS Lite are not covered by this installer.

Extract the archive as your desktop user, open a terminal in its folder, then:

```bash
sh install.sh
```

Internet and sudo access are required to install OS packages. The app and .NET
runtimes are included; this is not an offline OS dependency bundle. Python 3
(normally installed on Raspberry Pi OS) is required to run setup. The installer
automatically chooses the payload by OS release and dpkg userspace architecture,
checks payload hashes, installs signed packages from your configured apt sources,
checks native libraries, and adds your user to existing gpio/video groups.
On Buster only, it backs up and replaces active mirrors.aliyun.com/raspbian[/raspbian] Buster entries in sources.list and sources.list.d/*.list with https://legacy.raspbian.org/raspbian/. Other repositories, firmware, display setup and camera services stay unchanged. Signature checks remain enabled.
If apt reports another stale mirror, correct that source and rerun; do not mix OS releases. Repository backups have a .curtant-backup-TIMESTAMP suffix beside the source file. Preflight and --skip-packages do not edit repositories.

After success, use the desktop/menu shortcut. If group membership changed, log out
and back in first. The app is not launched as root. Allow Launching if requested.
An X11/XWayland desktop is required; setup does not create a display session.

Installed location: `~/.local/share/CurtantControl/app/`.
You may delete the extracted setup bundle after successful installation.

## Settings and updates

New installs use the explicitly supplied device profile: hardware relays enabled,
BCM pins [22,10,9,11,5,6,13,19] in channel order, ActiveLow=false,
200ms activation,1000ms maximum and100ms break-before-make. Python feed URL is
http://127.0.0.1:5000/video_feed. Fresh palette layer order is R1/R2 nearest,
then R3/R4,R5/R6,R7/R8, matching the supplied configuration. GPIO/polarity values
are user-provided, not independently verified from wiring. See photobooth.json
in the installed folder. Developers can build a simulation-only generic bundle
using Publish-SetupBundle.ps1 -GenericDefaults with a new version.

To update, close the app and rerun the new bundle's installer. The existing
photobooth.json and colors.json are preserved byte-for-byte, including hardware mode, colors and saved relay-layer order. The old app
folder/config is retained beside it as app-backup-TIMESTAMP. Saved curtain order
also remains untouched. Backups are not automatically removed.

Preflight only (no changes): `sh install.sh --check`.
If OS dependencies are already present: `sh install.sh --skip-packages`.
Skip-packages still checks native libraries and may request sudo for group changes.

## Cameras and GPIO

Automatic camera mode tries the configured/local MJPEG service, direct USB/V4L2,
then legacy CSI through raspivid. Existing services are never stopped. Modern
libcamera/rpicam CSI is not implemented: use USB or an existing MJPEG service on
Bookworm/Trixie. Legacy raspivid availability is checked but firmware/camera mode
is not changed. A Python service must not also write the same relay GPIO pins.

Check relays reports command signals, not contacts or curtain positions. The app
sends bounded relay pulses; it does not detect actuator limits. There is no STOP button in either screen. Internal shutdown attempts GPIO off;
this cannot guarantee stopping a controller that latches a pulse.

Read-only diagnostics (no GPIO toggling or camera capture):

```bash
sh ~/.local/share/CurtantControl/app/diagnose.sh > ~/curtant-diagnostics.txt 2>&1
```

Review the report before sharing. Configuration contents and process arguments
are omitted. For a launch error, also run `sh ~/.local/share/CurtantControl/app/start.sh`
inside a desktop terminal and copy the error output.

Buster is an unsupported OS configuration; .NET 8 support ends 2026-11-10.
Host packaging and tests do not establish successful runtime behavior on your Pi.


From camera-activation updates onward, a disabled local Python feed (204) may be enabled automatically through /camera_control mode=1 after the service's availability check accepts it. Set EnablePythonCameraActivation=false in photobooth.json to opt out. This is not an OS permission probe; other service control/GPIO routes are untouched.


## Current application workflow

Starts full screen in User mode. Hold the bottom-right corner for three seconds
while idle to open the mode chooser. Both modes preview a selected sample for two
seconds and apply curtains only on START. Developer adds logs/status/camera selection.
Five buttons are sorted nearest to farthest from the camera, fixed background last.
Settings edits five colors; only Developer Settings shows the nearest relay-pair
order selector. Save applies without movement and persists in colors.json.
Completed matching motor commands are skipped; saved/unknown estimates are reasserted.
The bundle includes all18 sample images, colors.defaults.json, runtime/native files,
shortcut installer, launcher and read-only diagnostics. See HUONG-DAN.md for Vietnamese.
