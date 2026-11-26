# Agent memory

Last updated: 2026-11-06 (Asia/Bangkok).

## Confirmed context

- The user requested initialization of a C# Avalonia desktop application that can
  run on Raspberry Pi 4, then requested agent instructions, documentation, and memory.
- Repository name and application namespace: `CurtantControl`. Preserve this
  spelling unless the user requests a rename.
- Initial development path: `C:\Users\Pc\Documents\ChatGPT\CurtantControl`.
  Commands and documentation should remain usable from other checkout locations.
- Stack: .NET 10, Avalonia 12.1.5, CommunityToolkit.Mvvm 8.4.2.
- SDK baseline: 10.0.403. A .NET 11 preview was also installed at initialization;
  `global.json` selects the stable .NET 10 toolchain.
- The photobooth screen has four selectable colors, selection highlight, Close all,
  Stop, and live USB webcam preview through an external FFmpeg process.
- Camera dropdown above preview lists discovered Windows DirectShow or Linux V4L2
  capture devices; Refresh rescans. Startup prefers configured CameraDevice, otherwise Automatic tries service then direct hardware. UI selections are session-only.
  Linux discovery requires v4l-utils, skips metadata-only nodes. Camera switching
  cancels/awaits the old stream, serializes changes, and skips stale selections.
  No device or discovery errors produce an offline message. Hardware remains untested.
- User confirmed the simulated curtain statuses looked correct. Simulated startup
  remains all Closed (off), with real hardware curtain positions Unknown.
- Gear icon navigates to a separate setup screen. Per user's correction, each
  fixed front-to-back slot has a color dropdown; there are no move-position buttons.
  Selecting a used color swaps its other slot. Save persists the unique ID order
  and applies it without relay movement. Save stays on setup and displays Saved;
  Back/Cancel discards only unsaved draft changes. All buttons use bold, centered
  text; the save action label is Save.
- `CurtainOrderStore` saves a per-user override at
  `<LocalApplicationData>/CurtantControl/curtain-order.json`, loaded after
  `photobooth.json`. Deployment hardware settings are not rewritten. Saving errors
  keep setup open and retain the previous active order.
- A temporary UI log shows the latest 100 timestamped entries, newest first, with
  a Clear command and `[SIM]` markers. Per user request, simulated curtains start
  Closed (off); real-hardware curtain states start Unknown. States
  transition through Opening/Closing, and finish Opened/Closed only on success.
  Cancellation/failure marks the affected curtain Unknown. No position feedback exists; last-command estimates now persist separately and restore with a saved annotation.
- Startup initializes all outputs OFF and logs readback for each of eight relay
  control signals. Check relays refreshes the snapshot. GPIO uses GpioController.Read
  with configured polarity; simulation reads its in-memory state. Unsupported or
  failed readings are Unknown. These are not pre-startup states or contact feedback,
  and must not be used to infer physical curtain position.
- `RaspberryPi64` publishes a self-contained, untrimmed `linux-arm64` folder.
- Initial local device-testing release: version 0.1.0; archive and checksum under
  `artifacts/releases/`. `scripts/Publish-RaspberryPi.ps1` builds/checks and packages
  future versions with docs and a `sh start.sh` launcher. Package defaults to
  simulation. Source config currently contains BCM pins [17,27,22,23,24,25,5,6]
  and ActiveLow=true, but physical wiring/compatibility remain unverified.
- v0.1.1 added `curtant-control.svg` and
  `install-shortcut.py`. Run once on the Pi as desktop user with Python 3; creates
  desktop/menu launchers without a terminal. Keep the extracted folder in place;
  rerun installer after moving/upgrading. Desktop launch trust may need user action.
- Latest package for the user's actual device is v0.1.2 linux-arm (32-bit), with
  RaspberryPi32 profile. Release script accepts -Architecture arm or arm64. Launcher
  uses getconf LONG_BIT and skips chmod on an already-executable binary.

## Decisions and assumptions

- Confirmed device: Raspberry Pi 4, 32-bit userspace (`getconf LONG_BIT` = 32) with
  a 64-bit kernel (`uname -m` = aarch64). ARM64 packages cannot run: the AArch64 loader
  is absent. Use `linux-arm` / `RaspberryPi32` for this Pi; retain ARM64 support for
  actual 64-bit OS installations. User reports wallpaper/icons, but diagnostic
  terminal reported Session=tty and empty DISPLAY; graphical launch is not verified.
- Physical launch of v0.1.2 ARM32 then failed: libc.so.6 lacks GLIBC_2.34.
  User screenshot subsequently confirmed Raspbian GNU/Linux 10 (buster), 32-bit;
  exact glibc command output was not shown. .NET 10 portable ARM32 requires
  glibc >=2.35 / Y2038-compatible userspace; matching architecture alone was insufficient.
  Do not claim this release works on the existing OS or suggest manually replacing libc.
- Recommended device migration: test current 64-bit Raspberry Pi OS Desktop on a
  separate SD card, preserving the Buster card and existing photobooth setup. No OS
  installation or destructive changes have been authorized or performed.
- User prefers keeping Buster and updating the code. Added optional LegacyBuster
  target net8.0, keeping default net10.0 and stable SDK 10. Legacy compiler uses
  C# 14 for partial observable properties. Candidate 0.1.3-buster uses .NET 8.0.33,
  with unchanged Avalonia/GPIO packages. Buster itself is outside official .NET OS
  support and .NET 8 support ends 2026-11-10; disclose these limits.
- Latest existing-device candidate: artifacts/releases/CurtantControl-v0.1.9-buster-linux-arm.tar.gz.
  Launcher can detect an accessible local X socket with xdpyinfo when DISPLAY is
  empty. It does not change authentication or start a desktop session.
- Actual camera is ribbon/CSI, correcting earlier USB assumption. User confirms
  /usr/bin/raspivid and firmware supported=1 detected=1, libcamera interfaces=0.
  Direct /dev/video0 preview failed with Invalid argument; internal Broadcom
  processing nodes also appeared incorrectly in the dropdown.
- Added legacy CSI selection raspivid://0: raspivid emits H264, piped directly
  into FFmpeg for BGRA frames. Native processes are managed without shell pipes.
  Modern libcamera/rpicam stacks are still unsupported. Codec/ISP nodes and
  non-single-planar capture are excluded; camera/setup errors reach activity log.
- Direct legacy CSI failed with MMAL ENOSPC on-device. User's React app camera
  works; Python PID 578 held /dev/vchiq and runs /home/pi/r1/model_1/app_model_1.py.
  Source screenshot confirms Flask GET /video_feed multipart JPEG, server port5000.
- Added CameraStreamUrl mode to reuse http://127.0.0.1:5000/video_feed via FFmpeg
  without V4L2 or raspivid access. Manual HTTP capture bypasses direct hardware; Automatic also discovers direct fallback choices. Keep Python running; initially close
  browser preview because server multi-client capture safety is unverified.
- User prefers file updates over copying the entire release. Small 0.1.5-buster
  ZIP under artifacts/updates contains CurtantControl.dll, .deps.json,
  .runtimeconfig.json, and UPDATE.txt. Existing config must be retained with the
  CameraStreamUrl property added. Dependency set matches v0.1.4-buster.
- Device 0.1.5 follow-up: FFmpeg reached HTTP feed but failed with "Expected boundary
  '--' not found". Candidate 0.1.6 uses .NET HttpClient to extract JPEG frames without
  relying on multipart boundaries, then pipes raw JPEGs to FFmpeg. Preserves JPEG
  segments/entropy markers; rejects invalid/oversized frames and HTML/JSON replies.
  Small update ZIP is artifacts/updates/CurtantControl-v0.1.6-buster-update.zip.
  Retain existing CameraStreamUrl and relay config; no Python service changes made.
- User clarified full width means the camera image filling its preview box, keeping the previous UI. v0.1.8-buster restores the balanced two-column scaled canvas, original large curtain controls and tagline, and normal window startup. UniformToFill and proportional decoder cropping remain; image edges may crop.
- Use MVVM for presentation and isolate future hardware access behind services.
- Confirmed aim: control four photobooth background curtains through eight relays,
  two per curtain (open/close). Logical pairs are 1/2, 3/4, 5/6, 7/8.
- Implemented `CurtainRelayController` with timed commands, per-curtain mutual
  exclusion, configurable off interval and maximum activation, cancellation,
  stop/all-off, fault latching, and disposal. GPIO and simulated backends are
  behind `IRelayOutput`; System.Device.Gpio is pinned to 4.2.0.
- App startup loads `photobooth.json`; simulated relays are the default. GPIO is
  enabled only by explicit configuration. Timed commands do not establish physical
  curtain position.
- User clarified USB webcam on the Pi and layered background behavior: close the
  curtains in front of the selected one, then open that curtain. Layers behind it
  stay unchanged. User allowed any initial order; configured nearest-to-farthest
  order is Pink (1), Sky Blue (2), Green (3), Purple (4).
- Gray in the reference is implemented as Close all because hardware has four
  curtains. No photo-capture action is requested or implemented.
- User provided a selectable high/low relay-board listing (see RELAY_CONTROL.md).
  The listing specifications were inaccessible. BCM mapping, polarity, 3.3 V
  compatibility, wiring, timing, and pulse/maintained control are unconfirmed.

## Verification history

2026-11-05:

- Debug build passed with zero warnings and errors.
- Release build passed with zero warnings and errors.
- RaspberryPi64 self-contained publish succeeded.
- Published executable had ELF magic and AArch64 machine type (183).
- Physical Raspberry Pi execution and visual/runtime interaction were not tested.
- Sandbox restrictions required approved build/restore execution for NuGet and
  user-profile writes. Debug built successfully after restoring Debug-specific assets.
- After relay-wrapper implementation: Debug and Release solution builds passed
  with zero warnings/errors, ARM64 publish passed with the GPIO dependency, and
  eight recording-output checks passed (including startup and activation faults).
  Physical relay switching and Pi runtime behavior remain untested.
- After photobooth UI integration: Debug/Release builds and ARM64 publish passed
  with zero warnings/errors. Eleven relay/background/UI-command checks passed.
  The actual AXAML was rendered using Avalonia.Headless/Skia and visually inspected;
  preview artifact is `artifacts/photobooth-ui.png` (generated, ignored by Git).
- Native app launched, but computer-use window captures timed out; visual QA used
  the headless renderer instead. No live webcam frames or physical GPIO commands
  were tested. FFmpeg is an external prerequisite and was not installed here.
- After temporary log changes: Debug and Release builds passed in
  `artifacts/verification` to avoid interrupting the user's running app. Twelve
  relay/background/UI checks passed, including state/log updates and cancellation.
  The updated headless UI render was visually checked. No hardware run was performed.
- After startup readback: 14 relay/background/UI checks passed and Debug/Release
  builds passed with zero warnings/errors. Hardware GPIO readback remains untested.
- After dropdown setup: 17 relay/background/UI checks passed, including save/cancel,
  persistence reload, duplicate prevention, and saved-order relay sequencing.
  Debug/Release builds passed in `artifacts/settings-verification`; the setup
  screen was rendered to `artifacts/curtain-settings-ui.png` and visually checked.
  An older verification folder contained a null-filled generated runtimeconfig;
  a fresh isolated output folder resolved validation. No hardware test was performed.
- Button-style/Save refinement: Debug and Release builds and all 17 checks passed.
  Rendered setup confirms bold centered button labels and green Saved feedback
  while remaining on setup. The Saved notice clears when dropdown choices change.
- Camera dropdown: all 20 checks and Debug/Release builds passed in
  `artifacts/camera-verification`. Checks use fake capture sessions to verify
  switching, rapid selection, refresh/disconnect, discovery errors, and shutdown.
  Updated UI was rendered and visually checked with sample device names; no live
  camera or GPIO hardware was used.
- Release 0.1.0: Debug/Release builds and all 20 Release checks passed; ARM64 publish
  included .NET 10.0.14 and assembly version 0.1.0.0. Archive is 41.13 MiB, all 229
  files matched staging after extraction, and SHA256 passed. Physical Pi run is pending.
- Release 0.1.1 builds, all 20 app checks, publish, checksum/extraction, shortcut
  asset presence, and SVG XML checks passed. Installer execution was not verified
  here because Windows has no usable Python interpreter. Pi shortcut test pending.
- Release 0.1.2 linux-arm: Debug/Release builds and all 20 checks passed. Executable,
  coreclr, Skia, and HarfBuzz machine types verified ARM32. Extracted ELF32 executable
  requests /lib/ld-linux-armhf.so.3; archive SHA256 and extraction passed. Package is
  40.09 MiB. Corrected architecture is not yet a successful physical Pi launch.
- Candidate 0.1.3-buster: legacy Debug/Release builds and all 20 behavior checks
  passed via net10.0 host runner against net8.0 app assembly; no ARM32 execution
  occurred here. Sixteen native files have GLIBC tags <=2.27 and GLIBCXX <=3.4.25;
  archive 37.73 MiB, extraction/checksum passed. Modern default Debug also passed.
  Physical Buster launch and full external dependency/display/camera validation pending.
- Device follow-up: user confirms the window appears with the Buster candidate.
  Startup logs repeat Avalonia.DBus WakeupFd.Set / LibDBusWireWorker native callback
  Win32Exception "Success". These warnings did not prevent this observed startup;
  root cause and desktop-integration effects remain unverified. Do not claim GPIO,
  camera capture, or all application behavior passed on hardware from this alone.
- 0.1.4-buster: legacy Debug/Release builds, 21 behavior/routing/filter checks,
  native symbol/architecture scans, and modern Debug build passed. Archive has
  227 verified files (37.74 MiB); checksum/extraction passed. No actual raspivid
  process or CSI frames were tested on Windows. User must select the explicit
  Raspberry Pi ribbon camera (legacy CSI) entry and verify capture on-device.
- 0.1.5-buster: legacy Debug/Release builds and all 22 checks passed. Native scans
  passed; 90.3 KiB update ZIP extracted and matched all four staged files. Full
  package has the local Python stream configured. HTTP decoder/frames not tested
  against the actual server here; await the user's device result.
- 0.1.6-buster: legacy Debug/Release builds, 24 checks (including malformed HTTP
  multipart/JPEG parsing), native scans, and small ZIP extraction/hash checks passed.
  Update ZIP 93.3 KiB; dependency set unchanged. On-device frame playback is pending.
- 0.1.7-buster: legacy Debug/Release builds, 24 checks, native scans, and small ZIP
  verification passed. Update 93.6 KiB; dependencies unchanged. Layout renders
  visually checked at 1024x600, 1280x760, 1920x1080; synthetic gradient verified
  camera fill, not actual playback. User should replace three managed files and
  retain photobooth.json. Physical Pi layout/stream result is pending.

## Pending work

- Confirm the graphical desktop session/display connection before launch; user's
  earlier terminal was tty with no DISPLAY despite a reported desktop on the monitor.
  Determine OS version, display size, and input device as deployment proceeds.
- Confirm relay/actuator electrical specifications, GPIO mapping, polarity, layer
  order, and timing before changing `SimulateRelays` to false in `photobooth.json`.
- FFmpeg installation initially failed due to Aliyun Buster mirror 404; user was
  instructed to switch the matching Buster source to legacy.raspbian.org, preserving
  signatures. Screenshot later shows FFmpeg running and reporting Invalid argument.
- Validate CSI preview and performance with the new legacy pipeline, then test
  physical relay behavior. Application window startup has been confirmed on Pi.

## Maintenance

Keep this file focused on durable project facts and current handoff context.
Do not treat old build results as verification of later code changes.


2026-11-05 correction: v0.1.8-buster restores previous UI with camera fill only. Legacy Debug/Release builds, all 24 checks, ARM32 publish/native scans and update ZIP extraction/hash verification passed. Rendered AXAML with synthetic frame visually checked; actual Pi stream remains unverified.

2026-11-05 automatic-camera work: MainViewModel now defaults to Automatic (unless
CameraDevice preference selects a manual source). Try configured HTTP feed, or
known local :5000/video_feed on Linux, then V4L2/USB, then legacy CSI. On failed
capture/disconnect/stall, fully clean up before fallback; after exhaustion rescan
every five seconds until canceled. Manual choices never silently fall back.
Advisory /proc fd check reports occupied /dev/video nodes; /dev/vchiq cannot prove
CSI ownership and is excluded. No external processes are terminated. Modern
rpicam/libcamera and arbitrary service discovery remain unsupported. Device tests
are still required; fake-session checks do not prove physical compatibility.


Verification: v0.1.9-buster legacy Debug/Release builds, 27 checks, ARM32 publish/native scans, dependency comparison and update ZIP hash/extraction passed. Existing UI retained; physical camera fallback and ownership checks need Pi validation.


2026-11-05 relay-check feedback: user reports no output on Check relays. Found
silent return when relay initialization had failed. Store original setup error,
report it on every click (even after Clear), and show/log completion or read error.
v0.1.10-buster legacy Debug/Release, 28 checks, ARM32 publish/native scans and
update ZIP extraction/hash checks passed. This fixes feedback, not the still
unknown Pi initialization cause. Ask for the newly visible error. User confirmed
all eight relays serve curtains; Python assignments convert to BCM
[22,10,9,11,5,6,13,19] in listed order, but IN1..8 order/direction remains unverified.
No source hardware settings were changed. Python also writes these GPIOs.

2026-11-05 installation bundle: user authorized building a package for testing on
a new device. Added scripts/Publish-SetupBundle.ps1 and packaging/bundle installer.
Targets Pi 4 Desktop only, Buster10 armhf (net8) and Debian/Raspberry Pi OS12/13
armhf/arm64 (net10). Python stdlib installer chooses via dpkg userspace architecture,
verifies payload hashes, installs signed apt dependencies, checks ldd, adds existing
gpio/video groups, installs per-user fixed ~/.local/share/CurtantControl/app and
creates shortcuts. Existing config preserved byte-for-byte, backup folder retained.
New bundle installs simulate with empty pins/null polarity; no guessed hardware profile.
OS dependencies require internet; not an offline bundle. No repository/firmware or
external camera service changes. Modern CSI rpicam/libcamera still unsupported.
Added read-only diagnose.sh (omits config/command lines). Host installer checks do
not establish apt or native execution on Pi. User device test remains required.

Device follow-up confirmed camera preview visible and relay control signals now
read OFF after libgpiod install. Pink R1 briefly changes then returns off. Actuator
label HY01: 24VDC,600mm,200N,45mm/s,48W,25% duty. Controller wiring/limit behavior
and physical movement completion remain unconfirmed; no activation duration change.

Bundle verification: v0.2.0 setup archive 119.0 MiB; SHA256 73c077905c6df8033294f30d9a7eb495870bdabdd05ce820951803920f708761. All three Debug/Release builds and 28 checks per variant passed, plus four Python installer tests. Archive extraction, per-payload hashes, sanitized configs, ELF architectures and LF/Python syntax checks passed. Real Pi installer/apt/native execution is pending. Bundle location artifacts/bundles/CurtantControl-Setup-v0.2.0.tar.gz.

2026-11-05 repository installer fix: user reported missing obsolete Aliyun migration. Source installer and user's artifacts/bundles/CurtantControl-Setup-pi/setup.py updated. Before apt update on Buster only, sudo helper backs up matching sources.list or sources.list.d/*.list and replaces active Aliyun Raspbian Buster URIs with HTTPS legacy.raspbian.org/raspbian/. Comments, source options/components and unrelated repositories retained; no signature bypass. Preflight/skip-packages do not mutate apt. Five host installer tests passed including migration scoping/idempotence/backups. Fixed archive CurtantControl-Setup-pi-repository-fix.tar.gz; original 0.2.0 bundle remains unchanged. Actual Pi apt update pending.

2026-11-05 Python camera activation: pasted service source confirms /video_feed
returns204 when camera_enabled=false; /get_camera_mode hardcodes1 and is not a
permission check. User explicitly authorized activation. On204 from the known
local HTTP :5000/video_feed only, app POSTs /camera_control {mode:1}, requires
type=camera before one feed retry. Handler checks availability, not guaranteed OS
permission. No GPIO/other mutation endpoints used. EnablePythonCameraActivation
(defaulttrue) allows opt-out. Redirects disabled; response bounded and cancellation
supported. Existing working feeds not activated. Host checks cover acceptance,
refusal, unrelated URLs, opt-out and repeat204. No Python source changes performed.

Activation release verification: 0.2.1-buster and modern0.2.1 Debug/Release,30 behavior/HTTP checks, ARM32 native compatibility and ARM64 publish checks passed. SmallZIP extraction/hash and unchanged runtime dependency comparison passed. User Pi-only setup folder refreshed and repacked as CurtantControl-Setup-pi-camera-activation.tar.gz, including repository repair; payload/archive hashes passed. Original universal0.2.0 bundle unchanged. Device handshake remains pending.

2026-11-05 dark/light mode: header ToggleSwitch bound to IsDarkMode, applies Avalonia theme and shared dynamic brushes to main/setup. ThemePreferenceStore persists separately in user LocalApplicationData/CurtantControl/appearance.json, defaults dark on missing/invalid file. No photobooth.json edits. Added persistence/reload/fallback check. Legacy Debug/Release,31 checks and ARM32 publish passed. Dark main and light main/setup AXAML rendered and visually checked; corrected offline reconnect contrast. SmallZIP artifacts/updates/CurtantControl-v0.2.2-buster-update.zip extraction/hash verified. Physical Pi appearance untested.

2026-11-06 selected-color guard: user requested no retrigger when clicking selected color. SelectCurtainAsync now returns when curtain.IsSelected. Extended existing UI check verifies no output writes/log entries on repeat and selection works again after Close all. Applies to requested selection, including after canceled/failed commands while highlight remains. No physical-state inference.

Selected-color verification: v0.2.3-buster Debug/Release, all31 checks, ARM32 publish/native scans and smallZIP extraction/hash passed. Device validation pending. Patch artifacts/updates/CurtantControl-v0.2.3-buster-update.zip.

2026-11-06 reconnect hover fix: dedicated theme brushes and pointerover/pressed template styles prevent dark-on-black label in light mode. Light pale-blue/dark text; dark blue-gray/light text. v0.2.4-buster Debug/Release,31 checks, ARM32 publish/native scans and smallZIP hash/extraction passed. Headless actual pointer hover rendered and visually checked. Update artifacts/updates/CurtantControl-v0.2.4-buster-update.zip. No hardware config edits.

2026-11-06 selection dot: binds to computed SelectionColor matching selected CurtainOption.Color, notifying after select/Close all. Gray with no selection. v0.2.5-buster Debug/Release,31 checks, ARM32 publish/native scans and patchZIP extraction/hash passed. Light render visually checked with Sky Blue. No hardware changes.

2026-11-06 configurable palette: user clarified more available color choices,
not more curtains. Added colors.json beside executable: extensible Colors
(Key/Name/#RRGGBB), four Slots keys in fixed curtain ID1..4 order. Shipped eight
choices with original four assignments. Settings right panel has four palette
dropdowns and independent Save colors; bottom Save retains layer-order behavior.
Color save persists first, updates observable labels/swatches/selection dot, never
moves relays. Unsaved edits discarded on reopen; duplicate assignments allowed.
Missing file falls back to original four; invalid content logs defaults warning.
Bundle installer source now preserves existing colors.json as well as hardware
config. Existing archives are not retroactively updated. No source wiring edits.

Palette verification: v0.2.6-buster and modern0.2.6 Debug/Release builds,32 app checks, ARM32 native compatibility scans and ARM64 publish passed. Five Python installer checks passed with palette preservation. Setup light AXAML rendered/visually inspected. SmallZIP with three managed files + colors.json + instructions hash/extraction and unchanged dependencies verified. Physical Pi test pending.

2026-11-06 user correction supersedes previous palette layout: removed right-side editor and separate Save colors. Existing four front-to-back dropdowns now choose from full palette, retain fixed physical IDs, reject duplicate keys without swapping. Bottom Save persists appearance only, stays on setup with Saved. Existing configured/saved layer order is respected and not edited. Deleted CurtainPaletteViewModel; consolidated into CurtainSettingsViewModel/CurtainLayerSlot. Palette validation requires four unique keys. v0.2.7-buster Debug/Release,32 checks, ARM32 publish/native scans and patch ZIP hashes passed; corrected setup render visually checked. Keep custom colors.json during update; example supplied separately.

2026-11-06 Close all guard: user requested no action when all curtains already closed. MainViewModel returns if every CommandStatus is Closed; no output writes/log entries. Unknown real startup still closes all, simulated Closed startup skips. Extended checks cover repeated Close all and startup distinction. v0.2.8-buster Debug/Release,33 checks, ARM32 publish/native scans and patchZIP extraction/hashes passed. Keep photobooth.json/colors.json; actual positions remain unmeasured.

2026-11-06 level-order fix: main screen previously enumerated physical ID order while setup enumerated configured front-to-back order. Added DisplayCurtains ordered shared references and DisplayLevel01..04 for main buttons/status. Keep Curtains physical ID ordered for relay/status/color indexing. Extended custom-order test compares main/setup IDs and levels, saves colors to correct displayed curtain and checks relay sequence unchanged. v0.2.9-buster Debug/Release,33 checks, ARM32 publish/native scans and ZIP hashes passed. Keep existing JSONs. Pi acceptance pending.


2026-11-06 last-command persistence: explicitly requested by user. App injects
CurtainCommandStateStore; hardware and simulation snapshots separate in user
LocalApplicationData/CurtantControl. Restore by physical ID as Opened (saved) /
Closed (saved), never sensor-confirmed. No selection restored or startup movement;
saved labels don't suppress first explicit commands. Save Unknown before new pulse,
final completion afterward; canceled/faulted commands stay Unknown. Pending save
failure blocks that curtain pulse. Invalid/missing snapshots log/fall back; no old
history recovered. Per-user files survive installed app updates. Legacy/modern
Debug/Release,36 checks and ARM32/native + ARM64 publishes passed; patch ZIP hashes
verified. Actual Pi persistence remains untested.

2026-11-06 startup background inference supersedes prior no-highlight restoration: scan DisplayCurtains front-to-back, skip Closed until first Opened (select) or Unknown (cannot infer). Behind states irrelevant. All Closed shows saved all-closed label, no highlight. Saved selected label/dot update; no GPIO pulses. First explicit re-selection allowed once despite saved highlight; subsequent same-color clicks skipped. Legacy/modern Debug/Release,37 checks, ARM32 native scans/ARM64 publish and update ZIP hashes passed. Nondefault order, multiple Opened behind, Unknown foreground and all Closed covered. Physical inference remains unverified.

2026-11-06 startup modes: user requested chooser with Developer retaining current
screen and User matching reference without logs. User explicitly chose START to
apply staged background. Implemented MainViewModel.Modes partial, StartupModeView,
UserPhotoboothView with glossy buttons/fixed dark theme, preview, START, separate
Stop and Close all. User tap stages only; START sends existing relay sequence;
Developer remains immediate. No photo capture added. User view hides logs/settings/
camera selector/status readback; entering clears old in-memory logs. Modes buttons
return to chooser; no password/security boundary. Pending drafts canceled without
movement on mode change, blocked while commands run; failed/canceled START retryable.
Camera delayed until first mode choice, then shared across switches. Mode not saved.
White/very light palette labels use dark text. Existing persistence/mapping retained.

Modes verification: v0.4.0-buster and modern0.4.0 Debug/Release,39 checks, ARM32/native scans and ARM64 publish passed. Startup/User/Developer headless renders inspected at1280x760; User compact1024x600 checked. Synthetic gradient is not real camera playback. Reconnect hover contrast explicitly styled in User mode. SmallZIP extraction/hashes and unchanged runtime dependencies verified; final Buster archive refreshed. Physical Pi screens/START flow remain untested.


2026-11-06 palette/photo previews: 17 handwritten choices plus retained Green =18.
Built-in imagegen produced 18 separate portrait edits saved under source
Backgrounds; prompt specification in BACKGROUND_PREVIEWS.md. Grain Art spelling
and themed artwork are provisional interpretations. Four fixed physical slots,
unique choices and relay mapping retained. Optional PreviewImage per palette key;
shipped colors.defaults.json merges missing choices/images while preserving user
Slots/name/swatch/custom image and does not rewrite colors.json on load.
User tap stages selection and overlays example for2s while capture continues;
START/Stop/mode change/dispose cancel sample. Developer remains immediate.
Path confinement, decode-to960px and missing-photo fallback included.
Verification: Buster ARM32 and modern ARM64 Debug/Release/publish passed;42 behavior
checks passed. Headless user render reviewed; all18 assets decoded, timed return
and START clearing checked with synthetic live frame. Physical Pi untested.


2026-11-06 shared Developer design supersedes separate old main layout: one
UserPhotoboothView now serves Developer/User. Glossy buttons/header/preview shared;
Developer retains logs/status, Check relays/Clear, camera selector/Refresh,
settings, dark/light switch and immediate color commands. User START/sample flow
unchanged and theme scoped dark. MainWindow only composes shared view/chooser/settings.
IsPhotoboothScreenVisible notified on mode/settings; PhotoboothTheme on mode/theme.
Developer 850px design height vs User680; compact1024x600 and dark/light headless
renders reviewed, reconnect hover checked;42 behavior checks and Debug/Release
builds, ARM32 legacy/ARM64 publishes passed. No physical Pi run. Update ZIP keeps
existing JSON config and includes previews. Setup bundle not regenerated.


2026-11-06 User controls update supersedes fixed-dark User mode: both modes now
share saved dark/light preference and expose settings. User STOP hidden,
Developer STOP retained. Removed BACKDROP/PHOTO BOOTH branding in shared header.
Glossy presenter stretches both axes and centers content; color TextBlocks
explicitly centered. Debug/Release legacy builds and ARM32 publish passed;42
behavior checks passed. User dark/light and Developer renders visually checked;
all18 assets/2sec return/START overlay clearing checked. No physical Pi run.
v0.5.2-buster update ZIP includes previews and defaults, preserves user JSON files.


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


2026-11-06 paper layout (0.6.0) explicitly approved: five backgrounds, fixed White
ID0 plus physical M1..M4 IDs1..4. BackgroundOptions=[White,Curtains physical order];
removed Close all UI, retained internal CloseAll command/tests. White User selection
stages then START MoveAsync(null); Developer immediate. Applied detection for White
requires all exact Closed; saved all-closed restores White highlight with saved
marker/reapply allowed. Simulation starts White highlighted. Background1 closes all;
BG2 M1open/M2..4close; BG3 M2open/M3..4close; BG4 M3open/M4close; BG5 M4open; behind
unchanged. App overrides loaded frontToBack in memory to[4,3,2,1], including config
fallback. Generic controller remains configurable. JSON pins/polarity/timing/colors
unchanged; old saved order no longer determines shipped app mapping. Settings
labels identify BG and M IDs, still four dropdowns; fixed White no actuator.
Debug/Release legacy+modern,45 checks (all five paper rows/no pulse on staging),
ARM32 native/ARM64 publish passed. Main/settings renders reviewed; short tap/3s
hold verified. No physical Pi test. Update ZIP hashes/extraction verified. Old
generated release-build caches removed after disk-full error; release packages kept.


2026-11-06 configurable fixed color (0.6.1) supersedes fixed White presentation:
CurtainPalette.FixedBackground key defaults white; validates key, GetFixedBackground
resolves preset or built-in white for older minimal palettes. Settings FixedColor/
FixedColors dropdown on right, with duplicate rejection against four motor choices
in both directions. Save passes fixed key plus four motor keys atomically; applies
name/swatch/preview and selected label only after successful save. Fixed physical
background ID0 still closes four motors; no hardware changes. Simulation/saved
all-closed labels now use configured fixed name. Existing colors.json gains property
only on Save. Tests cover unsaved draft, successful/failed save, duplicates,
JSON round-trip/reload, preview path and all-four close channels. Legacy Debug/
Release,46 checks and ARM32 publish passed; settings render reviewed. Pi untested.


2026-11-06 Settings order/layout (0.6.2): user requested all five together,
matching main. Settings now one centered vertical list, fixed ID0 then M1..M4;
removed right-side selector/explanatory panel. DisplaySlots projects fixed draft
plus same motor Slot references sorted physical ID, independent of front-to-back
control order. FixedColor synchronization retains duplicate rejection/save atomicity.
Motor Slots collection remains internal four-slot contract; five display rows save
same fixed key/four motor keys. Debug/Release legacy,47 checks (new unified order/
shared motor refs/fixed edit/duplicate/save test), ARM32 publish and headless
Settings visual check passed. No physical Pi test.


2026-11-07 Developer START workflow (0.6.3) supersedes immediate Developer color
commands: both User/Developer use pending selection,2s photo overlay and START.
CanStart and preview visibility include both modes. Developer START replaces
old tagline; Stop/logs/camera/settings retained. Five-row status list uses
BackgroundOptions in main order. New FixedBackgroundStatus partial observes motor
CommandStatus: allClosed Visible, anyOpened Hidden, Opening/Closing Changing,
otherwise Unknown, retaining saved annotation. Fixed name/color remain configurable;
status derived, not stored as fifth motor and never claims physical feedback.
Legacy Debug/Release,48 checks incl Developer no-pulse staging/START, fixed status
transitions/saved/unknown, ARM32 publish passed. Headless Developer dark/light
renders reviewed; all18 images decode,2s return and START clearing verified in
Developer with synthetic live frame. Pi runtime/touch not tested this turn.


2026-11-07 Developer STOP removal (0.6.4): removed visible STOP from shared photobooth view at user request. Both modes keep START, previews and existing diagnostics. Internal cancellation, shutdown cleanup and relay timeout behavior unchanged. Legacy Debug/Release,48 checks and ARM32 publish passed; Developer render inspected. Physical Pi untested.


2026-11-07 per-motor optimization (0.6.5), explicitly requested: BackgroundController
optional Func<int,CurtainCommandState> state reader checks cancellation then skips
RunAsync if current state matches desired. App maps only exact current Opened/Closed;
saved suffix/Unknown/in-progress map Unknown. No writes/state/report fabricated for
skips. Foreground closure barrier, cancellation/fault behavior and relay pairs
unchanged. Fixed closes only non-Closed motors, target alreadyOpened skipped after
foreground handling; behind unchanged. Generic no-reader calls retain full pulses.
Updated integration expectations for skipped logs/pulses. Legacy Debug/Release,
51 checks including80 known-state/background combinations with repeat/no-write and
ordering assertions, paper transition chain and cancellation/unknown test passed;
ARM32 publish/native scan passed. Saved estimate reassertion tests retained. No
physical Pi verification. Update ZIP hashes validated; preserve existing JSONs.


2026-11-07 runtime layer order (0.7.0): approved forward/reverse nearest relay-pair
choice, Developer Settings only per follow-up. CurtainSettingsVM RelayOrders and
SelectedRelayOrder, IsRelayOrderVisible on Begin; Save guard also rejects changed
order outside Developer. User retains five colors and can save unchanged order.
CurtainPalette optional RelayOrderFrontToBack validates permutation, persisted
atomically with colors/fixed key. Constructor applies saved palette order over
App paper fallback, superseding earlier always-hardcoded shipped-layer mapping.
Legacy custom permutations preserved as current third choice. No order-store writes.
On successful idle Save, rebuild only BackgroundController with same relays/state
reader; update settings/DisplayCurtains levels, cancel draft/preview, re-infer current
highlight (including saved-foreground reapply flag). Physical IDs/colors/statuses
and odd-open/even-close pairs unchanged; save no GPIO writes. On failed save/cancel
or movement no mapping change. 54 checks passed incl runtime next-START channels
both directions, optimization, atomic failure/cancel, reload/disk persistence,
selection reconciliation and Developer/User controls. Buster/modern Debug/Release
and ARM32/ARM64 publishes verified; dark/light Developer and User Settings renders
reviewed. Render helper now uses in-memory palette to avoid contaminating test
output colors.json with a saved order. Physical Pi runtime test pending.


2026-11-07 nearest→farthest sorting (0.7.1), explicitly requested to avoid confusion:
BackgroundOptions now mutable IReadOnlyList built from DisplayCurtains + fixedLast,
not physical ID order. Save order rebuilds list and notifies binding. Internal
Curtains remains ID1..4 order; selection/relay/persistence IDs unchanged. Settings
DisplaySlots follows SelectedRelayOrder, fixedLast; draft selector change rebuilds
rows, labels motor/relay pairs and nearest/farthest; Position now observable.
Main heading identifies nearest→farthest. Unit consumers changed from display
index to Single(Id), existing runtime test asserts both orders, draft vs applied
and main/setup agreement. Legacy Debug/Release,54 checks and ARM32 publish passed.
User forward/reverse+Settings renders reviewed; photo timeout/START check passed.
Preserve saved JSON assignments; fixed key remains0 but always displayed last.
No physical Pi test. Updated patch ZIP verified.


2026-11-07 User caption removal (0.7.2): nearest→farthest heading in shared view visible only in Developer, per user request. Ordering and relay logic unchanged. Legacy Debug/Release,54 checks and ARM32 publish passed; User render visually checked. Physical Pi untested.


2026-11-07 complete setup rebuild0.7.3 at user request: universal archive
artifacts/bundles/CurtantControl-Setup-v0.7.3.tar.gz,227523364 bytes; SHA256
18692c2f14c693cdf8b09da96aa73c3dd31bd65f055f5db50f8fc92f8e45b8cb.
Three new full releases via Publish-SetupBundle (Buster ARM32 net8, modern ARM32/
ARM64 net10); Debug/Release and54 checks passed per variant, native/ELF scans
passed. Five Python installer tests passed. Independently verified all payload
manifests (248/251/252 files),18 photos each, sanitized new-device config, runtime
TFMs, root scripts LF and entire758-file tar contents against assembled bundle.
Verification report artifacts/verification/setup-v0.7.3.json. Added HUONG-DAN.md to
packaging source/copied root; refreshed bundle README for current workflow/config
preservation/no STOP UI. Existing online dependencies/mirror repair retained. No
Pi installation/native runtime run performed. Previous bundles/updates untouched.


2026-11-07 explicit bundle device profile (0.7.4), user supplied exact JSON:
packaging/bundle/photobooth.default.json defaults real GPIO, BCM[22,10,9,11,5,6,13,19],
ActiveLow=false, frontToBack[1,2,3,4],200/1000/100ms,ffmpeg and loopback5000/video_feed.
Publish-SetupBundle now uses that profile by default; -GenericDefaults explicitly
retains prior sanitized simulation bundle. Bundle colors.json gets matching
RelayOrderFrontToBack because palette metadata controls actual app layer order.
Source app config unchanged by builder; existing installation JSONs still preserved
by setup. Installer reports actual installed mode rather than claiming simulation.
Universal0.7.4 archive227522859 bytes, SHA256
ce051a7491f3b366fb96d47029d32ea727fabbfd6217b9cfefbc379a397ffa5d.
Three variants Debug/Release,54 checks each/native scans,5 Python installer tests,
all payload manifests/profile equality/default layer order/18photos and entire
758-file tar hash comparison passed. No Pi installation or GPIO execution.
User subsequently asked servo wiring and stated no electrical knowledge. ASME-05
voltage/control pinout still unconfirmed; no exact hookup supplied. Asked for motor
rating/power-supply label; servo extension cable alone cannot establish safe hookup.


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


2026-11-09 separate Razr controllers, explicitly corrected by user: Razr I
only controls curtain relays; Razr II only controls camera angle. Removed all
light configuration, commands, timers and GPIO access from both. Legacy light
JSON fields are ignored and omitted on next save; existing deployment JSONs kept.
UserPhotoboothView/CurtainSettingsView serve I; new RazrIIView/RazrIISettingsView
serve II. Independent RazrModelSelectionView (Switch Razr in User/Developer), no mixed
model selector inside either settings page. Gear routes to the current model's
own settings; both modes access II basic settings, User hides hardware calibration.
Camera settings/model persist per-user razr.json; curtain palette/layers and
photobooth.json remain separate. Save/cancel/failure preserve the other model's
settings; switching retains both. Camera validation does not block I. II startup
never creates curtain GPIO; switching to II releases curtain outputs and back to
I lazily initializes them inactive. Each screen retains preview and corner hold.
II defaults Black Hole/Miller45/135deg, stage then START, settling and simulation;
IRazrHardware now exposes camera motion only (MCP4725@0x60, ADS1115@0x48 bus1,
AIN0 feedback). Calibration confirmation/limits/tolerance/stability/timeouts kept.
No inferred homing or electrical stop: DAC target remains on cancel/timeout/exit.
Legacy Python shutdown is BOARD18=BCM24; motor uses GPIO2/3 I2C, so GPIO3 power
button cannot share that bus. Stop old Python hardware owner before real C# use;
its camera HTTP feed is in the same process. Wiring/calibration remains unverified.
Debug/Release net10 clean;62 checks passed, including independent UI/settings,
legacy light migration, II startup without valid curtain config and no camera
movement in I. Both model corner-hold/short-tap sequences verified headlessly;
separate settings/main/model selector renders reviewed. ARM32 LegacyBuster/net8
and ARM64/net10 cross-publish passed. Artifacts isolated under razr-validation /
razr-publish because normal Debug executable is open; did not stop/relaunch it.
No setup bundle regenerated. No physical Pi/GPIO/I2C/motor/Buster runtime test.
See docs/RAZR_MODES.md.

2026-11-09 simplified controls: both main headers now show only CAMERA RAZR I/II,
removed capture-device/Auto second line. User now exposes Modes (User/Developer)
and Switch Razr (I/II), with existing movement guards. II gear in either mode opens
a simple page: two choice names, two angles, wait seconds. Developer-only advanced
motor expander collapsed by default. User Save applies only those five basic fields
and preserves simulation/calibration/I2C settings even if draft hardware fields are
changed programmatically.62 checks passed including this protection and User
switching; Debug/Release clean and User II header/settings renders reviewed.
No publish/bundle regeneration or physical Pi run for this UI-only update.

2026-11-09 settings/badge visual alignment at user request: Razr II settings
now use the same1200x900 Viewbox, centered820px rounded panel, typography, Back
header and Cancel/peach Save footer as Razr I. Two name/angle rows plus wait field;
Developer advanced section remains collapsed and scrollable. New shared RazrBadge
AXAML control draws a layered metallic frame and orange vector camera icon, with
CAMERA RAZR I/II text only. Both operating screens and settings headers reuse it.
Debug/Release clean; headless II main and I/II settings renders reviewed. Layout
only, no behavior/persistence/hardware changes, no new tests or Pi publish/run.

2026-11-09 Razr II input alignment: basic rows now centered with340px name /180px angle fields,52px heights; wait field180px centered below. Labels and text/numeric values centered horizontally; content vertically centered in inputs and form centered in panel with bounded advanced scrolling. Debug/Release clean and final headless User settings render reviewed. Layout only; no hardware run or publish.

2026-11-09 user requested pulse500ms (was200): changed PhotoboothSettings default,
source photobooth.json and bundle new-device profile; maximum1000ms and off100ms
unchanged. Modes button now Developer-only on both screens; User retains Switch
Razr and3s corner hold. README/Razr guide updated.62 checks, Debug/Release and
legacy ARM32/modern ARM64 publish passed; published JSON pulse500 verified.
User II render inspected. No physical Pi run or installer-bundle rebuild; existing
Pi JSONs are preserved on update and need explicit ActivationMilliseconds=500.

2026-11-09 update deliverable0.8.0-buster: artifacts/updates/CurtantControl-v0.8.0-buster-update.zip
contains3 managed files +VERSION/HUONG-DAN/SHA256SUMS; ZIP extraction hashes passed.
Non-app root dependencies unchanged vs0.7.5. Publish-RaspberryPi -Version0.8.0-buster
-Architecture arm -LegacyBuster passed Debug/Release,62 checks, native/ELF scans;
full archive under artifacts/releases. Preserve installed GPIO/palette settings;
Vietnamese instructions explicitly change only installed ActivationMilliseconds=500.
No automatic device update or physical Pi run.

2026-11-09 Razr II device report: user saw motor move then timeout in C#; asked
to match Python. Replaced target-error completion with consecutive-feedback
stability (previous angle starts0, AngleTolerance, stable0.5s, sample1s), preserving
timeout/cancellation/range checks and post-stability wait. Status Position stable
is not target verification. Focused stability/reset/off-target test added;63 checks
and legacy Debug/Release/ARM32 native scans plus modern ARM64 publish passed.
Update artifacts/updates/CurtantControl-v0.8.1-buster-update.zip,3 managed files
with instructions/version/hash; unchanged dependencies vs0.8.0 and extracted
hashes verified. Preserve per-user razr.json motor settings. No physical Pi test.

2026-11-09 simplified advanced Razr II settings after legacy-stability change:
removed UI fields for full-scale angle, DAC reference voltage, feedback min/max,
position-change tolerance and I2C bus/addresses. Backend and saved values preserved
because feedback scaling/stability still use them. Advanced retains simulation,
wiring/travel confirmation (same persisted CalibrationConfirmed flag), min/max
permitted angles and timeout. Basic two names/angles/wait unchanged. Legacy
Debug/Release,63 checks/native ARM32 publish passed; settings render reviewed.
Patch artifacts/updates/CurtantControl-v0.8.2-buster-update.zip extraction hashes
verified;3 managed files, no changed dependencies vs0.8.1. No physical Pi run.

2026-11-09 user supplied two-page ASMC-LQ manual (reviewed scanned pages): analog voltage control accepts0–5V; documented direct external voltage uses POT input/GND with selector NC and POT mode. Position output is explicitly coarse feedback, nominal0–3.3V for0–360deg. POT A adjusts sensitivity, POT B travel range; DR reverses direction and center is separately saved. Applies to documented family, exact board/jumper match still unverified. Manual provides no feedback noise tolerance; user's stationary ADS1115 samples0.512–0.560V do not establish electrical fault or acceptable noise. Keep command voltage and feedback ranges distinct; assess filtering/windowed stability rather than assume exact-angle feedback. No code/hardware settings changed in this review.

2026-11-09 motor-noise trial0.8.3: filtered feedback completion replaces0.8.1
consecutive pairs.100ms sampling;5-sample block means;5-mean window >=2s with
range<=AngleTolerance. Excessive raw block swings>max(12deg,6*tolerance) reset
window; no target-angle assertion. Timeout/range/cancel/post-wait unchanged.
Recorded stationary0.512-0.560V trace replay passes; tests reject travel/large
oscillation, require full time window and settle after movement.65checks, legacy
Debug/Release+ARM32 native/ELF scans, modern ARM64 publish passed. Patch
artifacts/updates/CurtantControl-v0.8.3-buster-update.zip:3managed files/instructions,
unchanged native dependencies vs0.8.2, extracted hashes verified. Preserve configs.
No physical Pi trial; user must test with motor.

2026-11-09 Razr II final-button persistence0.8.4: per-user razr.json records
LastCameraPosition/LastSimulatedCameraPosition (ID,angle,fullscale,Completed).
Pending saved before motor; failure to save blocks movement; success saves complete
after stability/wait. Browsing not saved; interrupted/failed commands not restored
as complete. Rebuild/restart/mode navigation restore matching last-complete button
with(saved), no motor writes or pending START. Angle/fullscale change invalidates
highlight; real/sim isolated.69checks include roundtrip/restart/no movement,
pending-write failure, failed move, mode navigation and settings invalidation.
Legacy Debug/Release/ARM32 native checks and modern ARM64 publish passed.
Patch artifacts/updates/CurtantControl-v0.8.4-buster-update.zip3managed files,
unchanged dependencies vs0.8.3; extracted hashes verified. No physical Pi run.

2026-11-09 user clarified button rename applies only to Razr II: State, with its camera-selection prompt updated. Razr I retains START and its prompts. Command names/behavior unchanged; no Pi package rebuilt for this text edit.

2026-11-09 small update0.8.5 at user request: Razr II button State, Razr I
START. artifacts/updates/CurtantControl-v0.8.5-buster-update.zip contains3managed
files plus version/Vietnamese guide/hashes, extracted hash verification passed.
Native/root dependencies unchanged vs0.8.4, net8 TFM checked; preserve all configs.
Release script from interrupted call completed and produced full build artifacts;
user deliverable is only the small ZIP, no full setup needed. No physical Pi run.

2026-11-09 fix selected Razr II button0.8.6: separate SelectedCameraPositionId /
SelectedSimulatedCameraPositionId saved on tap, independent of completed/pending
motor snapshots. Highlight/pending retry preserved through settings Back/Save,
errors and mode/restart. Failed motor still Unknown, not fabricated completion.
Older configs fall back to matching completed snapshots until next tap. STATE
uppercase only in Razr II and its selection prompt; I stays START.70checks include
reported Back/Save/error flow and independent selection/outcome. Legacy Debug/
Release ARM32 native scans +ARM64 publish passed. Small ZIP0.8.6 in artifacts/
updates, extracted hashes verified, unchanged dependencies vs0.8.5. Preserve
configs; no physical Pi run. Supersedes older browsing-not-persisted behavior.

2026-11-09 selected button border now red #FF3038 (4px) in both Razr operating views, shared across dark/light themes. Debug/Release passed; dark selected-hover and light selected renders inspected. Visual change only; no Pi patch generated for this edit.

2026-11-09 small patch0.8.7-buster packaged red selected border for both modes: artifacts/updates/CurtantControl-v0.8.7-buster-update.zip. ARM32 legacy publish passed; non-app dependencies identical to0.8.6;3managed files +guide/version/hashes, extracted hashes verified. Preserve all configs. No new physical Pi test.

2026-11-09 Razr selector cleanup: removed the two lower descriptive TextBlocks from each card per marked reference; model titles and Curtain relays/Camera angle motor retained. Debug/Release succeeded with NU1900 vulnerability-feed connectivity warnings; selector render generated. No behavior change or Pi patch generated.

2026-11-09 Razr II names now use centered dropdowns with reference presets: Red Room, Black Hole, Miller, Elevator, Subway, LightBlue, DarkBlue, Square pattern, Green, Brown, Yellow, Pink. Saved custom names appended to options so opening settings does not replace them. Draft Save/Cancel and duplicate-name validation retained. Debug/Release clean and settings render reviewed; no Pi patch generated yet.

2026-11-09 external Razr II name catalog0.8.8: razr2-names.json JSON string array
next to executable replaces hardcoded options; reloaded when settings opens.
Saved custom names appended; missing/invalid list logs and retains choices.
Project copies file to build/publish; setup.py preserves existing list on update.
71checks (external JSON validation/no overwrite included), legacy Debug/Release
ARM32/native scans, ARM64 publish and5installer tests passed. Small ZIP
artifacts/updates/CurtantControl-v0.8.8-buster-update.zip contains3managed files
plus name JSON/instructions/version/hashes; extracted hashes and12names checked.
Preserve configs and any previously edited names file. Includes selector text
cleanup from current source. No physical Pi run.

2026-11-09 Razr model chooser restyled like User/Developer chooser:1100x660 centered layout,42px heading, two equal260px rounded24px cards (peach I/themed II), simple icons/title/subtitle, centered Back/status. No removed long descriptions restored. Commands unchanged. Debug/Release clean, headless selector render reviewed. No small Pi package generated for this layout edit.

2026-11-09 small patch0.8.9: model chooser style update, artifacts/updates/CurtantControl-v0.8.9-buster-update.zip. ARM32 legacy publish and extracted hashes verified; native dependencies unchanged vs0.8.8.3managed files +names JSON/guide/version/hashes. Keep customized names JSON if already present. No physical Pi run.

2026-11-09 Razr II color swatches0.8.10: settings dropdowns now have color chips
like Razr I; successful Save rebuilds button name/color from the same current
curtain palette, without motion. Added Hip-hop/Grain Art/Vintage/Modern to names
JSON (16entries). New razr2-colors.json maps theme names to palette keys (custom
unknowns gray, name/key normalization fallback). Specific theme mappings are UI
default choices, not physical background measurements. Names/configs/state kept.
Setup preserves color-map JSON; no full bundle rebuilt.72app checks include
draft/failed-save/no-motion/palette equivalence;5installer tests, legacy Debug/
Release ARM32 native scans and ARM64 publish passed. Settings swatch render
reviewed. Small ZIP0.8.10 under artifacts/updates includes3managed files plus
2JSONs;16names/extracted hashes verified. If names JSON customized, merge4new
names rather than overwrite. No physical Pi run.

2026-11-09 consolidated Razr II name/color config0.8.11: only razr2-options.json
shipped, array{Name,Color}, Color supports shared palette key or #RRGGBB.16
defaults, reload on settings open. Removed source names/colors JSONs; old readers
retained only if combined file absent. Existing chosen names/state preserved;
custom two-file edits must be merged into combined file during small update.
Setup preserves existing options JSON.73app checks include combined file/duplicate
validation/no overwrite;5installer tests, legacy Debug/Release ARM32 native scans
and ARM64 publish passed. Small ZIP0.8.11 in artifacts/updates:3managed files
plus1JSON/guide/version/hashes, extracted hashes and16options verified. No Pi run.

2026-11-09 Razr II selected-state fix0.8.12: SelectCameraPosition now uses
IsCameraPositionApplied (completed snapshot ID/angle/fullscale). Already-applied
choice and browse-other/return keep highlight without enabling STATE/repeating
motor; actually applying another choice allows return. Restore uses same check.
Failed/unknown outcome still retryable, independent selected ID persistence kept.
74checks include applied/reselect/browse/settings/apply-other. Actual headless
button class+red border verified after STATE/settings Back/Save/browse-back via
--verify-razr-state helper. Legacy Debug/Release/ARM32 native checks passed.
Small ZIP0.8.12 under artifacts/updates,3managed files +optional options JSON,
unchanged native dependencies vs0.8.11 and extracted hashes verified. No Pi run.


2026-11-10 Razr II requested selectable Motor / Relay1-2 / Motor+Relay parallel.
Default remains Motor; Developer-only dropdown persists ControlMode in razr.json,
User cannot overwrite it. Lazy RazrPositionHardware composes motor and existing
interlocked relay wrapper: position1 R1, position2 R2; shared photobooth.json
mapping/polarity/simulation/pulse(default500ms)/maximum/off interval. Relay-only
skips motor hardware/calibration; parallel awaits both, cancels sibling on failure,
then applies one settle delay. DAC cancellation is not an electrical motor stop.
Completion snapshots now include relay execution profile to invalidate mode/wiring/
pulse changes. R1/R2 use marks Razr I M1 estimate Unknown before outputs. GPIO2/3
relay assignments rejected. No automatic backup detection or held-until-limit mode.
80 host checks passed including routing, overlap/cancel/fault, lazy resources,
Developer/User draft isolation, persistence and relay validation. Physical actuator
trigger behavior/wiring and Pi run remain unverified. Legacy Debug/Release,80 checks, ARM32 native/ELF scan and modern ARM64 publish passed.
Developer settings render reviewed. Fixed UTF-8 mojibake in Razr status/validation
strings introduced during this edit; Inter font unchanged. Small ZIP0.8.14-buster
under artifacts/updates verified against hash manifest; runtime dependencies
unchanged vs0.8.12.0.8.13 intermediate predates encoding fix, not delivered.
No physical Pi/actuator test.


2026-11-10 selection persistence follow-up: Razr I now saves selected background ID
independently in razr.json (real/simulated fields) on tap, and restores highlight on
startup/return to I without relay movement. Saved command states remain estimates;
START may reapply after restart. Startup mode entry preserves restored selection.
Razr II settings Save carries the selected button into the newly selected output/
simulation context, so switching Motor/Relays/parallel/real/simulated preserves
highlight while completed snapshots still invalidate for changed execution profile.
Added disk-relaunch tests for staged/applied I and II output/simulation settings matrix.
82 checks, legacy Debug/Release and ARM32 native/ELF publish passed; actual UI
red-border after STATE/settings/browse-back verified. Updated older real/sim test
expectation: selection crosses settings context, completion snapshots stay isolated.
Small ZIP0.8.15-buster generated, manifest verified, runtime dependencies unchanged
vs0.8.14. Preserve installed JSONs. No physical Pi run.


2026-11-11 standalone keycheck replacement created at user request:
packaging/raspberry-pi/keycheck/key_check_new.py (Python3.7) owns BCM24/pin18
shutdown and BCM25/pin22 confirmed graceful CurtantControl window close, not
Chromium.80ms polling debounce, boot-held release required, one action per press,
GUI worker does not block shutdown. Uses exact /proc/exe basename+wmctrl PID and
WM close; no force kill, motor/relay/I2C access or GPIO24 wake claim. Installer
for pi preserves old key_check1.py, backs up existing new script/drop-in, installs
wmctrl/zenity then enables/restarts existing keycheck.service as pi with DISPLAY0
and ~/.Xauthority. Requires existing sudo shutdown permission; no sudo policy edits.
6 mocked Python checks+Python3.7 syntax passed, Debug/Release and ARM64 publish
passed; no Pi GPIO/X11/shutdown or POSIX shell execution here. Standalone ZIP
artifacts/updates/CurtantControl-keycheck-v1.zip manifest verified. No C# app
binary changes; users keep installed Buster app. Check duplicate manual/autostart
old scripts before device acceptance.


2026-11-11 keycheck v2 at user request uses exact original PyAutoGUI popup:
Notify / ClienStop Continue ? / OK,Cancel and legacy cancelled/already-stopped
alerts, while still gracefully closing only CurtantControl. Lazy PyAutoGUI import
keeps shutdown independent of GUI; installer checks existing PyAutoGUI via find_spec,
installs wmctrl only. Explicit StandardOutput/StandardError journal override fixes
missing script logs observed with old unit output settings.7 mocked Python checks
and Python3.7 syntax/ZIP hashes passed. No C# or native dependency changes; prior
Debug/Release/ARM64 verification unchanged. No actual Pi/X11 popup verification.
artifacts/updates/CurtantControl-keycheck-v2.zip supersedes v1 for popup behavior.


2026-11-11 keycheck v2 installer failed on Pi: set Illegal option. Confirmed
install.sh had CRLF (set -eu\r) from Windows text writing. Normalized source shell
and v3 ZIP text entries to LF using explicit bytes; added line-ending regression
check. v3 ZIP hashes/LF content verified; no button behavior changes. Existing
extracted v2 can be repaired with sed -i 's/\r$//' install.sh then sh install.sh.
artifacts/updates/CurtantControl-keycheck-v3.zip supersedes v2. No Pi rerun here.


2026-11-11 keycheck v4 follows pending start-if-closed request and missed-popup
report. App button now checks app_pids: absent launches installed apphost once,
present opens unchanged legacy popup.40ms app debounce/10ms polling, shutdown
retains80ms. Preload PyAutoGUI after DISPLAY setup; logs raw transitions, accepted,
busy skips and answers. Existing modal Cancel alert still needs acknowledgement.
Actual missed-press cause unconfirmed without Pi logs.11 mocked tests +3.7 syntax
and v4 ZIP hashes/LF passed; no physical Pi test, no C# changes. Apps launched
from service inherit cgroup and may terminate on service restart; documented.
artifacts/updates/CurtantControl-keycheck-v4.zip, old versions preserved.


2026-11-11 keycheck v5 requested visible startup feedback: centered topmost Tk
440x240 camera vector icon, app name, animated Starting... shown before app launch.
Helper subprocess owns Tk mainloop; wmctrl launched-PID window appearance hides it;
process exit/60s timeout/missing executable show error with dismiss. No forced kill
on timeout. While helper active, duplicate app-button presses cannot open close
popup or another instance. Installer adds python3-tk.14 mocked checks/Python3.7
syntax/ZIP hash+LF checks passed; actual Pi/Tk visual placement not verified.
No C# binary changes. artifacts/updates/CurtantControl-keycheck-v5.zip.


2026-11-11 keycheck v6 requested Stop confirmation matching Starting style.
Shared create_overlay draws same dark/peach/camera/title;300px Stop dialog has
Cancel/Stop, Escape defaults cancel, no extra acknowledgement. Separate Tk helper
returns0 only after explicit Stop; parent then sends WM close to matching app
windows. Startup remains240px, same shared frame. Removed PyAutoGUI preload,
installer check and legacy alerts per new request.14 mocked tests adapted/pass,
Python3.7 syntax and v6 ZIP LF/hash checks pass. No Pi/Tk visual verification or
C# changes. artifacts/updates/CurtantControl-keycheck-v6.zip.


2026-11-12 keycheck v7 fixes missing Starting UI on desktop login: --start-app
uses same overlay as button without GPIO access. Installer invokes new
configure_autostart.py to migrate direct installed executable Exec entries in
~/.config/autostart, preserving metadata/unrelated entries and backing up changed
files; creates standalone entry if no match. Custom shell/rc.local launchers not
automatically migrated. Shared user flock prevents duplicate login/button launch;
button skips close during login startup.17 mocked host tests, Python3.7 syntax,
ZIP LF/hashes verified. No Pi/login/X11 run and no app-binary changes. Package
artifacts/updates/CurtantControl-keycheck-v7.zip; user must run installer then
logout/login or reboot to verify.


2026-11-12 Razr II full selection audit after user confirmed loss on app/Pi restart.
Added unified SelectedCameraButtonId +timestamp and independently injected per-user
CameraButtonSelectionStore(camera-button-selection.json); real/sim output contexts
no longer select separate UI preferences. Copies resolve newest timestamp; timestamps
advance even if system clock goes backwards. Both stores flush unique temporary files
before atomic rename. Config validation fallback preserves controller/button while
using simulated motor defaults. Legacy IDs migrate regardless of completed/angle
validity. Settings drafts carry current history instead of stale snapshots; model
chooser Back restores pending STATE, and Razr II CancelUserDraft returns before
unrelated Razr I draft can overwrite its label. Selection write failures retain UI;
visible warning when both copies fail. Added full2button/3output/result/navigation/
settings/restart matrix, actual-file relaunch and partial-write freshness tests.
87 checks passed including real file relaunch/freshness recovery, full UI navigation
matrix and actual headless selected red-border verification. Legacy Debug/Release,
ARM32 native/ELF scans and modern ARM64 publish passed. Small ZIP0.8.16-buster
manifest verified; runtime dependencies unchanged vs0.8.15. App update required,
not a keycheck script update. No physical Pi reboot test.


2026-11-12 user explicitly requested no position-stability checking, only send and
save state. RazrHardware now writes MCP4725 target only, no ADS1115 open/read,
feedback validation, stability detector or movement timeout. Wait time remains in
view model after selected outputs; completed means command sent, not actual position.
Removed unused stability class/tests and Developer timeout field. Legacy feedback/
timeout config properties retained but ignored by validation. Motor voltage/angle/
DAC bus-address/wiring confirmation and I2C/persistence errors remain. Added exact
DAC-byte/no-read tests using injected I2cDevice plus wait/state/cancel/write-error
checks. All0.8.16 selection/restart persistence changes retained.87 checks passed,
legacy Debug/Release/ARM32 native+ELF and modern ARM64 publish passed. Headless
selected red border and simplified advanced Settings renders checked. Small ZIP
0.8.17-buster hashes verified; native/runtime dependencies unchanged vs0.8.16.
No physical Pi/motor run.


2026-11-12 user requested transparent123Click PHOTO BOOTH logo matching reference,
placed throughout UI. Built-in image edit produced white/red and light-theme
charcoal/red PNGs in Assets/Brand; same white PNG in keycheck folder. Shared
BrandLogo AXAML/view behavior chooses by ActualThemeVariant. MainI/II headers,
SettingsI/II, mode/model choosers updated; existing Razr badges/actions retained.
Keycheck create_overlay displays same logo via PIL/ImageTk, fallback Tk/camera;
installer copies PNG and ensures PIL ImageTk. Actual headless dark/light/User/
Developer/settings/selector renders reviewed; no overlap, light logo visible.
17keycheck checks pass. No hardware/state behavior changes. Prompts/provenance in
docs/BRANDING.md. Legacy Debug/Release,87app checks, ARM32 native/ELF +modern
ARM64 publish passed. Final combined small ZIP0.8.19 hashes/LF verified, runtime
dependencies unchanged vs0.8.17; embeds app logos and includes keycheck/ installer
with white PNG. Final I/II simplified Settings renders reviewed. No physical Pi
or Tk popup visual test.0.8.18 is an undelivered intermediate release.


2026-11-12 during brand update user requested simpler Settings headers: removed
Back plus setup title/subtitle on both I/II, badge moved left and123Click stays right.
Cancel/Save footer remains.0.8.18 intermediate predates this header change; final
brand deliverable will be0.8.19. No navigation command/hardware/state changes.


2026-11-12 header spacing at user request: Razr I/II operating headers now use one
right-aligned horizontal StackPanel with12 spacing for Switch/diagnostic/theme/
Modes/settings/logo; hidden controls reserve no grid-column gaps. BrandLogo.tight
uses a clipped viewport around actual artwork, removing transparent image padding
from optical gap, while150x52 header slot keeps prior visible logo size. Other
logo placements preserve full image framing. No action/state/hardware changes.
Headless User/Developer renders reviewed. Final0.8.21 small ZIP hashes verified;
legacy Debug/Release,87checks and ARM32 native/ELF publish passed. No Pi run.


2026-11-12 header follow-up: user requested larger main-screen logo proportional
to52px action height. Tight viewport header slot increased200x52 (visible~48px).
Earlier0.8.20 intermediate predates final tight-selector fix and size; final patch
will be0.8.21. User asked Settings alignment opinion; recommended retaining820px
panel and aligning badge/logo/footer to its edges, not applied without instruction.


2026-11-12 user requested Settings logo same visible size as main and applied to
Starting/Stop popups. Both Settings now use tight artwork viewport270x72 logical
(to compensate Settings900-high Viewbox vs main680-high Viewbox at common16:9
screen). Popup shared create_overlay crops the same artwork region at runtime and
scales to240x60, preserving alpha; both Startup/Stop automatically reuse it.
Settings container alignment unchanged (user has not instructed that suggestion).
No input/command/state changes. Legacy Debug/Release,87app checks and ARM32
native/ELF publish passed;17keycheck checks and Python3.7 syntax passed. Headless
SettingsI/II renders reviewed. Combined small ZIP0.8.22 includes3managed app files
plus keycheck/installer/logo; LF/hash manifests verified, runtime dependencies
unchanged vs0.8.21. No physical Pi/Tk visual run.


2026-11-12 user reverted logo enlargement, requesting pre-enlargement User size
as standard: main tight150x52 slot (~150x36 artwork), Settings200x54 compensating
Viewbox scale, choosers tight150x52, Starting/Stop150x40 max artwork (Tk fallback
190px full image). Spacing12 and tight transparent-padding treatment retained.
No behavior changes. Legacy Debug/Release,87app checks, ARM32 native/ELF publish,
17keycheck checks and Python3.7 syntax passed. Main/Settings renders reviewed.
Combined small ZIP0.8.23 hashes/LF verified, dependencies unchanged vs0.8.22.
No physical Pi/Tk run.


2026-11-12 keycheck v9 at user screenshot request: shared popup content frame is
placed at relx/rely0.5 center, so logo/title/message/Cancel-Stop form one centered
group instead of top-packed. Display name is Curtant Control, Stop Curtant Control?.
Executable/path/service names,440x240/300 popup sizes, button dimensions and logo
size from0.8.23 unchanged. Starting/error content shares the center placement.
17Python checks +3.7 syntax +v9 ZIP LF/hash pass; no Pi/Tk visual run. No C# app
changes; standalone artifacts/updates/CurtantControl-keycheck-v9.zip.


2026-11-12 user accepted trying smaller main CAMERA RAZR badge: I/II now wrap
existing280x76 badge in230x62 Viewbox (~18% smaller), preserving shape/text/icon
proportions. Header row76 and52px action buttons, logo standard150px, spacing12
unchanged. Settings badges untouched. Visual-only. Legacy Debug/Release,87checks and ARM32
native/ELF publish passed; I/II User renders reviewed. Small ZIP0.8.24 hashes
verified, runtime dependencies unchanged vs0.8.23. No physical Pi test.


2026-11-12 user requested alternate large header trial: restored mainI/II badge
original280x76, wrapped entire right action/logo group in76px-high proportional
Viewbox from52px controls, preserving proportional text/icon/button spacing. Main
logo base200x52, so artwork approximately badge height after scaling. Duplicate
header RelayMode text removed for Developer fit; footer diagnostics remain.
Settings/popups untouched. User/Developer headless renders reviewed, no overlap.
Interpreted user's left/right wording from prior header discussion and stated it.
Legacy Debug/Release,87checks, ARM32 native/ELF publish passed. Small ZIP0.8.25
hashes verified, runtime dependencies unchanged vs0.8.24. No physical Pi test.


2026-11-12 user rejected0.8.25 enlarged trial, then asked shrink both10%. Stated
baseline as pre-enlargement0.8.24: badge230x62 ->207x55.8, right controls52 ->46.8
via proportional Viewbox, main logo base back to150px; Developer RelayMode header
restored. BothI/II, header row76 and all body/Settings/popups unchanged. Debug/Release,
legacy ARM32/net8 publish and User/Developer renders passed; native dependencies
byte-identical to0.8.25, ELF40/TFM checked. No repeat of87behavior checks for this
layout-only edit. Small ZIP0.8.26 hashes verified. No physical Pi test.


2026-11-12 user requested +5% from0.8.26: mainI/II badge207x55.8 ->217.35x58.59;
right group46.8 ->49.14 high. Uniform scaling retains proportions and spacing.
No body/Settings/popup/control changes. Debug/Release, legacyARM32/net8 publish
and headless render passed. Dependencies byte-identical vs0.8.26; ELF40/TFM
verified. Small ZIP0.8.27 hashes checked. No repeated behavior tests or Pi run.


2026-11-12 user requested final full app+keycheck package. Added
Publish-DeviceFinal.ps1 for BusterARM32 only, version0.9.0. Includes complete latest
app/layout,18 Backgrounds, device default profile, keycheck/logo/unit and one-command
root setup. Installer opts into button dependencies/integrity only when keycheck
included, verifies user pi, stages app preserving config/state, then installs buttons.
Fresh keycheck service supported (After display-manager.service, no graphical-target
ordering cycle); existing base unit kept with drop-in override. Desktop/menu start.sh
launchers also migrate to --start-app with backups, matching autostart/button.
No legacy rc.local auto edits or old Python app auto-kills. Root --check remains
read-only.8installer tests and18keycheck tests pass including config/state preservation,
combined dependency/preflight and desktop/menu migration. Debug/Release and legacy
ARM32 publish completed; all archive files/manifests and archive SHA256 verified,
Python3.7 syntax/LF scripts/ELF40 checked. Final archive is 75,140,461 bytes.
No physical Pi installation/run.


2026-11-12 added standalone rollback.py and Vietnamese ROLLBACK.md at user request.
Backup must run on Pi before update: captures r1 (old Python/keycheck), installed
app, user JSON, autostart/LXDE, standard shortcuts, service/drop-ins and rc.local.
Restore validates archive paths/link traversal, stages extraction, saves current
snapshot and renames replaced targets, restores service enablement then asks reboot.
Does not recover an old version without its snapshot or undo apt/firmware.
Three focused roundtrip/absent-target/unsafe-target tests passed; Python3.7 AST/LF
verified and Debug/Release builds passed. No native app/dependency changes; no new
Pi publish. No physical Pi sudo/systemd/restore verification. Delivered standalone
artifacts/updates/rollback.py; existing final0.9.0 archive remains unchanged.


2026-11-12 user requested installer stop old Chrome kiosk. Added legacy_kiosk.py:
recognized /home/pi/r1/cr_*.sh, camrazr-client.sh or explicit Chromium --kiosk
startup disabled with timestamp backups in user autostart/LXDE and sudo rc.local.
Only pi-owned Chromium main --kiosk processes receive SIGTERM; ordinary browser
and legacy Python startup preserved. Runs after app staging before shortcuts/buttons.
Standalone helper also delivered for already-installed0.9.0. Four migration tests
passed (33 Python tests total), Python3.7 syntax verified. Final0.9.1 Debug/Release,87 app checks and legacy ARM32 publish passed.
Full archive files/manifests/SHA256 verified. No physical Pi process/systemd/startup
verification.


2026-11-12 user requested single install/backup/rollback bundle. Final0.9.2 adds
rollback.py + rollback.sh; setup snapshots and validates before any mutating setup
action. Bundle-local .rollback-target retains first baseline across failed/retried
installs (never replaces it with partial state). Missing/corrupt backup aborts.
rollback.sh accepts recorded baseline or explicit archive; restore snapshots current
state and asks reboot. --check remains read-only. 36 Python tests passed including
backup-failure gating, check-before-pointer and retry preservation. Debug/Release,
87 app checks and legacy ARM32 publish passed. Full bundle archive/manifests/hash,
LF shell scripts and Python3.7 syntax verified. No physical Pi sudo/systemd/rollback
execution.


2026-11-12 user clarified rollback means stop CurtantControl and return immediately
to original Chromium + original key_check1.py.0.9.3 rollback.sh now uses legacy mode,
with optional bundle snapshot. Original keycheck/backend/launcher preflight happens
before stop; no invented legacy Python code. Stops relevant processes with SIGTERM,
saves current snapshot, restores if supplied, disables new/duplicate startup, writes
keycheck service to key_check1.py, manages app_model_1.py through new
camrazr-legacy-backend.service, enables kiosk desktop startup, launches original
script as pi. Snapshot-only restore remains separate.40 Python tests pass;
Python3.7 AST checked. Full0.9.3 Debug/Release,87 app checks and legacyARM32
publish passed; bundle files/manifests/SHA256 verified. No Pi runtime verification.


2026-11-12 Pi rollback reported stale-unit warning and missing legacy-backend unit.
Fixed legacy rollback to daemon-reload before inspecting/stopping services and skip
LoadState=not-found. Actual inspect/stop failures abort instead of being ignored.
Eight rollback tests including first-run missing unit and stop failure pass;
Python3.7 AST verified. Delivered updated standalone artifacts/updates/rollback.py
for replacement in extracted0.9.3 bundle; published0.9.3 archive remains unchanged.
No physical Pi retest.


2026-11-12 user reports rollback has zero output. Exact device blocking stage
unknown. Added immediate shell/Python starting messages, unbuffered sudo child,
phase/progress heartbeats every5s for archive validation/save/extraction and
45s limits on legacy systemctl commands, with actionable timeout errors.
No removal of safety backup or guessed Pi diagnosis. Delivered rollback-fix-v2.zip
containing replacement rollback.py/sh;0.9.3 archive unchanged. Python3.7 AST
verified; focused timeout/progress/orchestration checks pass. No device retest.


2026-11-12 Pi install-after-rollback failed because old app_model_1.py was active.
Added backup-install/stop-apps actions: stop relevant services/processes before
file backup, preserve original enabled state. Retry validates first snapshot then
stops apps too. Integrated setup allows running app when managed stop is available.
Migration disables managed legacy backend and recognized old Python startup as
well as kiosk, preventing post-reboot simultaneous hardware control.
Delivered reinstall-fix-v3.zip with setup/rollback.py/sh/legacy_kiosk.py replacements.
44 Python tests and Python3.7 AST pass. Full0.9.4 Debug/Release,87 app checks
and legacy ARM32 publish passed; archive/manifests/SHA256 and patch equivalence
verified. No Pi retest.


2026-11-12 user reports two shortcut icons and requests existence check. Found
keycheck configure_shortcuts wrote .desktop.bak-* beside live Desktop/menu entries.
Moved backups outside those folders. install-shortcut.py now reuses matching
canonical files (no rewrite on repeat), preserves Starting helper, moves recognized
duplicate copies/backups off Desktop/menu, preserves unrelated commands.
Four focused shortcut tests;48 Python tests total passed; Python3.7 AST verified.
Delivered shortcut-fix.zip for existing installation, no service/autostart changes.
Full0.9.5 Debug/Release,87 app checks and legacy ARM32 publish passed;
bundle files/manifests/SHA256 and repair ZIP verified. Desktop appearance still
needs actual Pi check.


2026-11-16 user requests User Settings hidden, PIN-gated Switch Razr, touch numeric
keypad and Developer PIN editing. Implemented for both I/II: gear hidden/User open
commands blocked, fresh 4–8 digit challenge per chooser, default1234, modal keypad
and masked input. Developer header Switch PIN sets/confirm new PIN. Salted PBKDF2
store switch-pin.json atomic/Flush(true), Unix user-only; separate from controller
configuration so settings/save/state cannot revert PIN. Failures stay locked or
show save error; no hardware commands on PIN entry. Existing Developer entry not
PIN-gated (scope is controller switch).90 model/hardware checks passed including
wrong PIN/cancel/re-auth, confirmation/leading-zero/bounds, restart/hash, read/save
failure. Actual headless mouse taps verify keypad/delete/Cancel/OK in1280x760 dark
and800x480 light; both images inspected. Debug build isolated because a user app
process held normal output executable; left it running. Normal net10 Debug/Release and legacy net8 Debug/Release passed,90 checks
passed on release candidate. Full0.10.0 ARM32 publish/native compatibility and
archive files/manifests/SHA256/net8/ELF40 verified. No actual Pi test.


2026-11-16 user requests hide Dark/Light switch and default Dark. Removed the
complete toggle containers from both I/II headers (User/Developer), so no empty
gap remains. Startup ignores old saved light preference and starts dark. Theme
resources/preferences API kept for compatibility/preview, no UI toggle.0.10.1
Debug/Release and90 checks passed (appearance test updated to ignore old light
preference), legacy ARM32 publish passed. Compact User render inspected without
toggle/gap; full archive files/manifests/SHA256 verified. No physical Pi test.

2026-11-16 user requested small0.10.1 update. Packaged DLL/deps/runtimeconfig,
VERSION and guide/checksums only. Runtime/native managed dependencies verified
byte-identical vs0.10.0 and0.9.5; configuration/PIN/keycheck not included or changed.
ZIP contents/hash verified; existing90-test publish reused. No Pi test.

2026-11-16 user requested rebuild. Rebuilt current source as0.10.2-buster ARM32:
Debug/Release and90 checks passed, native compatibility/ELF40/net8 verified.
Small0.10.2 packages three app files/version/guide/hashes; dependencies byte-
identical vs0.10.1. ZIP verified. No config/PIN/keycheck changes or Pi test.

2026-11-21 user requested Release small rebuild. Built0.10.3-buster ARM32 with
Debug/Release and90 checks passed; native compatibility/ELF40/net8 verified.
Packaged app DLL/deps/runtimeconfig + version/guide/hashes; ZIP contents/checksums
verified. No config/PIN/keycheck included. Previous artifacts were absent so no
byte comparison against earlier runtime files was possible; pinned package versions
unchanged. For an existing full Buster ARM32 installation only. No Pi run.

2026-11-21 user requested complete release. Built full0.10.4 Pi Buster ARM32
bundle with app/runtime/Backgrounds, latest keycheck, backup-install/legacy rollback,
progress/timeouts, kiosk migration, single-shortcut fix, PIN keypad and dark default.
Debug/Release and90 app checks,48 Python checks passed. Archive files/manifests/
SHA256, LF scripts, Python3.7 AST, net8/ELF40 verified. No physical Pi test.


2026-11-21 user: Stop popup disappears but app stays. Device journal confirms
keycheck requested WM close for1 matching window. Existing app teardown awaited
camera tasks indefinitely and Closing lacked error recovery.0.10.5 adds explicit
cancellation of all sources, callback-error isolation, immediate relay stop,
movement cleanup and output disposal before shared3s camera/background cleanup
budget. MainWindow catches errors, queues final close, logs stages to shutdown.log.
No keycheck change/force-kill. Added hanging discovery/cancel-fault regression and
headless close check (synchronous + noncooperative capture, repeated close, output
release); headless Debug passes. Actual Pi blocking stage still unconfirmed until
new shutdown.log; no physical Pi test. Debug/Release and91 checks passed; Release headless window-close checks passed
for synchronous and stubborn-camera cases. Full and small0.10.5 archive contents/
checksums verified; runtime dependencies unchanged vs0.10.4.

2026-11-21 user designated GitHub repository trungvu0128/CurtantControl. Remote
origin configured to its HTTPS URL; remote had no refs at inspection. Initial
source branch codex/initial-import; artifacts/bin/obj/cache files excluded, common
.env excluded and LF text checkout configured. User approved author trungvu0128
with GitHub noreply (verified account id72425062). Source remains same validated
0.10.5 behavior; GitHub update-check feature is not implemented by repo linking.

Initial source commit f2df459 pushed successfully; remote SHA verified. GitHub
HEAD/default branch is codex/initial-import and local tracks origin branch. No
release assets or updater workflow published as part of this repository setup.


2026-11-21 user authorized GitHub Check Update and automatic check on app startup.
0.11.0 targets public trungvu0128/CurtantControl Releases, once per window startup,
nonblocking15s check; offline does not block User/camera, no automatic installation.
Developer Updates modal checks/downloads/prepares full BusterARM32+SHA256 only.
Worker apply-update.py in independent user systemd unit validates size/hash/paths/
manifests/platform/libs/disk/sudo before READY, waits old PID exit then existing
full backup/setup; restores snapshot on failure, execs installed/restored app.
Cancel marker + unit stop handle cancellation/readiness race. Per-user update
status and optional Tk progress; PIN/config/state preserved. Full bootstrap needed.
Publisher script uses local Git credential helper; draft assets checked before
publication, no token on Pi.97 C# checks and56 Python checks passed; UI renders
inspected at1280x760/800x480. Python3.7 AST passed. Source version0.11.0.
Full0.11.0 Debug/Release and97 checks passed;56 Python checks passed. Native
ARM32/net8 compatibility, archive/manifests and real updater extraction verified.
Source673f809 fast-forwarded to default branch; GitHub v0.11.0 release published
with verified archive/checksum assets. Host-compiled updater tested against public
GitHub API: prior version sees0.11.0, current version does not offer itself, full
archive downloads/checksums and worker extraction pass. Release headless shutdown
check passed. No actual Pi systemd/sudo/X11 update transaction run. See UPDATES.md.

0.11.1 adds version-pair small updates. Builder compares both full bundles and
includes changed app files only. Worker verifies installed hashes, takes file
backups and replaces the changed files after app exit. Baseline mismatches use
the full bundle before shutdown. Manual bootstrap for 0.11.0 uses the helper
inside the small ZIP. Debug/Release Buster ARM32 build, 98 C# checks, 57 Python
checks, Python 3.7 syntax and patch application to a copy of 0.11.0 passed. The
952055-byte ZIP preserves customized settings in that check. No physical Pi test.

0.11.2 displays a non-interactive startup check banner for at least 1.2 seconds.
The banner clears on completion and becomes an update notice when applicable.
Existing one-check startup and idle/modal visibility rules remain in place.
Debug/Release, 98 app checks and Buster ARM32 publish passed. Headless 800×480
preview shows the banner. Verified small patches from both 0.11.0 and 0.11.1
against copied installations with a custom config. No physical Pi test.

0.11.3 cleans installer files after successful `install.sh` and verified manual
ZIPs after a successful small update. Reopening the app cleans completed updater
cache jobs. Failed/in-progress jobs stay available. Full rollback tools are kept
in per-user recovery, and snapshots and small file backups remain available.
98 app and 61 Python checks passed, including unrelated-file preservation and
rollback retention. Buster ARM32 publish and real full/small artifact cleanup
passed against temp installations for 0.11.0–0.11.2. No physical Pi test.

2026-11-23 added Camera RAZR 3 to the PIN-gated model chooser. Outside commands
the shared DAC motor only. Room 1 waits after rotation and then routes the four
physical curtain pairs through the existing layer planner. Subsequent colors in
the same settled Room 1 session reuse that direction. The independent per-user
razr3.json stores calibration, color assignments and staged selection. The default
is simulation even on installations with real GPIO configured for Razr I.
Debug and Release built, 104 host checks passed, and the 800×480 and desktop
screens rendered. The simulated Outside → Green → Purple → White sequence ran
through Start with an activity log. Linux ARM64 and Buster ARM32 were cross-published.
Pending physical acceptance includes both measured angles, settling time, shared
bracket travel, actual curtains and Buster runtime operation.

2026-11-23 RAZR I and RAZR 3 share UserPhotoboothView. The RAZR 3 layout follows
the supplied reference with five swatches on the left and Start and Open
landscape above the camera on the right. Reviewed RAZR 3 User at 800×480 and
Developer at 1280×760. Debug and Release builds succeeded and the headless
RAZR 3 scenario completed.

2026-11-23 replaced the low-resolution Open landscape crop with a 1280×720
Lake Bled photo and enabled high-quality scaling for the tile. Source and CC0
license are in DEVELOPMENT.md. Debug, Release and both screen renders passed.

2026-11-23 published v0.12.0 on GitHub from d91cded. Debug and Release Buster ARM32
builds, 104 app checks, 61 Python checks and the RAZR 3 headless render passed.
Verified the full archive manifests and applied the small update to a copy of the
public v0.11.3 bundle with customized settings. GitHub's four asset digests matched
the local builds, and the downloaded patch matched its SHA-256. Physical Pi
installation and hardware calibration remain pending.

2026-11-23 simplified RAZR 3 Settings to angles, settling time, simulation and
five backdrop assignments. The reference gives 70°–180° without direction labels.
Empty simulation profiles prefill Outside 70° and Room 1 180° where allowed by
saved limits. Prefilling resets hardware confirmation. Advanced calibration stays
in razr3.json. Debug, Release, 105 app checks, 800×480 Settings rendering and
Buster ARM32/.NET 8 cross-publish passed.

2026-11-23 published v0.12.1 from 2c8a6d6. The Buster bundle passed 105 app
checks, 61 Python checks and the Settings render. Applied the small update to a
copy of the public v0.12.0 bundle and checked backups plus saved angle, palette
and relay settings. GitHub tag and all four asset digests matched the local build.
The public patch download passed SHA-256 verification. No physical Pi run.

2026-11-23 moved the Open landscape outline above its photo so the rounded
corners stay visible. The tile uses a rounded keyboard focus indicator and keeps
the red outline when selected with focus. Debug and Release passed. The rendered
focus state showed red at all four corners, and the RAZR I preview matched its reference.

2026-11-23 published v0.12.2 from c212182. Debug and Release Buster ARM32
builds, 105 app checks, 61 Python checks and the four-corner RAZR 3 focus render
passed. Applied the small update to a copy of the public v0.12.1 bundle and
verified replaced files, backups and preserved settings. The GitHub tag, four
asset digests and downloaded patch matched the local release. No physical Pi run.

2026-11-23 investigated a reported sudo error during full update. Verified the
v0.12.2 full archive and reproduced a simulated password-required denial before
app close. The worker now preserves sudo's actual error and points to manual
installation as pi. The user reported code -1. As a Python subprocess return
code on Linux that means SIGHUP, and the worker now reports signal termination
separately from denial. The device journal is pending. Debug and Release,
105 app and 63 Python checks, Python 3.7 syntax and ARM32 Buster/ARM64 cross-publishes
passed. The diagnostics change has no new GitHub release or physical Pi test.

2026-11-25 investigated a possible RAZR I layer reversal issue. The v0.12.2
RelayChecks suite passed. Headless Settings selection and Save persisted both
orders at 800×480 and 1280×760 with no relay pulse on Save. With M1 and M4
open, M2 emitted R2 then R3 for M1-nearest, or R8 then R3 for M4-nearest.
Paper button order, physical relay pairs and White behavior are independent of
this setting. No app code change. Actual Pi behavior remains unconfirmed.

2026-11-25 user requested RAZR I relay presets also reverse color assignments.
R7/R8-nearest keeps Pink through Purple on M1 through M4. R1/R2-nearest maps
the color slots to M4 through M1 in the same button order. Save persists mapping
and layer order together and leaves physical motor states attached to their IDs.
Legacy mappings stay active until a preset is selected. RAZR 3 keeps its own
color assignments. Debug/Release, 106 checks, 800×480 and 1280×760 headless
Settings interaction, and Pi Buster ARM32/ARM64 cross-publishes passed.

2026-11-25 published v0.12.3 from 5c28036. Release packaging from an isolated
worktree included the RAZR I change. The pending sudo diagnostic edits stayed
local. Debug/Release, 106 app and 61 Python checks, Buster ARM32 packaging, and
small/full update staging against copies of v0.12.2 passed. Preserved the old
defaults JSON line endings for exact small-update compatibility. GitHub tag,
four asset digests and the downloaded patch matched the local release.
No physical Pi test.

2026-11-25 user requested the literal relay order from the new sheet. RAZR I
with the saved R1/R2-nearest reversed preset runs White 1/3/5/7, Pink 8/5/3/1,
Sky Blue 6/3/1, Green 4/1, Purple 1, one timed pulse at a time. The same R1
appears under White and Purple as written. A completed sequence marks only
command completion and leaves affected physical positions Unknown. Stop or
error aborts remaining pulses. Debug/Release, 107 tests and Buster ARM32
cross-publish passed. Physical movement remains unverified.

2026-11-25 published v0.12.4 from cf1a894. Debug/Release, 107 app and 61 Python
checks and Buster ARM32 packaging passed. The small and full updates were tested
against copies of v0.12.3 with customized settings, and backups were verified.
The GitHub tag, all four asset digests and the public patch download matched.
No physical Pi test.

2026-11-25 user requested rollback of v0.12.4. Published v0.12.5 from 588bb89
with v0.12.3 source behavior and a newer version for the updater. Only the
version field differs from the v0.12.3 source tree. Debug/Release, 106 app
and 61 Python checks and Buster ARM32 packaging passed. Applied small updates
and staged full updates on copies of v0.12.3 and v0.12.4, preserving settings
and checking backups. GitHub tag, six asset digests and both downloaded
patches matched. No physical Pi test.

2026-11-25 restored v0.12.4 RAZR I behavior in source and extended the shared
relay preset to RAZR 3. The models maintain independent colors and share the
mapping by backdrop slot and relay planning. Under R1/R2 nearest, RAZR 3 White,
Sky Blue, Green, Purple, Gray use 1/3/5/7, 8/5/3/1, 6/3/1, 4/1, 1 respectively.
Indoor pulses start after Room 1 settles. Open landscape leaves the relays off.
Debug/Release, 108 host checks, 800×480 renders, and ARM32 Buster/ARM64
cross-publishes passed. No physical Pi test.

2026-11-25 published v0.12.6 from 3510d37. Debug/Release, 108 app checks, 61
Python checks and Buster ARM32 packaging passed. Tested small patches and full
staging on copies of v0.12.4 and v0.12.5 with saved settings and verified backups.
GitHub tag, all six digests and both public patch downloads matched. Physical
Pi operation still needs verification.

2026-11-26 device report clarified full-update failure as sudo -n -v exit 1.
Reproduced the v0.12.6 worker failing before app close and leaving a verified
bundle and checksum reusable in the cache. Exit 1 alone does not identify the
Pi's authorization error. Unreleased diagnostics now include the retained
archive path. Added terminal recovery instructions without sudo policy changes.
Debug/Release, 63 Python tests and Python 3.7 syntax checks passed. No Pi run.

2026-11-26 full Updates stages the whole app as pi when the system setup
revision and installed keycheck files match and keycheck is enabled and active.
The publisher stamps the revision before manifest generation. The directory
swap keeps a backup and existing configuration. System changes use existing
noninteractive sudo or stop before app closure. Devices on 0.12.6 can bootstrap
with a compatible small patch. Debug/Release and ARM32 Buster/ARM64 cross-
publishes passed, as did 64 Python tests including a full app swap, preserved
settings, cache cleanup and pre-handoff refusal. No physical Pi test or release.
