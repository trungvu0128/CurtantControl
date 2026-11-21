# CurtantControl @VERSION@ for Raspberry Pi 4

Device-testing release for **@BITS@-bit Raspberry Pi OS Desktop (@ARCH@)**, with
X11 or XWayland available. Not for headless OS Lite. The app bundles
the .NET runtime; installing a .NET SDK/runtime on the Pi is unnecessary.

## Install native dependencies

From a terminal on the Pi:

```bash
uname -m
getconf LONG_BIT
cat /etc/os-release
sudo apt update
sudo apt install -y ffmpeg v4l-utils libx11-6 libice6 libsm6 libfontconfig1 ca-certificates libc6 libgcc-s1 libgssapi-krb5-2 libstdc++6 tzdata zlib1g
```

`getconf LONG_BIT` must show `@BITS@`. A 64-bit kernel (`uname -m` = `aarch64`) can
still run a 32-bit OS; select packages by OS bitness. Install ICU/OpenSSL for your OS:

```bash
# Raspberry Pi OS Bookworm (Debian 12):
sudo apt install -y libicu72 libssl3

# OR Raspberry Pi OS Trixie (Debian 13):
sudo apt install -y libicu76 libssl3t64
```

These are the native .NET/Avalonia prerequisites plus camera tools. See the
[official .NET dependency list](https://github.com/dotnet/core/blob/main/release-notes/10.0/dotnet-dependencies.md)
for other distributions. Do not run both OS-specific commands blindly.

## Extract and launch

Copy the archive and its `.sha256` file to the Pi. From the folder containing them:

```bash
sha256sum -c CurtantControl-v@VERSION@-@ARCH@.tar.gz.sha256
tar -xzf CurtantControl-v@VERSION@-@ARCH@.tar.gz
cd CurtantControl-v@VERSION@-@ARCH@
sh ./start.sh
```

Run from a terminal **inside the graphical desktop**, as your normal user. Keep
all extracted files together. The launcher sets the executable permission for you.
The first run uses simulated relays; connect a USB webcam and use Refresh above
the preview to select it. Camera discovery requires permission to access the
video device. Missing cameras or tools display errors in the preview area.

## Desktop icon shortcut

Keep the extracted folder in a permanent location. From that folder, run once as
your normal desktop user (no sudo):

```bash
python3 install-shortcut.py
```

The installer adds a camera icon shortcut on your desktop and in the application
menu. Double-click CurtantControl to launch without a terminal. If your desktop
asks whether to execute/trust the launcher, choose Execute/Allow Launching. If
Python is missing, install it with `sudo apt install python3`.

The shortcut points to this exact extracted folder. Rerun the installer after
moving it or switching to a new release folder. If desktop icons are disabled,
use the application-menu entry instead. Existing native dependencies are still
required. This uses the [Desktop Entry specification](https://specifications.freedesktop.org/desktop-entry/latest-single/).

## Physical relays

The shipped `photobooth.json` has `SimulateRelays: true`. Its existing GPIO mapping
and active-low setting are configuration examples, not verified wiring. Check
BCM pin mapping, relay trigger polarity, 3.3 V interface compatibility, and actuator
timing before setting `SimulateRelays` to false. The shipped 200 ms activation is
an example, not a validated motor travel time. Never apply the relay board's 24 V
supply to Pi GPIO. Restart after editing the file.

Use the gear icon to set curtain colors from front to back. Saved order is kept
per user at `<LocalApplicationData>/CurtantControl/curtain-order.json` and survives
extracting a later release. Unsaved logs and simulated positions reset at restart.

## Included features and validation

- Four curtain colors, paired open/close relay control, stop, and temporary logs.
- Fixed-position color dropdown settings with persistent order and Saved feedback.
- USB-camera discovery, selection, refresh, and single-stream live preview.
- Self-contained @ARCH@ executable; managed packages and native runtime bundled.

Windows-hosted automated checks cover relay logic, setup persistence, camera
discovery parsing, switching, and cleanup using simulated outputs/capture sessions.
The executable and key native libraries are verified against the target ELF architecture. Actual Raspberry Pi startup,
GPIO switching, camera compatibility, and display performance are not yet verified.

See `docs/PHOTOBOOTH_UI.md` and `docs/RELAY_CONTROL.md` for more detail.
