# Releases

## 0.4.0 startup modes - 2026-11-06

Adds startup Developer/User chooser. Developer retains prior controls. User provides reference-style dark glossy colors and preview, hides logs/diagnostics, stages selection until START, and retains Stop/Close all. Modes returns while idle; no security/password gate. Camera starts after first mode choice and is reused. Entering User clears old temporary logs. Shared wiring/palettes/state persistence unchanged.

Patch: artifacts/updates/CurtantControl-v0.4.0-buster-update.zip; preserve photobooth.json/colors.json. Legacy/modern Debug/Release,39 checks, ARM32/native+ARM64 publish, patch hashes/dependency comparison and headless visual checks passed (1280x760; User1024x600). Synthetic camera pattern only; physical Pi validation pending.

## 0.3.1 saved background selection - 2026-11-06

Infers startup selected background from saved front-to-back states: Opened with all foreground Closed. Unknown foreground prevents selection; all Closed selects none. Updates saved highlight/label/dot without relay pulses. First explicit re-selection remains available. Patch: artifacts/updates/CurtantControl-v0.3.1-buster-update.zip; preserve both config JSONs. Legacy/modern Debug/Release,37 checks, ARM32/ARM64 publishes and patch ZIP verification passed. Hardware status remains unverified.

## 0.3.0 saved command estimates - 2026-11-06

Persists per-curtain last-command estimates in separate hardware/simulation user files. Restores annotated saved labels without moving relays or suppressing first commands. Pending/canceled/faulted commands persist Unknown; pending-write failure blocks new pulse. No physical feedback inference. Patch: artifacts/updates/CurtantControl-v0.3.0-buster-update.zip; preserve photobooth.json/colors.json. Legacy/modern Debug/Release,36 checks, ARM32/native scans, ARM64 publish and patch ZIP checks passed. Actual Pi acceptance pending.

## 0.2.9-buster - 2026-11-06

Main buttons/status list now follow Settings front-to-back order. Button labels01..04 indicate display levels; physical curtain/relay IDs stay fixed. Patch: artifacts/updates/CurtantControl-v0.2.9-buster-update.zip. Keep photobooth.json/colors.json. Debug/Release,33 checks, ARM32 publish/native scans and ZIP verification passed.

## 0.2.8-buster - 2026-11-06

Close all no longer sends commands when all four command statuses are Closed. Unknown startup still closes; no physical position inference. Patch: artifacts/updates/CurtantControl-v0.2.8-buster-update.zip. Keep photobooth.json/colors.json. Debug/Release,33 checks, ARM32 publish/native scans and ZIP verification passed.

## 0.2.7-buster - 2026-11-06

Removes right-side color editor. Existing four dropdowns use full palette and update only their fixed curtain colors. Duplicate assigned keys are rejected without swapping. Save persists colors and stays on setup; layer order and relays unchanged. Patch: artifacts/updates/CurtantControl-v0.2.7-buster-update.zip. Keep photobooth.json/colors.json; starter colors.example.json provided. Debug/Release,32 checks, ARM32 publish/native scans, ZIP checks and setup visual inspection passed.

## 0.2.6 configurable palette - 2026-11-06

Adds external colors.json palette with extensible choices and four fixed physical curtain assignments. Settings Curtain colors dropdowns and Save colors update appearance without moving relays or changing order. Source installer preserves custom palette during updates. Small Buster update: artifacts/updates/CurtantControl-v0.2.6-buster-update.zip (three managed files plus starter colors.json). Preserve photobooth.json and any existing custom colors.json.

Legacy/modern Debug/Release,32 app checks, five installer checks, ARM32/ARM64 publishes, native scans, patch ZIP/dependency verification and setup visual inspection passed. Pi validation pending.

## 0.2.5-buster - 2026-11-06

Selection-label dot matches the selected curtain color; gray when no selection/Close all. Patch: artifacts/updates/CurtantControl-v0.2.5-buster-update.zip; retain photobooth.json. Debug/Release,31 checks, ARM32 publish/native scans, ZIP hashes and light layout visual check passed.

## 0.2.4-buster - 2026-11-06

Fixes Reconnect camera contrast on hover/press with dedicated theme colors. Light mode uses pale blue and dark text. Debug/Release,31 checks, ARM32 publish/native scans, ZIP verification and actual AXAML pointer-hover visual check passed. Patch: artifacts/updates/CurtantControl-v0.2.4-buster-update.zip. Preserve photobooth.json.

## 0.2.3-buster - 2026-11-06

Already-selected color clicks no longer repeat relay commands. Choosing another color or Close all allows selecting it again. Small update: artifacts/updates/CurtantControl-v0.2.3-buster-update.zip; preserve photobooth.json. Debug/Release,31 checks, ARM32 publish/native scans and ZIP hashes passed. Physical Pi validation pending.

## 0.2.2-buster appearance - 2026-11-05

Adds Dark/Light switch beside Settings, immediate shared theme resources for main/setup, and per-user appearance.json persistence independent of hardware settings. Small update: artifacts/updates/CurtantControl-v0.2.2-buster-update.zip. Preserve photobooth.json. Legacy Debug/Release,31 checks and ARM32 publish passed; actual AXAML dark/light main and light setup visually checked, update ZIP hashes verified. No physical Pi visual test.

## 0.2.1 camera activation - 2026-11-05

On204 from the known local Python video feed, request camera mode1 and require type=camera before one feed retry. EnablePythonCameraActivation defaults true; false disables activation. No separate OS permission API exists. Unrelated URLs receive no POST. Redirects disabled; cancellation and bounded activation apply. No Python source or GPIO route changes.

Existing Buster install update: artifacts/updates/CurtantControl-v0.2.1-buster-update.zip. Preserve photobooth.json. Pi-only bundle: artifacts/bundles/CurtantControl-Setup-pi-camera-activation.tar.gz, including repository repair. Original universal0.2.0 archive unchanged. Legacy/modern Debug/Release and30 checks passed, plus ARM32/ARM64 publishes, native checks, unchanged dependencies and patch/bundle hash checks. Physical Pi verification pending.

## 0.2.0 setup bundle - 2026-11-05

Artifact: artifacts/bundles/CurtantControl-Setup-v0.2.0.tar.gz (119.0 MiB), with matching SHA256 file. Includes Buster10 ARM32 net8 and Debian/Raspberry Pi OS12/13 ARM32/ARM64 net10 payloads. Extract, then run sh install.sh as the desktop user. Online apt dependency installation, GPIO/video group checks, fixed per-user install path, shortcut creation and read-only diagnostics are included. No offline OS dependency packages are included.

Fresh bundle settings have simulation enabled, empty GPIO mapping and null polarity. Existing installed configuration is preserved; the old installation remains in a timestamped backup. Unsupported OS/architecture/board combinations are rejected. Setup does not alter repositories, firmware, external camera services or display sessions.

All three Debug/Release builds, 28 application checks per variant, four host installer tests, ARM32/ARM64 native validation, archive checksum/extraction and payload hash/config checks passed. Actual apt installation, desktop shortcut and native execution on a new Pi remain untested. Modern CSI needs an existing MJPEG service; rpicam/libcamera capture is not implemented. See packaging/bundle/README.md.

Bundle SHA256: 73c077905c6df8033294f30d9a7eb495870bdabdd05ce820951803920f708761

## 0.1.10-buster (linux-arm) - 2026-11-05

Fixes silent Check relays when initialization failed. Each click reports the saved setup error or logs readback/completion. Hardware configuration is unchanged. Small update: artifacts/updates/CurtantControl-v0.1.10-buster-update.zip; retain photobooth.json. Legacy Debug/Release builds, all 28 checks, ARM32 publish/native scans and ZIP extraction/hash verification passed. Real GPIO testing remains pending.

## 0.1.9-buster (linux-arm) - 2026-11-05

Automatic camera tries the configured/local HTTP feed, then USB/V4L2, then legacy CSI. Failed sessions clean up before fallback; exhausted choices rescan every five seconds. Manual choices remain manual. Existing processes are never stopped. Modern rpicam/libcamera is not supported.

Small update: artifacts/updates/CurtantControl-v0.1.9-buster-update.zip. Preserve photobooth.json. Legacy Debug/Release, 27 checks, ARM32 publish/native scans, unchanged dependencies and ZIP extraction/hash verification passed. Physical Pi tests remain required.

## 0.1.7-buster (linux-arm) - 2026-11-05

Main window starts maximized. Replaces the fixed 1200x800 Viewbox canvas with a
window-filling grid: compact curtain column (240..400 px), flexible camera column,
and aligned diagnostics below. Curtain buttons stretch evenly in their rows and
retain bold centered labels. Preview uses proportional fill and the decoder
center-crops to 640x360 instead of baking letterbox bars into frames. Edges may
be cropped; no aspect-ratio distortion is introduced.

Small update: `artifacts/updates/CurtantControl-v0.1.7-buster-update.zip` (93.6 KiB),
with the same three managed files plus UPDATE.txt. Preserve photobooth.json.
Full package is under `artifacts/releases/` with matching version and SHA256.

Legacy Debug/Release builds, all 24 existing checks, native compatibility scans,
and update ZIP extraction/hash verification passed. Actual AXAML was rendered
and visually checked at 1024x600, 1280x760, and 1920x1080. A generated gradient
verified preview fill; it was not a camera stream. Minimum window size is 800x600.
Actual Pi appearance/playback remains a device acceptance check.

Full archive SHA256:

```text
e85a2f502751d798e4caed8f4828094a71cb8ffd274f8720c97b9e926008dbd7
```

## 0.1.6-buster (linux-arm) - 2026-11-05

Addresses on-device `Expected boundary '--' not found` failures when FFmpeg parses
the existing Python feed's multipart response. HTTP is now read in .NET; complete
JPEG frames are extracted independently of multipart boundary syntax and piped
into FFmpeg's raw MJPEG decoder. Length-delimited JPEG metadata segments, escaped
entropy bytes, and restart markers are preserved. Frame size is bounded to 8 MiB;
HTML/JSON responses and streams without complete JPEGs produce clear errors.

Artifacts: full `artifacts/releases/CurtantControl-v0.1.6-buster-linux-arm.tar.gz`
and small `artifacts/updates/CurtantControl-v0.1.6-buster-update.zip` (93.3 KiB).
Update requires replacing the same three managed application/dependency/runtimeconfig
files. Keep existing photobooth.json, including CameraStreamUrl and relay values.

Legacy Debug/Release builds, all 24 behavior/HTTP/JPEG parsing checks, native
compatibility scans, and update ZIP hash/extraction verification passed. Dependencies
match v0.1.5-buster. Tests simulate malformed multipart responses and fragmented
JPEG bytes; they do not establish actual playback from the user's Python server.

Full archive SHA256:

```text
2a815440f45be5b37a612555f9cfb1de78edc119f7f23ff23d699eb60350eef7
```

## 0.1.5-buster (linux-arm) - 2026-11-05

Adds configured HTTP/MJPEG input to reuse the user's existing Python/React camera
service at `http://127.0.0.1:5000/video_feed`. The source screenshot confirms Flask
GET `/video_feed` returns multipart JPEG and the server listens on port 5000.
With CameraStreamUrl set, discovery returns only the configured stream and skips
V4L2/raspivid detection. FFmpeg reads HTTP with a ten-second network timeout; no
physical camera is opened and no other Python API/control route is invoked.

Artifacts:

- Full package: `artifacts/releases/CurtantControl-v0.1.5-buster-linux-arm.tar.gz`.
- Small update: `artifacts/updates/CurtantControl-v0.1.5-buster-update.zip` (90.3 KiB).

The update contains three matching application/dependency/runtimeconfig files
and UPDATE.txt. Existing v0.1.4-buster runtime dependencies are unchanged. Preserve
the device's photobooth.json and add CameraStreamUrl manually; the full package
has the local URL configured and simulation enabled. Close the browser preview
for initial testing, but keep the Python camera service running. Its multi-client
camera safety is unknown.

Legacy Debug/Release builds, all 22 behavior/routing checks, native compatibility
scans, and update ZIP extraction/hash verification passed. Tests cover stream-mode
discovery bypass and HTTP routing without actual FFmpeg or Pi camera capture.
Live stream decoding still needs a device test.

Full-package SHA256:

```text
aaa5f988ec49b41b43e0c586e522a0e843d9f6116b9f1578fdcae52973389178
```

## 0.1.4-buster (linux-arm) - 2026-11-05

Adds a legacy CSI ribbon-camera backend for the user's confirmed hardware:
`/usr/bin/raspivid` exists and firmware reports supported=1 detected=1,
libcamera interfaces=0. Discovery adds Raspberry Pi ribbon camera (legacy CSI),
ID `raspivid://0`. The capture pipeline is raspivid H264 stdout into FFmpeg stdin,
with decoded BGRA frames passed to the Avalonia preview. Both child processes are
cancelled and awaited on pipeline cleanup. No shell command composition is used.

Fixes device discovery that incorrectly admitted bcm2835 codec/ISP processing
nodes. Uses numeric per-node capability flags and excludes known pipeline nodes.
Full bounded camera errors and relay initialization errors now reach the activity
log. Curtain position semantics remain unchanged; real startup positions are Unknown.

Artifact: `artifacts/releases/CurtantControl-v0.1.4-buster-linux-arm.tar.gz`
(37.74 MiB). Legacy Debug/Release builds, all 21 behavior/routing/filter checks,
native compatibility scans, and modern Debug build passed. All 227 archived files
matched staging after extraction. Tests do not execute raspivid or actual CSI
capture on the Windows host; first-frame delivery and camera hardware cleanup
still need device verification. Package starts in simulation mode.

SHA256:

```text
63ce53f68d92bcf37f5401ffe073714200fdb21d3defb15d2b54762b32323f1d
```

## 0.1.3-buster (linux-arm) - 2026-11-05

Experimental candidate for keeping the user's Raspbian 10 Buster installation.
Uses .NET 8.0.33 ARM32, preserving Avalonia 12.1.5, GPIO 4.2.0, and application
features. Default development remains net10.0; `-LegacyBuster` opts into net8.0.
This build does not modify the device OS or bundle/replace GLIBC.

Artifact: `artifacts/releases/CurtantControl-v0.1.3-buster-linux-arm.tar.gz`
(37.73 MiB), with `.sha256`. Includes Buster-specific prerequisites/readme, icon
installer, and a launcher that checks accessible local X sockets using xdpyinfo
when DISPLAY is empty, without bypassing X authentication.

Legacy Debug/Release builds and all 20 behavior checks passed; the net10.0 test
runner references the net8.0 app. Modern default Debug build also passed.
Sixteen native files were scanned; highest observed GLIBC tag was 2.27, below
Buster's standard 2.28. GLIBCXX tag ceiling checks, ARM32 machine checks, archive
extraction, and checksum passed. These static checks do not replace device testing.

Buster is not a supported .NET OS configuration. .NET 8 support ends November 10,
2026; this is a legacy test option, not a supported long-term replacement for
an OS/runtime upgrade. [Runtime baseline](https://github.com/dotnet/core/blob/main/release-notes/8.0/supported-os.md),
[support date](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).

SHA256:

```text
ed52d68d9e9932cc0f2306bd07ff7bc76aca291fb9f2dd7239f03f0fd6c08931
```

## 0.1.2 (linux-arm) - 2026-11-05

Adds `RaspberryPi32` and a 32-bit ARM release for the user's confirmed 32-bit OS,
even though its kernel reports aarch64. The v0.1.1 ARM64 executable could not load
because `/lib/ld-linux-aarch64.so.1` was absent. The new executable uses
`/lib/ld-linux-armhf.so.3`. OS bitness is now checked with `getconf LONG_BIT`.
Launcher skips chmod when the binary is already executable, avoiding failures
with executable files owned by another user.

Archive: `artifacts/releases/CurtantControl-v0.1.2-linux-arm.tar.gz`, plus SHA256.
Debug/Release builds and all 20 checks passed. Executable, core runtime, Skia,
and HarfBuzz were verified as ARM ELF. Archive extraction/checksum and ELF32 loader
verification passed. Physical device startup and graphical-session setup remain pending.

Build explicitly with `-Architecture arm`; `-Architecture arm64` remains available.
Do not choose packages from `uname -m` alone. Use a modern Raspberry Pi OS with
the native dependencies listed in the packaged README.

Device test subsequently reached an older libc and failed with GLIBC_2.34 not
found. .NET 10 portable ARM32 requires glibc 2.35 or newer; the user's OS version
is still being determined. This package is not compatible with that existing
userspace. See [official runtime requirements](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

SHA256:

```text
b26b757fd67722c31de77f300982076c22825a38264eebc6861cfe0a9a08cbde
```

## 0.1.1 - 2026-11-05

Adds the camera icon and `install-shortcut.py`. On the Pi, run
`python3 install-shortcut.py` from the extracted folder as the desktop user. It
installs desktop and application-menu entries pointing at that folder, using
`Terminal=false` and the bundled icon. Keep the folder in place, or rerun the
installer after moving/upgrading. The desktop may require Allow Launching on
first use. Python 3 is required only to install the shortcuts.

Package: `artifacts/releases/CurtantControl-v0.1.1-linux-arm64.tar.gz` and its
`.sha256` file. Debug/Release builds, all 20 app checks, ARM64 publish, archive
checksum/extraction, and presence of shortcut assets passed. Shortcut installation
and launch on the physical Pi remain untested. A usable Python interpreter was
not available on this Windows host for executing the installer.

SHA256:

```text
e9eb6a536bbb9058b22631a01e61dfd6990a46ef2403f9508aba44308771f7a0
```

## 0.1.0 - 2026-11-05

Initial Raspberry Pi 4 device-testing release, targeting `linux-arm64` on 64-bit
Raspberry Pi OS Desktop with X11/XWayland. Self-contained .NET 10.0.14 runtime.

Artifacts (generated locally, not committed):

- `artifacts/releases/CurtantControl-v0.1.0-linux-arm64.tar.gz` (41.13 MiB).
- Matching `.tar.gz.sha256` checksum file.
- Extracted package in `artifacts/releases/CurtantControl-v0.1.0-linux-arm64/`.

The package includes `README.md`, `start.sh`, `VERSION.txt`, `photobooth.json`,
and relay/UI documentation. It starts in simulation mode. Existing configured
BCM pins and polarity are preserved as examples; physical compatibility and
timing must be checked before enabling GPIO.

Validation: Debug/Release builds passed with zero warnings/errors; all 20
relay/background/UI/camera checks passed. Published executable is AArch64 ELF,
managed assembly version is 0.1.0.0, and all 229 archived files matched staging
after extraction. SHA256 verified. The application has not been run on a physical Pi.

SHA256:

```text
06bea416d13d455d29551a8e5ad0e9b723194ead8f2f644fea233f6e5384350d
```

## Produce a subsequent release

From PowerShell at the repository root:

```powershell
./scripts/Publish-RaspberryPi.ps1 -Version 0.1.1
```

The script builds both configurations, runs Release checks, publishes self-contained
ARM64, verifies required native files and executable architecture, stages the
launcher/docs, defaults packaged relays to simulation, and writes a `.tar.gz` plus
SHA256. Existing version directories/archives are not overwritten. The version
argument overrides the project version for that release build.

Use `packaging/raspberry-pi/README.md` as the packaged run guide template. Native
dependencies remain prerequisites on the Pi; FFmpeg and v4l-utils are not bundled.



















## 0.5.0 color sample previews

18 palette choices and matching AI portrait examples; User tap displays sample
for two seconds before returning live, START applies curtains. Four slots retained.
Both Buster ARM32 experimental and modern ARM64 cross-publishes verified with42
behavior checks; headless overlay/return/START checks passed. No physical Pi run.
Small update includes three managed files, colors.defaults.json and Backgrounds.
Preserve photobooth.json and colors.json. A separate photo ZIP is also available.


## Shared Developer photobooth layout (0.5.1)

Developer and User modes now share the glossy curtain buttons, header and camera
layout in UserPhotoboothView. Developer adds the bottom activity log and curtain
command status, camera selector/Refresh, relay checks/Clear, settings and theme
switch. Developer color taps still activate immediately; User retains sample
previews and START. The shared view has separate layout heights, with Developer
allowing room for diagnostics. User stays dark even after a light Developer session.

Verified Debug/Release and 42 checks; Pi cross-publishes do not establish physical device validation.


## 0.5.2 User controls

Centered glossy button content; User gains theme/settings controls, hides STOP.
Removed BACKDROP/PHOTO BOOTH header branding. Developer diagnostics/STOP retained.
Debug/Release,42 checks, ARM32 cross-publish and dark/light renders verified.
Physical Pi validation remains pending. Preserve photobooth.json and colors.json.


2026-11-06 alignment correction (0.5.3): selection dot/message centered together
under left curtain controls. Theme switch enclosed in52px panel; settings and
Modes buttons also52px high. START now uses explicit centered TextBlock rather
than implicit string presenter. Debug/Release legacy builds,42 checks and ARM32
cross-publish passed. Headless User screen reviewed and sample-return/START
behavior verified. Physical Pi untested.


2026-11-06 hover/spacing fix (0.5.4): custom glossy button presenter renamed
PhotoContentPresenter with transparent background so Fluent default pointerover
PART_ContentPresenter background cannot draw an inset rectangle. Rounded outer
border/gradient remains the only button surface; whole-button opacity handles
hover/press. Removed unused header grid column causing doubled gap: theme,
settings and Modes now equal18px spacing. Actual pointer-hover headless render
reviewed. Debug/Release legacy builds,42 checks and ARM32 publish passed. Pi untested.


2026-11-06 shared camera mapping (0.5.5): Developer-selected CameraDevice/session
remains shared when switching to User; no separate camera selection in User.
Header now shows selected device Name, or Auto for automatic://camera, with
sample caption temporarily overriding it. CameraCatalog automatic choice renamed
Auto. SelectedCamera changes notify header. This is session sharing, not new
cross-restart preference persistence. Debug/Release legacy,43 checks (new shared
selection/name/mode-switch concurrency check) and ARM32 publish passed. Pi untested.


2026-11-06 preview layout (0.5.6): removed User helper text below preview,
reduced camera heading25→16px and indicator20→10px, heading row36→22px and
status row auto (Developer status retained). Freed vertical space expands preview.
Settings moved to far-right header after Modes; equal gaps/heights retained.
Debug/Release legacy,43 checks and ARM32 publish passed. Headless User preview
reviewed; photo timeout/START clearing verified. No physical Pi run.


2026-11-06 camera label correction (0.5.7): selected camera name belongs in
left metallic badge, replacing CAMERA RAZR I. CameraDisplayName is Auto for
automatic selection (or not yet selected), otherwise selected device Name,
trimmed visually with full tooltip. Preview heading restored to Live Camera
Preview / Camera preview offline; temporary Sample caption retained. Shared
Developer/User camera session unchanged. Debug/Release legacy,43 checks and
ARM32 publish passed; badge render reviewed. No physical Pi run.


2026-11-06 equal button sizing/START guard (0.5.8): left panel uses five equal
rows with matching margins and32px labels, including Close all. User draft
selection checks current completed-command states: selected Opened and every
foreground Closed means already applied; no pending START. Merely browsing other
colors does not change this state. Actually applying another background allows
returning/reapplying the previous one. Saved estimates with suffix remain
reapplyable. START completion also checks foregrounds for failure/retry instead
of selected Opened alone. No new physical position feedback. Debug/Release legacy,
44 checks (new apply/browse/return/no relay repeat check), ARM32 publish and User
render passed. Physical Pi untested.


2026-11-06 default User startup/corner hold (0.5.9): App explicitly enters User
before creating main window (view-model default Startup retained for chooser/tests).
User top Modes button hidden, Developer retains it. Hidden80x80 bottom-right
view corner opens chooser after3s continuous left/touch hold while idle. Capture,
release/move-out/loss/detach cancellation and generation check prevent stale callback;
shared cancellable timer posts to UI dispatcher. No mode change during movement.
Debug/Release legacy,44 checks, ARM32 publish passed; actual headless pointer
short tap200ms+wait stayed User,3.2s hold opened chooser. Physical Pi touch untested.


2026-11-06 fullscreen startup (0.5.10): App sets main window WindowState.FullScreen
before showing, retaining default User and bottom-right3s mode chooser. Preview
renderer continues to use normal windows for layout capture. Debug/Release,
44 checks and legacy ARM32 cross-publish passed. Physical desktop fullscreen
behavior remains untested on Pi.


## Five-background paper layout (0.6.0)

This supersedes the four-background-plus-Close-all screen. There are five equal
choices: fixed White (Background1), then four configurable colors for Backgrounds
2–5, assigned to M1–M4. No Close all button is displayed. User stages any of five
choices and START applies it; Developer applies immediately. White closes all
four actuators. All-closed saved command estimates highlight White; they remain
unverified and can be reapplied. White has its own sample preview.

| Choice | M1 | M2 | M3 | M4 |
| --- | --- | --- | --- | --- |
| White / Background1 | Close | Close | Close | Close |
| Background2 | Open | Close | Close | Close |
| Background3 | Unchanged | Open | Close | Close |
| Background4 | Unchanged | Unchanged | Open | Close |
| Background5 | Unchanged | Unchanged | Unchanged | Open |

App composition enforces front-to-back M4→M3→M2→M1 for this paper layout,
overriding the loaded legacy CurtainOrderFrontToBack in memory. JSON hardware
pins/polarity/timing and palette slot assignments are not rewritten. Existing
order overrides no longer control the shipped app's layer mapping. The generic
BackgroundController remains configurable and closes foregrounds before opening
the selected actuator. Relay pairs remain M1:R1/R2, M2:R3/R4, M3:R5/R6, M4:R7/R8
(open/close). White uses no fifth actuator or extra relays.

Settings retains four color dropdowns, showing the background and motor ID;
White is fixed. Display button order is White, M1, M2, M3, M4; diagnostics/settings
retain front-to-back order. Keep existing photobooth.json/colors.json during
update. Startup fullscreen/User and three-second corner hold remain.


## Configurable fixed background (0.6.1)

Settings adds Background1 · Fixed background. Select an unused palette color and
press Save; the first main button's name, swatch, selection label and sample photo
update together. Save stays in Settings and does not move curtains. White remains
the default for old configurations. The fixed background still selects all four
motors Closed; it has no relay pair of its own. Four motorized slots remain.

colors.json now accepts `"FixedBackground": "white"` (or another Colors key),
separate from the four physical Slots. Changing a palette entry's Name/Color/
PreviewImage also changes its fixed-background presentation. Missing white in an
old/minimal palette gets the built-in White choice. New duplicate fixed/motor
assignments are rejected in Settings. Existing files remain unchanged until Save.


2026-11-06 Settings order/layout (0.6.2): user requested all five together,
matching main. Settings now one centered vertical list, fixed ID0 then M1..M4;
removed right-side selector/explanatory panel. DisplaySlots projects fixed draft
plus same motor Slot references sorted physical ID, independent of front-to-back
control order. FixedColor synchronization retains duplicate rejection/save atomicity.
Motor Slots collection remains internal four-slot contract; five display rows save
same fixed key/four motor keys. Debug/Release legacy,47 checks (new unified order/
shared motor refs/fixed edit/duplicate/save test), ARM32 publish and headless
Settings visual check passed. No physical Pi test.


## Developer preview and START (0.6.3)

Developer mode now stages color selections just like User mode: a matching sample
photo displays for two seconds, returns to the shared live camera, and START is
the only background-application action. Selecting fixed or motorized backgrounds
sends no relay commands. The applied-background repeat guard remains. Stop is
retained in Developer mode to interrupt movement. Camera/settings/log controls
remain available according to existing movement guards.

The Developer status list now shows all five backgrounds in main-screen order.
The fixed background has derived Visible/Hidden/Changing/Unknown command status:
all four motors Closed means Visible, any Opened means Hidden, active movement
means Changing, and inconclusive state means Unknown. Restored estimates retain
(saved). These are inferred command states, not physical sensor readings.


2026-11-07 Developer STOP removal (0.6.4): removed visible STOP from shared photobooth view at user request. Both modes keep START, previews and existing diagnostics. Internal cancellation, shutdown cleanup and relay timeout behavior unchanged. Legacy Debug/Release,48 checks and ARM32 publish passed; Developer render inspected. Physical Pi untested.


## Necessary relay commands only (0.6.5)

BackgroundController accepts an optional state reader. App supplies only exact
current-session Opened/Closed command states; saved estimates, Unknown and
in-progress states are treated as Unknown. Before each motor operation, matching
completed state skips the entire relay command (no output writes or fake completion
log). Necessary foreground closes still finish before target opening. An already
Opened target is skipped after foreground handling. Fixed background closes only
motors not already Closed; curtains behind the target remain unchanged.

Examples with paper M4→M1 front order: M4 open, M3 closed, selecting M2 sends R8
then R3. If M2 is already open, it sends only R8. Selecting fixed after that sends
only R4. Startup hardware positions remain Unknown and receive explicit commands;
restored (saved) estimates do not suppress first commands. No physical feedback
was introduced: skipping relies on completed application commands, not sensors.
Standalone controller callers without a state reader retain the previous behavior.


## Runtime relay layer order (0.7.0)

Developer mode > Settings now shows Relay layers, with two nearest-to-farthest
choices: R1/R2→R3/R4→R5/R6→R7/R8, or R7/R8→R5/R6→R3/R4→R1/R2.
User Settings hides this control and cannot save a changed layer order. Both
modes use the saved order. Save applies at idle, remains in Settings and never
writes GPIO. Cancel/failed Save retain the previous mapping; saving during
movement is rejected. Open remains odd relay and Close remains even relay.
Physical color/relay IDs and five main/Settings color rows do not move.

RelayOrderFrontToBack is an optional array in colors.json, saved atomically with
Colors, Slots and FixedBackground. Its entries are physical motor IDs1..4.
[1,2,3,4] means R1/R2 nearest; [4,3,2,1] means R7/R8 nearest. MainViewModel loads
this saved value after palette validation, taking precedence over the app's
paper-layout fallback. Existing files without the field retain current default
behavior until Save. Legacy custom permutations are retained as a third current
choice rather than silently rewritten; new UI choices are forward/reverse.

Applying an order rebuilds only the background command planner, not GPIO output
objects. Pending sample/draft selection is cleared and selected-background
highlight is inferred again using the new order. Motor command states remain
attached to physical IDs; saved estimates stay reapplyable. Per-motor skip logic
and foreground-before-target ordering follow the chosen order on the next START.


## Nearest-to-farthest display (0.7.1)

Main/User/Developer buttons, diagnostics, and Settings color selectors now follow
saved front-to-back relay layer order, then fixed background last. For R1/R2
nearest: M1,M2,M3,M4,fixed. For R7/R8 nearest: M4,M3,M2,M1,fixed. A main heading
marks nearest→farthest; Settings rows identify motor/relay pair and fixed farthest.
Changing the Developer order previews the draft Settings order immediately;
main order changes only after successful Save. Cancel/failure keep applied order.
Color assignments, commands and saved motor statuses remain keyed by physical ID.
The displayed list index is no longer a motor ID: consumers resolve by option.Id.


2026-11-07 User caption removal (0.7.2): nearest→farthest heading in shared view visible only in Developer, per user request. Ordering and relay logic unchanged. Legacy Debug/Release,54 checks and ARM32 publish passed; User render visually checked. Physical Pi untested.


## Complete setup bundle 0.7.3

Latest complete installer: artifacts/bundles/CurtantControl-Setup-v0.7.3.tar.gz
(227,523,364 bytes). Includes legacy Buster ARM32 (.NET8), modern ARM32/ARM64
(.NET10), all18 images per payload, colors defaults, startup/shortcut/diagnostic
scripts and installer documentation including HUONG-DAN.md. Same application
behavior as0.7.2; packaging version advanced for fresh publish outputs.
Build with Publish-SetupBundle.ps1 and an unused version. Installer is online for
OS dependencies including FFmpeg; runtimes/application/assets are bundled.
Existing photobooth.json/colors.json preserved on update; fresh configs sanitized
to simulation with no wiring/polarity/private camera settings. Obsolete Buster
Aliyun repository migration retained. No physical Pi install was run on host.


Bundle0.7.4 uses the explicit user profile in packaging/bundle/photobooth.default.json by default (hardware mode, ActiveLow=false, supplied BCM pins and local Python feed). Fresh colors.json sets R1/R2 nearest. Use -GenericDefaults for sanitized simulation distribution. Updates preserve existing JSONs; profile does not overwrite installed settings. Universal archive verified with all54 app checks per variant and5 installer checks.


2026-11-07 paper display order (0.7.5) explicitly supersedes nearest-to-farthest
visual sorting: user requested buttons per handwritten drawing, fixed ID0 first,
then M1,M2,M3,M4. Main/User/Developer/status and Settings DisplaySlots use this
stable physical-background order regardless of relay-layer selector. Settings
labels background numbers1..5 plus motor/relay IDs. Developer heading Background1→5;
User remains no heading. Runtime near-layer selection, foreground planner, state
optimization, GPIO pairs and persisted colors unchanged. Tests assert stable paper
order before/after forward/reverse Save and same Settings/main identities. Legacy
Debug/Release,54 checks and ARM32 publish passed; main/Settings renders reviewed,
all-photo decode/2s return/START clearing verified. Pi physical test pending.

## 0.8.0-buster update — 2026-11-09

Patch: artifacts/updates/CurtantControl-v0.8.0-buster-update.zip. Three managed
application files plus version, Vietnamese instructions and SHA256 manifest.
Targets existing Buster ARM32 .NET8 installations; unchanged non-app dependency
files compared against0.7.5. Retain installed photobooth.json/colors.json; set only
ActivationMilliseconds to500 in the installed configuration. Includes separate
Razr I/II screens/settings, metallic badge, centered input fields and User-hidden
Modes. Debug/Release legacy builds,62 checks, native-symbol/ELF checks and ZIP
extraction/hash verification passed. Full release archive also generated under
artifacts/releases. No physical Pi run; legacy Buster compatibility remains experimental.

## 0.8.1-buster — 2026-11-09

Razr II completion matches legacy Python position stability rather than target
angle accuracy. One-second sampling; timeout/cancellation remain. Position stable
status avoids claiming target reached. Patch under artifacts/updates,3 managed
files; preserve deployment/palette/per-user motor configuration.63 checks and
legacy Debug/Release/ARM32 native scans passed; ARM64 publish and ZIP hashes
verified. No physical Pi test.

## 0.8.2-buster — 2026-11-09

Razr II advanced UI exposes only simulation, wiring/travel confirmation, angle
limits and timeout. Calibration/scaling/stability/I2C fields hidden; backend and
existing persisted values retained. Patch in artifacts/updates;3 managed files,
preserve all settings. Legacy Debug/Release,63 checks, ARM32 native scans and
archive extraction/hash verification passed. No physical Pi run.

## 0.8.3-buster — 2026-11-09

Razr II coarse ADC feedback now uses five-sample averages and a multi-window
stability check to tolerate the stationary voltage variation reported on the Pi.
Retains timeout/cancellation and post-stability wait; rejects large oscillation.
65 checks including reported ADC trace/travel/reset/time window passed; legacy
Debug/Release/native ARM32 checks and ARM64 publish passed. Three-file update ZIP
in artifacts/updates; preserve configuration. Archive hashes verified. No physical
Pi/motor acceptance test.

## 0.8.4-buster — 2026-11-09

Razr II persists its last completed START selection and restores saved highlighting
without motion; pending/interrupted commands remain unverified and real/simulated
state is separate. Existing state begins after first successful START.69checks,
legacy Debug/Release/ARM32 native scans, ARM64 publish and update ZIP hash checks
passed. Three managed-file patch under artifacts/updates; preserve settings.
No physical Pi test.

## 0.8.5-buster — 2026-11-09

Small three-managed-file patch: Razr II uses State, Razr I retains START. Includes
previous feedback filtering and saved camera button. ZIP in artifacts/updates;
net8 ARM32, unchanged dependencies vs0.8.4 and extracted hashes verified.
Preserve all deployment/palette/per-user configuration. No physical Pi test.

## 0.8.6-buster — 2026-11-09

Razr II persists the selected button independently from motor completion, retaining
it across settings Back/Save and motor errors. Unknown outcome remains retryable
and is not marked complete. STATE uppercase; I remains START. Small three-file
update ZIP in artifacts/updates.70 checks, legacy Debug/Release/native ARM32,
ARM64 publish and ZIP hash verification passed. No physical Pi test.

## 0.8.8-buster — 2026-11-09

Small update:3 managed app files plus razr2-names.json external dropdown choices.
Edit the JSON array and reopen settings; custom saved names remain available.
Preserve existing config/name files when already customized. Includes selector
text cleanup.71 app checks,5installer tests, legacy Debug/Release/ARM32 native
scans, ARM64 publish and archive hash verification passed. No physical Pi run.

## 0.8.10-buster — 2026-11-09

Razr II name dropdowns show palette color chips; Save applies matching button
colors. External razr2-colors.json maps names to shared curtain palette keys.
Names list adds Hip-hop, Grain Art, Vintage and Modern. Small ZIP includes3managed
files plus names/color-map JSON; preserve other configs and merge customized
names lists.72app checks,5installer checks, ARM32 native scans/ARM64 publish
and extracted archive hashes passed. No physical Pi run.

## 0.8.11-buster — 2026-11-09

Name and color choices consolidated into razr2-options.json array of Name/Color
objects. Palette keys or literal hex supported;16defaults. Old two-file catalog
is fallback only when combined file absent. Small ZIP:3managed files plus1JSON;
merge custom catalog edits.73app checks,5installer tests, legacy builds/native
ARM32 checks, ARM64 publish and ZIP hashes passed. No physical Pi run.

## 0.8.12-buster — 2026-11-09

Razr II applied selection now mirrors Razr I repeat guard: highlight stays, STATE
is disabled on already-applied choice and browse-back; changing actual position
allows return. Settings preserves it and failed commands remain retryable.74
checks plus actual selected-border UI sequence passed; legacy Debug/Release/ARM32
native scans and small ZIP hashes verified. Preserve options/configuration.
No physical Pi run.
