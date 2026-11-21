# Appearance (0.10.1)

Dark/Light controls are removed from both Razr headers in User and Developer
modes. The app always starts in Dark mode, ignoring an older saved light preference.
The layout closes the removed control gap; other header controls keep their size.

# Current access controls (0.10.0)

User mode hides the Settings gear for both Camera Razr I and II. Switch Razr
opens a masked numeric PIN dialog with touch buttons 0–9, delete, OK and Cancel.
A correct PIN opens the existing I/II chooser, retaining User mode. Each new
Switch Razr request needs authentication again; Cancel or wrong PIN never changes
the selected model or drives hardware. The modal blocks underlying scene actions.

In Developer mode, use **Switch PIN** in the header to set a 4–8 digit PIN, then
enter it again to confirm. Initial default is **1234**. Leading zeroes are kept.
PIN changes do not move hardware. The per-user `CurtantControl/switch-pin.json`
contains a random salt and PBKDF2-SHA256 hash, saved atomically; updates preserve
it outside the app folder and the full bundle backup includes it. Read/corruption
errors refuse access instead of falling back to the default. Existing Developer
entry via mode chooser/three-second corner hold is unchanged; this PIN gates the
Razr controller switch, not OS access or entry to Developer mode.

The historical notes below describe earlier versions; the rules above supersede
older statements offering Settings directly in User mode.

# Photobooth screen

The screen follows the user's two-column reference: large curtain color buttons
on the left, a live camera preview on the right, and a highlighted selected color.
The original balanced two-column layout and scaled 1200x800 canvas are retained. Video fills the full rounded preview box proportionally, including a 16:9 center crop in the decoder. Edges may crop without distorting the image. The window opens normally; maximizing is optional.
Four curtains use Pink, Sky Blue, Green, and Purple. The gray fifth control closes
all four curtains. Stop cancels movement and attempts to turn all relays off.
No photo capture or START workflow is included; the requested camera function is
live preview only.

## Curtain selection

`BackgroundController` closes all curtains in front of the selected layer in
parallel, waits for those timed commands to finish, then opens the selected
curtain. Curtains behind it remain unchanged. The default front-to-back order is
Pink (1), Sky Blue (2), Green (3), Purple (4); the user allowed an arbitrary initial
order. Match configuration to the actual installation before using hardware.

For example, selecting Green closes Pink and Sky Blue first, then opens Green.
Purple is unchanged. Selecting Pink opens Pink without moving any layer behind it.
The highlight represents the requested selection, not verified physical position.
Color and Close all controls are disabled during a sequence. Stop remains available.

## Temporary status log

The bottom panel shows each curtain's last command state: Closed (off) at startup
in simulation, or Unknown at startup with real hardware,
Opening/Closing while a command is running, and Opened/Closed after successful
completion. Cancelled or failed commands return the affected curtain to Unknown.
These are commanded states, not sensor-confirmed positions.

The activity log lists the latest 100 timestamped entries, newest first. Simulation
entries include `[SIM]`. Clear removes the log without changing curtain status.
The log and statuses are held in memory only and reset on application restart.

Startup initializes relay outputs OFF, then reads all eight control signals into
the activity log. Check relays takes another snapshot without changing outputs.
Each entry includes relay channel, curtain, open/close direction, and ON/OFF/UNKNOWN.
Read failures are reported per channel and do not prevent reading the others.
These readings are GPIO control levels (or simulated states), not relay-contact
feedback. They describe the state after startup initialization, not the state
before the application started. An OFF relay does not mean a closed curtain.

## Configuration

Use the gear icon to open setup. The existing four front-to-back dropdowns
choose from the full colors.json palette. Each position keeps its physical curtain
ID and relay pair. Choosing an already-assigned color is rejected without swapping
or changing another position. Save persists the four colors, stays on setup and
shows Saved. Back/Cancel discards unsaved edits. Saving never moves relays.

Existing layer order still loads from CurtainOrderFrontToBack and the per-user
curtain-order.json override. The dropdowns now change appearance only; they do not
reorder physical curtains or rewrite the order file. No extra right-side controls.

Edit `src/CurtantControl/photobooth.json` for development. It is copied beside the
executable on build/publish; deployed devices load the adjacent copy. Local device
credentials and secrets should never be added to this file.

| Setting | Meaning |
| --- | --- |
| `SimulateRelays` | Defaults to true; commands run in memory without GPIO |
| `BcmPinsByChannel` | Eight distinct verified BCM pins in relay channel order |
| `ActiveLow` | Must explicitly be true or false before GPIO is enabled |
| `CurtainOrderFrontToBack` | Each curtain ID 1..4 exactly once, nearest camera first |
| `ActivationMilliseconds` | Duration for every open/close relay command |
| `MaximumActivationMilliseconds` | Maximum permitted activation |
| `BreakBeforeMakeMilliseconds` | Positive off interval before energizing a direction |
| `FfmpegPath` | FFmpeg executable name or absolute path |
| `CameraDevice` | Linux device path or Windows DirectShow camera name |
| `CameraStreamUrl` | Optional HTTP/MJPEG URL; selects stream mode and skips direct hardware discovery |

The shipped timing values are simulation examples. Configure timings and verify
pulse/maintained control against the actuator before setting `SimulateRelays` to
false. See `RELAY_CONTROL.md` for electrical and GPIO constraints.

Invalid GPIO setup disables curtain selection with an error message. Invalid JSON
falls back to simulation with an explicit configuration error message.

## USB webcam on Raspberry Pi

The camera adapter uses FFmpeg's V4L2 capture on Linux. Install FFmpeg on the Pi:

```bash
sudo apt update
sudo apt install -y ffmpeg v4l-utils
v4l2-ctl --list-devices
```

The dropdown above the preview lists detected capture devices. Select one to
switch the live preview; Refresh rescans after plugging in or removing a webcam.
The application user needs permission to access the device. Linux discovery uses
`/sys/class/video4linux` and `v4l2-ctl --all`, filtering out metadata-only nodes.
Direct V4L2 capture is for USB webcams. Buster legacy ribbon-camera capture is
also available through raspivid, as described below.

`CameraDevice` is an optional startup preference (for example `/dev/video0`). If it is unavailable or unset, Automatic is selected. Dropdown changes
apply for the current session and do not rewrite the deployment settings. Refresh
keeps the selected camera when still present; otherwise it selects an available
one, or returns to Automatic for recovery. One camera streams at a time;
switching cancels and awaits the previous stream before opening the new one.

The app connects when the window opens. It displays 640 × 360 BGRA frames at up to
15 fps, preserving proportions using center cropping to fill the preview. Frames stay local;
there is no recording, uploading, or audio capture. Actual camera format support
and Pi performance need verification on the target device. FFmpeg remains a native
system prerequisite even though the .NET app is self-contained.

If FFmpeg is missing, access is denied, the camera disconnects, or no frame arrives
for ten seconds, the preview shows an offline state and error. Reconnect camera
retries capture. Window shutdown cancels capture and terminates the FFmpeg child.

## Legacy ribbon / CSI camera on Buster

### Reuse an existing camera service

The user's existing `/home/pi/r1/model_1/app_model_1.py` provides Flask
`/video_feed` on port 5000, with multipart JPEG output. Set `CameraStreamUrl` to
`http://127.0.0.1:5000/video_feed` to reuse that service. Automatic tries that feed before direct cameras; the dropdown also includes manual choices. The .NET HTTP reader extracts
complete JPEG frames without trusting multipart boundary formatting and sends
them to FFmpeg's raw MJPEG stdin decoder. JPEG segment lengths and entropy escaping
are respected, frame size is bounded, and HTTP HTML/JSON errors are rejected.
HTTP headers and first-frame/no-frame waits are bounded to ten seconds.
While the HTTP feed works, no V4L2 device or raspivid capture is started. When the known local feed returns 204, EnablePythonCameraActivation (default true) permits POST /camera_control with mode=1, then retries the feed once only if the service returns type=camera. It never invokes GPIO or other control routes.

Keep the Python service running, and initially close the React/browser preview
while testing the Avalonia consumer. The handler's support for multiple concurrent
stream clients has not been verified. Clear/null CameraStreamUrl to use the known local feed in Linux Automatic mode. Direct camera choices remain available. Live endpoint/decoder behavior still needs testing on the Pi.

### Direct legacy capture

If `raspivid` is installed and `vcgencmd get_camera` reports supported=1 detected=1,
discovery adds Raspberry Pi ribbon camera (legacy CSI), device ID `raspivid://0`.
Select it instead of raw video nodes. This captures H264 with raspivid and pipes
it into FFmpeg; both processes are stopped/awaited on cancellation and shutdown.
No shell pipeline is executed. Preview is still local and is not recorded.

The modern libcamera/rpicam camera stack is not implemented. Broadcom codec and
ISP processing nodes are excluded from the dropdown; the direct V4L2 backend only
accepts single-planar capture flags, not multi-planar or memory-to-memory pipelines.
Camera connection/failure details and relay setup failures are recorded in the
temporary activity log for device troubleshooting.

## Windows development

Install FFmpeg separately and ensure it is on PATH, or set `FfmpegPath` to its
executable. List DirectShow devices:

```powershell
ffmpeg -list_devices true -f dshow -i dummy
```

Windows discovery uses the DirectShow list automatically, including unique
alternative device IDs when available. Select a camera from the dropdown, or set
`CameraDevice` to the webcam name/ID as a startup preference (without `video=`).
No detected cameras or discovery failures display an offline message.

## Verification and preview artifact

```powershell
dotnet run --project tests/CurtantControl.RelayChecks
dotnet run --project tests/CurtantControl.RelayChecks -- --render artifacts/photobooth-ui.png
dotnet run --project tests/CurtantControl.RelayChecks -- --render-settings artifacts/curtain-settings-ui.png
```

The renderer uses Avalonia.Headless with Skia to render the actual AXAML, showing
an example selection and sample camera choices with no live frames. Its dependency is confined to
the verification project and is not included in the desktop app publish. The
behavior checks cover relay interlocking, background sequencing, cancellation,
selection highlighting state, and Close all channel mapping.

Reference: [FFmpeg capture device documentation](https://www.ffmpeg.org/ffmpeg-devices.html),
[Avalonia headless rendering](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform).



## Automatic source selection and recovery

The dropdown defaults to Automatic unless CameraDevice selects a specific source.
Automatic tries CameraStreamUrl first (on Linux, the known local
http://127.0.0.1:5000/video_feed when unset), then discovered direct V4L2/USB
cameras, then legacy CSI/raspivid. Discovery does not open cameras. A working
source remains selected until it fails; no background source switching interrupts it.

Connection errors, missing tools, invalid HTTP/JPEG, permission failures, occupied
cameras, EOF, disconnects, and ten-second frame stalls move to the next candidate.
Each failed capture finishes cleanup before another starts. After exhausting the
list, wait five seconds and rescan, allowing a later service start or USB insertion.
Manual source selection does not silently substitute another camera; Reconnect
retries that source. Select Automatic to enable fallback. Refresh rescans choices.

Linux /proc file-descriptor checks can report another process holding a direct
/dev/video node. They are advisory: permissions and ownership races remain, and
backend errors are authoritative. /dev/vchiq is shared by GPU clients, so it is
not used to infer CSI ownership. Busy legacy CSI is reported by raspivid; the app
never kills other applications. Only the explicitly supported local Python camera activation route may be called. Logs show each
attempt, connection, failure, and retry. The old frame clears after a failure.

Limitations: arbitrary service URLs cannot be discovered automatically; configure
them. Modern libcamera/rpicam, every USB pixel format, driver failures, power/cable
faults, and server multi-client safety are not guaranteed. Hardware discovery
utilities that are absent or time out can omit devices. Install ffmpeg and
v4l-utils (and legacy camera tools on Buster). Automatic retries continue until
you select another source or close the app. Real Pi acceptance testing is required.



Check relays always reports a result: eight GPIO control readings and completion, a read failure, or the original relay setup error if initialization failed. It does not retry initialization or infer physical curtain position.


Python activation is scoped to HTTP localhost/127.0.0.1/[::1]:5000/video_feed
without a query or credentials. Other HTTP URLs never receive activation POSTs.
The service has no independent OS permission-check API; get_camera_mode always
reports 1. The camera_control handler itself checks camera availability, and its
accepted response gates the feed retry. This does not prove OS permissions or
successful frames: the stream still must deliver valid JPEGs. Redirects are not
followed; activation responses are bounded, timeouts/cancellation apply, and a
second 204 produces an explicit disabled-stream error. A working feed is not
re-activated. Set EnablePythonCameraActivation=false to disable this behavior.

## Appearance

Use the Dark/Light switch beside the gear icon. Both screens and Fluent dropdowns update immediately, while curtain colors and video remain unchanged. Preference persists per user in <LocalApplicationData>/CurtantControl/appearance.json; hardware configuration is not rewritten. Default/fallback is dark. Save failures appear in the activity log.

Clicking an already-highlighted background color is a no-op: it does not repeat relay commands or add log entries. Selecting another color or using Close all clears that selection, allowing it to be requested again.

## Extend the color palette (four physical curtains)

Edit colors.json beside the executable. Installed Pi path:
/home/pi/.local/share/CurtantControl/app/colors.json

Colors is an extensible array of objects with unique Key, display Name, and opaque
#RRGGBB Color. Slots contains exactly four color keys in physical curtain ID1..4
order, independent of the saved front-to-back layer order. Example entry to append:

```json
{ "Key": "orange", "Name": "Orange", "Color": "#F2AA7E" }
```

Reopen the app after external edits. Open Settings and use the existing four
dropdowns, then Save. The four main buttons, status swatches,
selection label and selected dot update immediately; relay pairs and layer order
stay fixed. Save stays on setup and does not move relays. Back/Cancel
without Save discards color drafts; already saved colors remain. Save updates colors only. Duplicate assigned color keys are rejected.

If colors.json is absent, original four colors remain available. Invalid palette
content logs a fallback warning and loads original defaults. Explicit Save
can replace that invalid file; correct it first to preserve custom entries. Keep
colors.json during updates; the latest bundle installer preserves it byte-for-byte.
Read/write permissions are needed beside the installed executable for Save.

Close all is a no-op when every curtain's last-command status is Closed. Unknown or other statuses still allow a close-all sequence. This is based on command state, not physical sensors.

Main buttons and curtain-status rows use the same front-to-back display order as Settings. Button01..04 labels are visual levels; physical IDs and relay pairs are unchanged. Saved color updates change the shared curtain objects at the matching level.

## Saved last-command estimates

The application saves last command states per physical curtain ID. The hardware
file is <LocalApplicationData>/CurtantControl/curtain-command-state.json; simulation
uses simulated-curtain-command-state.json, keeping simulated estimates separate.
For pi, hardware path is /home/pi/.local/share/CurtantControl/curtain-command-state.json.

At startup, completed values display Opened (saved) or Closed (saved). Unknown,
pending, failed and canceled commands display Unknown. The app does not move relays. It restores a saved highlight only when front-to-back states identify a background: the first Opened curtain with every curtain before it Closed. Unknown foreground prevents inference; all Closed leaves no color selected. Saved estimates do not trigger same-color/Close
all suppression; the first explicit command after restart remains available.
No file means existing startup defaults; invalid files log a restore error.

A snapshot is written as Unknown before each new pulse, then updated on completion.
Writes use a flushed temporary file and replacement. If recording a pending state
fails, that curtain's new pulse is blocked and the error is logged. Completion-save
failure is logged; the previous pending Unknown snapshot remains conservative.
No data predating this feature is recovered. Hardware limit completion, manual
movement, other GPIO writers and controller-latched motion are not detected.
Save/restore does not establish actual curtain positions. The activity log remains
in-memory; these snapshots persist only command-state estimates and a timestamp.


Restored selection updates highlight, checkmark, selected-color dot and label with (saved). Unknown states behind a known background do not prevent inference. This remains an unverified estimate. Explicitly clicking a restored highlight may reissue its command once; after that, normal repeated-selection suppression applies.

## Startup modes

Every launch shows a mode chooser. Developer mode keeps the existing control
panel: immediate color actions, camera selector/refresh, appearance toggle,
settings, relay checks, curtain command statuses and activity logs. User mode
uses a dark reference-style photobooth view with large glossy color buttons,
a live camera preview, START, Stop and Close all. It hides logs, raw errors,
GPIO readback, settings and camera selection. Entering User mode clears the
previous in-memory log; subsequent diagnostic entries remain internal and are
visible after switching to Developer. This is a presentation mode, not a password
or authorization boundary. Mode choice is not persisted; each launch asks again.

User color taps stage a choice and highlight it without sending pulses. START
applies the staged background through the existing foreground-close/selected-open
sequence. START stays disabled with no pending choice and does not repeat after
success. Failed/canceled operations remain retryable with START. Close all acts
immediately and clears a pending choice; existing all-Closed suppression remains.
Stop retains its existing command-cancellation behavior and does not guarantee
stopping a controller that continues moving after a pulse.

Use Modes in either screen to return to the chooser. Mode changes are blocked
while a relay command runs; abandoning a pending choice sends no pulses. Camera
startup is delayed until the first mode choice; existing capture is reused across
later mode switches. All relay mapping, palettes and saved-estimate behavior are
shared between modes. User colors still follow configured front-to-back order.


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


0.5.3 centers the selection message beneath the left controls, uses consistent52px header control heights, and explicitly centers the START label.


Select a camera in Developer mode; switching to User keeps that same camera and session. The top-left metallic badge displays its name; automatic source is labeled Auto. The heading above the preview retains Live Camera Preview / Camera preview offline, temporarily replaced by the sample caption. Selection sharing applies during the current app session.


0.5.6 uses a compact camera heading and removes the User helper caption below the preview to enlarge the viewport. Settings is the rightmost header control. Developer camera diagnostics remain.


0.5.8: Close all has the same size as the four color buttons. Browsing another color and returning to the currently applied background keeps START disabled, based on completed command states (not sensor feedback). After a different background is applied, START can apply the previous background again.


Default startup is User mode. Hold the bottom-right80x80 corner for three seconds while idle to open the mode chooser. Releasing early/moving outside cancels. No visible Modes button in User; Developer retains its button.


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


The nearest-to-farthest caption is shown only in Developer mode; User retains the same sorted buttons without the caption.


0.7.5 supersedes distance sorting: display follows the paper, fixed background first, then M1,M2,M3,M4. Main and Settings remain aligned. Changing Developer relay layer order changes control sequencing only, not button positions or physical color/relay IDs.
