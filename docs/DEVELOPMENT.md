# Development guide

## Architecture

The solution contains a cross-platform desktop project at
`src/CurtantControl/CurtantControl.csproj` targeting `net10.0`, plus a console
verification project at `tests/CurtantControl.RelayChecks`.

| Location | Responsibility |
| --- | --- |
| `Program.cs` | Avalonia platform detection and desktop application lifetime |
| `App.axaml` | Application theme |
| `App.axaml.cs` | Main window and view model composition |
| `Views/` | AXAML layout and view-specific code-behind |
| `ViewModels/` | Observable presentation state and relay commands |
| `ViewLocator.cs` | Convention-based view resolution |
| `Assets/` | Embedded Avalonia resources |
| `Services/Relays/` | Timed curtain relay commands, GPIO adapter, and simulation |
| `Properties/PublishProfiles/RaspberryPi64.pubxml` | Linux ARM64 publish settings |

The main screen provides curtain color selection, a highlighted requested
background, Close all, Stop, and a USB webcam preview. `BackgroundController`
closes foreground layers before opening the selected curtain. Runtime settings
load from `photobooth.json` beside the executable and default to simulated relays.
`CurtainSettingsView` provides fixed position color dropdowns through
`CurtainSettingsViewModel`. Saved layer order overrides the deployment default via
`CurtainOrderStore` in the current user's local application data directory. The
draft applies only after persistence succeeds; Save does not drive relays.
See `PHOTOBOOTH_UI.md` and `RELAY_CONTROL.md` for configuration and contracts.
The FFmpeg camera adapter is separate from UI bitmap presentation. There is no
photo recording or network API.
`ICameraCatalog` discovers Windows DirectShow or Linux V4L2 capture devices; the
preview dropdown switches a single `ICameraPreview` session at a time. Both are
injectable for device discovery, switching, and shutdown checks without hardware.

## Toolchain

`global.json` selects SDK 10.0.403 with `latestFeature` roll-forward and preview
SDKs disabled. Use a compatible stable .NET 10 SDK. Package versions are pinned
in the project file; the template package is not needed to build an existing
checkout. No MAUI workload is required.

## Commands

Run from the repository root:

```powershell
dotnet restore CurtantControl.slnx
dotnet build CurtantControl.slnx
dotnet build CurtantControl.slnx -c Release
dotnet run --project src/CurtantControl
dotnet publish src/CurtantControl -p:PublishProfile=RaspberryPi64
dotnet run --project tests/CurtantControl.RelayChecks
```

The publish directory is `artifacts/raspberry-pi/linux-arm64/` relative to the
repository root. See the README for transfer, dependencies, and launch steps.
For a versioned archive, run `./scripts/Publish-RaspberryPi.ps1 -Version 0.1.0`
using an unused version. It validates builds/checks and packages under
`artifacts/releases/`; see `RELEASES.md`. User wiring values in the source config
are preserved, while the packaged configuration always starts with simulation on.

Debug and Release use different diagnostic-package assets. Let `dotnet build`
restore after switching configurations; using `--no-restore` immediately after
a Release publish can leave Debug without its developer-tool references.

## Validation

- For C# or AXAML changes, build Debug and Release, then manually check the changed
  interaction when a graphical session is available.
- For deployment-sensitive changes, publish with `RaspberryPi64` and verify the
  output includes the executable and its runtime/native libraries.
- For device acceptance, copy the full output to a Pi 4, launch within its desktop
  session, and verify layout and interactions on the actual display.
- Record checks actually run and distinguish build success from runtime success.

The initial ARM64 executable was checked as an ELF binary with machine type 183
(AArch64). That check does not replace a device run.

## Environment troubleshooting

NuGet restore needs access to `api.nuget.org`. The .NET CLI and Avalonia build
services may write caches/logs outside this repository in the user's profile.
If a sandbox denies those writes or network access, request the appropriate
execution permission through the environment's approval mechanism and retry.
Do not change application code to work around a build sandbox restriction.

Choose `RaspberryPi32` (`linux-arm`) when `getconf LONG_BIT` reports 32, or
`RaspberryPi64` (`linux-arm64`) when it reports 64. A kernel reporting aarch64 does
not prove 64-bit userspace; the user's Pi is a confirmed counterexample. Both
profiles need an X11/XWayland graphical session. OS Lite, kiosk/framebuffer startup,
and boot-time launch need separate configuration and validation.

Versioned 32-bit package command:

```powershell
./scripts/Publish-RaspberryPi.ps1 -Version 0.1.2 -Architecture arm
```

For the existing Buster device, explicitly opt into the compatibility candidate:

```powershell
./scripts/Publish-RaspberryPi.ps1 -Version 0.1.3-buster -Architecture arm -LegacyBuster
```

`LegacyBuster=true` targets net8.0 while retaining the stable .NET 10 SDK and C# 14
compiler for generated partial observable properties. The default target remains
net10.0. Tests run on the host's net10.0 runner referencing the net8.0 application;
they do not establish ARM32 runtime compatibility. Packaging additionally scans
native symbol tags against GLIBC 2.28 / GLIBCXX 3.4.25 ceilings. Device testing
and external library checks are still required. See `RELEASES.md` for support limits.

## Installation bundle

Run `./scripts/Publish-SetupBundle.ps1 -Version 0.2.0` with an unused stable
version. It builds/checks/publishes three payloads (legacy Buster ARM32 and modern
ARM32/ARM64), removes development wiring and camera overrides from bundle-only
configs, generates per-payload SHA256 manifests, and archives under artifacts/bundles.
The source photobooth.json is unchanged. Installer source is packaging/bundle;
read packaging/bundle/README.md for supported OS versions and device steps.

Installer host checks: `python tests/test_bundle_setup.py` with Python 3.
They cover OS/architecture selection, dependency variants, checksums, fresh install,
configuration preservation and backups using temporary files. No apt/sudo/native
Linux execution occurs in host tests. Actual device installation remains necessary.

The Buster installer now migrates only obsolete Aliyun Raspbian Buster .list entries before apt update, retaining timestamped backups and signature verification. New bundles inherit this fix; the original 0.2.0 archive predates it.

CurtainPaletteStore loads/atomically saves colors.json beside the executable. CurtainSettingsViewModel edits appearance in the existing four fixed-position dropdowns. Duplicate color choices are rejected without swapping. Existing layer order is retained. CurtainOption name/color are observable; IDs and relay mapping remain fixed. The bundle installer preserves both photobooth.json and colors.json during updates.

App composition injects CurtainCommandStateStore into MainViewModel. ReportCurtain records pending Unknown before relay activation and persists final command states. Startup restores annotated estimates without movement or selection guards. Separate hardware/simulation files in per-user data survive app-folder updates. Tests inject stores and cover round-trip restore, interrupted commands, corrupt files and blocked pulses on pending-write failure.

MainViewModel.Modes.cs owns Startup/Developer/User navigation and staged User-mode selection. StartupModeView and UserPhotoboothView are AXAML-only layouts with initialization-only code-behind. Window Opened requests camera startup; first mode choice releases discovery. New headless render options: --render-startup, --render-user (synthetic gradient), --render-user-compact (1024x600). Mode tests verify no pulse on staging, START/Stop semantics and no mode change during commands.


## Color sample previews

Settings now offers 18 colors/themes: Pink, Sky Blue, Green, Gray, Dark Blue,
Purple, Yellow, Red, White, Brown, Gold, Black, Silver, Grain Art, Vintage,
Antique, Modern and Hip-hop. Four physical curtain slots remain unchanged.
In User mode, tapping a color stages it and shows its sample portrait for two
seconds, then returns to live video. START clears the sample immediately and
applies the staged curtain sequence. Developer mode also previews first and waits for START.
The live capture continues behind the sample. Missing photos leave live video
available. Stop, mode changes and shutdown cancel the sample timer.

`colors.json` accepts optional `PreviewImage` paths such as
`Backgrounds/pink.png`. Keep the `Backgrounds` folder beside the app. Shipped
`colors.defaults.json` merges new choices and missing image paths without
replacing custom slot assignments, names or swatches. Save in Settings persists
the merged palette. See [preview assets and prompt set](BACKGROUND_PREVIEWS.md).


## Shared Developer photobooth layout (0.5.1)

Developer and User modes now share the glossy curtain buttons, header and camera
layout in UserPhotoboothView. Developer adds the bottom activity log and curtain
command status, camera selector/Refresh, relay checks/Clear, settings and theme
switch. Developer color taps still activate immediately; User retains sample
previews and START. The shared view has separate layout heights, with Developer
allowing room for diagnostics. Both modes share the saved dark/light preference and offer settings access.


0.5.2: Both modes expose theme and settings controls; User hides STOP and retains
START. Removed the BACKDROP / PHOTO BOOTH header text. Glossy button templates
explicitly stretch the presenter and center content horizontally/vertically.
Developer keeps STOP and diagnostics. Settings opens without applying curtain
commands; the current mode remains selected when returning.


Glossy buttons use PhotoContentPresenter to avoid Fluent template hover backgrounds. Render actual curtain hover with RelayChecks --render-curtain-hover PATH. Header controls use equal18px gaps.


App composition enters User before window creation. UserPhotoboothView code-behind implements the cancellable3s corner gesture; verify real pointer sequence with RelayChecks --verify-mode-hold PATH.


App composition requests WindowState.FullScreen at startup. Headless layout captures override WindowState.Normal and do not verify desktop window-manager fullscreen behavior.


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


0.6.2 Settings presents all five selectors in one centered vertical list, in main-screen order: fixed background, M1, M2, M3, M4. This supersedes the separate right-side fixed selector and the former front-to-back Settings display. Relay layer order remains M4 to M1.


## Developer preview and START (0.6.3)

Developer mode now stages color selections just like User mode: a matching sample
photo displays for two seconds, returns to the shared live camera, and START is
the only background-application action. Selecting fixed or motorized backgrounds
sends no relay commands. The applied-background repeat guard remains. The STOP button is hidden in both modes. Camera/settings/log controls
remain available according to existing movement guards.

The Developer status list now shows all five backgrounds in main-screen order.
The fixed background has derived Visible/Hidden/Changing/Unknown command status:
all four motors Closed means Visible, any Opened means Hidden, active movement
means Changing, and inconclusive state means Unknown. Restored estimates retain
(saved). These are inferred command states, not physical sensor readings.


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


0.7.5 supersedes distance sorting: display follows the paper, fixed background first, then M1,M2,M3,M4. Main and Settings remain aligned. Changing Developer relay layer order changes control sequencing only, not button positions or physical color/relay IDs.

## Separate Razr controllers

See [RAZR_MODES.md](RAZR_MODES.md). UserPhotoboothView and CurtainSettingsView
serve Razr I only; RazrIIView and RazrIISettingsView serve camera position control
only. RazrModelSelectionView is the independent installed-model chooser. Shared
preview/mode/theme services remain reused. MainViewModel.Razr owns model selection
and camera settings/START; no light state, timers, GPIO service or settings remain.
IRazrHardware exposes camera movement only. Curtain GPIO initializes only in Razr I.
Camera settings/model persist per user; curtain settings retain their existing files.
Use --artifacts-path artifacts/razr-validation if the normal Debug executable is locked.

Razr II basic settings and model switching are available in User/Developer.
User Save copies only the two names/angles and settling interval into the applied
configuration; hardware fields remain unchanged. Advanced motor controls use a
collapsed Developer-only expander. Operating headers show model names only.

Both operating screens and settings headers reuse Views/RazrBadge, an AXAML
metallic camera badge with a vector orange camera icon and a Text styled property.
Razr II settings now match the Razr I 1200x900 Viewbox, 820px rounded panel,
control sizing, header and Cancel/peach Save footer; advanced controls still scroll.

Relay defaults now use a500ms activation pulse (maximum1000ms, off interval100ms
unchanged). Source and new-device bundle profiles match; installer updates still
preserve an existing device photobooth.json. Modes is Developer-only; User retains
Switch Razr and the three-second corner gesture for User/Developer navigation.

Razr II completion uses CameraPositionStability: ADC samples about100ms apart,
five-sample block averages, five averaged blocks spanning at least2s. The range
of those means must fit AngleTolerance; large intra-block oscillation (>max(12deg,
6*tolerance)) resets the window. Target accuracy is not asserted. Timeout,
cancellation, feedback range checks and configured post-stability wait remain.

RazrConfiguration now includes LastCameraPosition/LastSimulatedCameraPosition
snapshots. MainViewModel persists pending before motor access, completion after
stability and wait, and restores matching completed snapshots without commands.
Records are immutable so Copy keeps settings drafts isolated from state updates.

Razr II choices now load from one razr2-options.json beside executable: an array
of {Name, Color} objects,2–100unique names (1–40characters). Color is a shared
curtain-palette key or literal #RRGGBB. Both dropdown swatches and main buttons
use the same resolver; Save applies atomically without motion. Catalog reloads
on settings open; unknown saved names remain available with gray fallback.
The old two-file names/colors readers are retained only as a fallback when the
combined file is missing. Do not overwrite customized options on future updates;
setup.py preserves an existing combined file. Source build/publish ships only the
combined catalog (16defaults). Small updates require3managed files plus this JSON.


RazrPositionHardware composes lazy motor and relay services for Razr II. Position-aware
IRazrHardware.MovePositionAsync routes1/2 to R1/R2 with the existing interlocked
CurtainRelayController, or to the DAC, or both concurrently. The legacy angle-only
method remains the motor adapter contract. Production composition passes the shared
PhotoboothSettings; no outputs are initialized by settings construction/save.


Optional existing-device button replacement is in packaging/raspberry-pi/keycheck.
Run sh install.sh as pi after extracting its standalone ZIP on the device. The
keycheck.service drop-in keeps key_check1.py unchanged and starts key_check_new.py.
BCM24 requests shutdown; BCM25 confirms then sends WM close to CurtantControl
windows matched by /proc executable PID. Requires X11, wmctrl, python3-tk and RPi.GPIO;
GUI session and passwordless shutdown permissions must be verified on Pi. No motor
or relay access. Host tests: python tests/test_keycheck.py (mocked commands/GPIO-free).


Keycheck v5 starts a separate Python/Tk centered startup overlay before launching
CurtantControl; installer ensures python3-tk. Uses wmctrl PID window readiness,
60second visible timeout/error, and an active-helper guard against repeated
start/close during launch. Button GPIO service remains independent of Tk mainloop.


Keycheck v6 replaces legacy PyAutoGUI confirmation with the same shared Tk
camera-icon/dark/peach centered overlay as Starting; Cancel/Escape dismiss,
Stop sends graceful WM close after helper exit0. No PyAutoGUI requirement now.


Keycheck v7 installer routes matching direct desktop autostart entries through
key_check_new.py --start-app (no GPIO ownership), with per-entry backups. New
entries created only if none match. User flock serializes login/button startup
helpers; button skips close while login startup is active. Custom shell launchers
are not migrated automatically. This is desktop login startup, not headless boot.


App composition injects CameraButtonSelectionStore for Razr II UI persistence.
Tests opt out unless injected. Cached selected ID is independent of output context,
while timestamped durable copies in camera-button-selection.json and razr.json
resolve partial-write recovery. Both stores use unique temp files and Flush(true)
before replacement. Hardware validation, settings drafts and completion checks do
not decide whether the button remains selected. Focused matrix and actual file
relaunch tests cover navigation/settings/real-sim/motor-relays/errors/restart.


RazrHardware is DAC-only as of0.8.17: an injectable I2cDevice factory enables
host checks asserting one0x60 device, exact target bytes and zero reads. Production
factory stays Linux-only. CameraPositionStability was removed with its unused
feedback-only checks; current focused tests exercise DAC writes/cancellation/errors
and view-model completion after Wait time without any ADC dependency.


123Click branding is shared via Views/BrandLogo with embedded transparent dark/light
PNGs; theme changes update its displayed image. Keycheck embeds no app assets: its
installer copies123click-logo.png beside the script and installs Pillow ImageTk for
smooth resizing. See BRANDING.md for asset provenance, prompts and usage.


## Complete current-device app + buttons bundle

Run `./scripts/Publish-DeviceFinal.ps1 -Version 0.9.0` using an unused version. This builds/verifies only the
confirmed BusterARM32 target, packages the complete self-contained app/Backgrounds,
keycheck scripts/unit/logo and the shared installer. Root setup detects included
keycheck, validates both manifests, adds GPIO/Tk/wmctrl/Pillow OS dependencies and
runs keycheck install after app staging/shortcut creation. Existing configuration
and per-user state remain; prior app and modified script/launcher files are backed
up. Keycheck can create a fresh systemd unit, or override the existing one. Desktop,
menu and autostart launch through the centered Starting helper. --check is read-only;
--skip-packages assumes all app/button dependencies already exist. From0.9.1, legacy_kiosk.py backs up and disables recognized Chromium kiosk
startup in desktop/LXDE/rc.local and sends SIGTERM only to current-user main kiosk
processes. Other browser windows and Python startup commands are preserved.
See packaging/bundle/FINAL-PI.md.


Standalone pre-update backup/restore: `packaging/raspberry-pi/rollback.py` uses
Python3.7 stdlib on Pi with sudo elevation. See `packaging/raspberry-pi/ROLLBACK.md`.
Snapshots legacy r1, app, JSON state and startup/service files; restore preserves
the replaced installation and requests reboot. It requires a snapshot before update
to recover that earlier version; it does not reconstruct missing historical files.


From final0.9.2, setup invokes rollback.py backup + full archive check before any
apt/group/app/startup changes. `.rollback-target` in the extracted bundle records
the first baseline; retries verify/reuse it and abort if missing. `sh rollback.sh`
restores it, or accepts an explicit archive. Keep both the bundle and backup.


From0.9.3, bundle rollback.sh selects `rollback.py legacy [archive]`, explicitly
returning to the Chromium system rather than an earlier CurtantControl version.
Preflight requires original key_check1.py, model_1/app_model_1.py and an identifiable
cr_*.sh/camrazr-client.sh in snapshot or r1. Stops current app/button/backend first,
saves current files, restores snapshot if supplied, disables app autostart, installs
original keycheck service and managed legacy backend service, enables old kiosk
autostart and launches original shell script as pi with X11 environment. Raw
`rollback.py restore ARCHIVE` keeps its prior snapshot-only semantics.


From0.9.4, integrated install stops existing keycheck/backend/app via rollback.py
backup-install before capturing files; retries use stop-apps after baseline check.
Legacy migration disables managed backend service plus exact old Python startup
entries, preventing simultaneous owners after reboot. File backup still precedes
configuration/app changes. Standalone backup retains its close-app requirement.


From0.9.5 shortcut installation checks/reuses canonical desktop/menu entries and
moves recognized duplicate icons (including .desktop.bak-* artifacts) into
XDG_DATA_HOME/CurtantControl/shortcut-backups. Keycheck shortcut migration also
backs up there, never alongside live Desktop/menu entries. Starting helper entries
are preserved. fix-shortcuts.sh repairs an existing installation without touching
autostart/service modes.


0.10.0: MainViewModel.Pin implements a modal PIN challenge and Developer-only
PIN editing/confirmation. PinDialogView AXAML supplies touch keypad; no platform
keyboard dependency. SwitchPinStore is injected at composition, defaults to
in-memory for model checks, and persists a salted PBKDF2-SHA256 credential in
per-user switch-pin.json in production. Default1234 applies only when the file
is absent; malformed/read failures stay locked. File saves use unique temporary,
Flush(true), atomic rename, user-only permissions on Unix. Authentication applies
to one I/II chooser visit and clears on cancel/mode change. User Settings open
commands are blocked and the gear is hidden. See PHOTOBOOTH_UI.md.


0.10.5 shutdown: MainWindow logs close receipt/progress/completion to per-user
CurtantControl/shutdown.log (bounded64KiB) and stderr, catches cleanup errors and
queues the final Close on the dispatcher after Closing unwinds. MainViewModel
cancels all sources (isolating callback errors), stops relays immediately, awaits
movement then releases outputs in finally. Background preview/camera discovery,
switch/capture/reconnect waits share a3s budget; late faults are observed. Never
force-kills the app or bypasses hardware release with a window timeout. Headless
`--verify-window-close` checks synchronous and stubborn-camera cleanup, including
repeated close and relay release. Exact Pi issue not yet reproduced: provided log
shows keycheck successfully sent one WM close, so this fix targets app teardown.


0.11.0: IAppUpdateService/GitHubAppUpdates is injected only in production; tests
and previews default to NoAppUpdates. MainWindow starts one asynchronous check
alongside camera startup. MainViewModel.Updates exposes a dismissible notice and
Developer modal, never auto-installing. Version/tag and exact public GitHub asset
selection, streamed SHA256/size validation and preparation are separate from UI.
apply-update.py runs under a transient user systemd unit to survive keycheck
restart, validates/extracts before READY, waits for parent exit then invokes the
existing full backup/setup. Cancellation marker and unit stop prevent delayed
installation after cancel. Documented limits, diagnostics and publishing steps:
UPDATES.md. Publisher uses local Git credentials, uploads draft assets and checks
server digests before publishing; never embeds a token in the Pi app.

From 0.11.1, GitHubAppUpdates selects a checksummed ZIP for the exact installed
version when offered. The worker validates the baseline before reporting ready,
then backs up and replaces changed files after the app exits. A baseline mismatch
falls back to the full bundle. `scripts/build-small-update.py` compares two builds
and refuses changes outside the app patch set. See UPDATES.md.

From 0.11.3, the manual installer calls the installed worker to remove verified
package files after success. The worker saves full rollback tools and the snapshot
reference in per-user recovery before cleanup. The startup update service calls
cleanup independently of GitHub and retires only completed jobs. The small-patch
builder allows the manual cleanup hook since it runs after unchanged setup.
