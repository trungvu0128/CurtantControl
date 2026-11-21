# CurtantControl @VERSION@ - Raspbian Buster compatibility candidate

This package keeps your existing **32-bit Raspbian GNU/Linux 10 (Buster)**.
It targets `linux-arm` using a self-contained .NET 8 runtime, rather than .NET 10.
You do not need to install .NET or replace GLIBC on the Pi.

This is an experimental legacy compatibility build, not a supported Buster runtime
configuration. Buster is outside .NET's supported OS list; .NET 8 support ends on
November 10, 2026. Continuing to update application code does not extend runtime
or operating-system security support.

## Install prerequisites

```bash
sudo apt update
sudo apt install -y ffmpeg v4l-utils x11-utils libx11-6 libice6 libsm6 libfontconfig1 libicu63 libssl1.1 libgssapi-krb5-2 zlib1g libstdc++6
```

These package names target Buster's native repositories. If apt cannot locate or
download them, send the error; do not mix packages from newer Debian releases or
manually overwrite libc.so.6. Other required base runtime libraries normally come
with the OS. Do not reinstall the OS for this candidate test.

## Extract and start

Extract as your normal desktop user, without sudo, into a writable folder:

```bash
tar -xzf CurtantControl-v@VERSION@-linux-arm.tar.gz
cd CurtantControl-v@VERSION@-linux-arm
sh start.sh
```

The launcher checks 32-bit userspace using `getconf LONG_BIT`, even if the kernel
reports aarch64. It can recover a missing DISPLAY only when xdpyinfo successfully
accesses an existing local X socket using your user's authentication. It cannot
create a desktop session or grant access to another user's display.

If startup still fails, run `./CurtantControl` directly in this extracted folder
and send its complete output. A missing shared library or display error must be
resolved separately. Never launch the application with sudo.

## Ribbon / CSI camera preview

### Existing Python/React camera stream (preferred when already running)

If `CameraStreamUrl` is set in photobooth.json, the app uses that HTTP/MJPEG feed
instead of trying to open a physical camera. For the user's existing Flask server:

```json
"CameraStreamUrl": "http://127.0.0.1:5000/video_feed"
```

The dropdown shows Existing camera stream (Python / MJPEG). Keep the Python camera
service running; do not kill it. For the first test, close the React/browser preview
so only Avalonia consumes the stream, since the Python handler's multi-client
safety has not been verified. FFmpeg remains required, but raspivid is not started
in this mode. No other Python API/control endpoints are invoked.

The HTTP reader accepts JPEG frames even when the existing server's multipart
boundaries are incompatible with FFmpeg. It extracts complete JPEGs in .NET and
pipes them into FFmpeg for decoding, instead of FFmpeg parsing the HTTP multipart
envelope. Frame/header waits are bounded; errors appear in the activity log.

### Direct legacy capture (when no other service owns the camera)

This version adds Buster's legacy CSI capture path. With a working `raspivid` and
`vcgencmd get_camera` reporting supported=1 detected=1, select **Raspberry Pi ribbon
camera (legacy CSI)** above the preview. Do not select bcm2835 codec/ISP nodes;
these internal processing devices are now filtered from discovery.

The app captures H264 from raspivid (640 x 360, 15 fps, no overlay) and pipes it to
FFmpeg for BGRA preview frames. Both processes stop on camera switching, reconnect,
and shutdown. Legacy detection and pipeline arguments are checked automatically,
but actual CSI video still needs a Pi test.

Check the camera independently, with the app and other camera users closed:

```bash
command -v raspivid
vcgencmd get_camera
raspivid -n -t 2000 -w 640 -h 360 -fps 15 -o /dev/null
```

Camera failure details now appear in the activity log. This path does not support
the modern libcamera/rpicam stack or newer sensors that require it.

## Icon shortcut

After a successful launch:

```bash
python3 install-shortcut.py
```

This replaces the earlier shortcut to point to this release's folder. Keep this
folder in place. If needed, choose Execute/Allow Launching on first use.

## Relay mode and settings

Simulation starts with all four curtains closed and all relay outputs off. The
package keeps simulation enabled. Verify wiring, polarity, 3.3 V compatibility,
and actual actuator timing before enabling physical outputs in photobooth.json.
Saved color order is shared with previous releases for the same desktop user.

## Verification limits

The candidate is compiled for net8.0 using the stable .NET 10 SDK; modern builds
continue targeting net10.0. Windows-hosted verification checks exercise the
net8.0 application assembly from the net10.0 test runner. No ARM32 runtime was
executed on the build host.

Native executable/runtime/graphics libraries are ARM32. Native-file symbol-tag
scans reject GLIBC requirements above 2.28 and GLIBCXX above 3.4.25. Passing these
scans is not proof of complete system compatibility: shared libraries, driver
permissions, camera/display behavior, and hardware must still be tested on the Pi.

References: [.NET 8 runtime requirements](https://github.com/dotnet/core/blob/main/release-notes/8.0/supported-os.md),
[Avalonia net8.0 target](https://www.nuget.org/packages/Avalonia/12.1.5),
[.NET 8 support end date](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).
