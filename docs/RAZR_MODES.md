# Separate Razr controllers

## Model selection and independent screens

Use **Switch Razr** in User or Developer mode to choose **Razr I**, **Razr II**, or
**Camera RAZR 3**. User mode prompts for the switch PIN. The selected controller
persists for the next launch. Use **Modes** in Developer or hold the bottom-right
corner for three seconds in User mode to change modes. Model switching is disabled
during movement.

- **Razr I** has its own five-background curtain screen and curtain settings.
  The gear opens **Razr I · Curtain setup**: fixed-background/four-motor colors
  and Developer relay layer order. START only applies curtain relay commands.
  Existing photobooth.json relay pins, polarity and pulse timing remain unchanged.
- **Razr II** has a separate two-position screen and camera settings. Black Hole
  and Miller initially use 45/135 degrees. Tap stages; START moves the camera and
  waits for settling. The gear opens **Camera Razr II · Settings** in either mode: two choice names,
  two angles and wait time. Developer has a collapsed **Advanced motor setup**
  section for simulation, wiring/travel confirmation, angle limits.
  Calibration voltage, full-scale, stability threshold and I2C address values
  remain internal/persisted and are no longer exposed in the settings UI. User saves
  only the five basic fields; hardware settings are preserved.
  There are no curtain/color/layer or relay-check controls on that screen.
- Both headers display only CAMERA RAZR I/II, without Auto or capture-device names.
  Both retain live preview and the User/Developer distinction. Choosing a model
  does not choose a different physical capture device. The corner gesture works
  on either User screen.
- **Camera RAZR 3** uses the angle motor and four curtain pairs in sequence.
  **Open landscape** turns the shared camera and screen assembly toward Outside
  and leaves the curtains in place. The five color buttons all face Room 1.
  Select a scene, then press **Start** in either mode. The preview stays live.
  Start finishes at **Ready** after scene preparation. See the setup below.

The AXAML screens are UserPhotoboothView (Razr I), RazrIIView,
CurtainSettingsView (Razr I), and RazrIISettingsView. RazrModelSelectionView is
only the controller chooser. Camera and curtain START paths are guarded by model.
Razr II Motor mode startup does not create the curtain GPIO service, even when legacy curtain
configuration is invalid. Relay modes validate the shared relay installation settings and create their own outputs lazily on STATE. Switching to II releases existing curtain outputs;
switching back creates the curtain service at idle with inactive outputs. Switching
releases motor device handles but does not reset the retained DAC target.

Camera settings use draft copies: Back discards edits, failed Save retains the
applied settings, and successful Save stays on that page without movement. Camera
validation does not block Razr I; switching to II validates the saved camera data.

## Camera motor

The motor service is behind `IRazrHardware`; development uses simulation. Real
Linux operation uses the existing System.Device.Gpio package's I2C API:

- I2C bus1, MCP4725 at0x60 (decimal96). No ADS1115 device is opened or read.
- MCP4725 volatile fast-mode DAC writes; no EEPROM writes and no automatic homing.
- Desired DAC fraction = requested angle / full-scale angle. Expected voltage is
  that fraction times the measured DAC reference voltage. This is analog position
  control, not a direct GPIO PWM servo signal.
- STATE sends the target and waits only the configured Wait time, then persists
  completion/selection. Command sent means the write/wait completed, not measured
  angle arrival. No feedback-range checks, stability detector or movement timeout.
- Cancellation and actual I2C write/persistence errors still leave command outcome
  incomplete/retryable. Selected button remains highlighted independently.

Confirm measured calibration, mechanical travel limits, ADC/DAC wiring, voltage
compatibility and controller behavior before clearing Simulate motor. The legacy
Python numbers are only initial draft values, not measured acceptance results.
GPIO2/3 are the I2C SDA/SCL lines and must not be assigned to other outputs. Do not connect the shutdown button to GPIO3 while using
this motor bus. Its existing GPIO24 wiring cannot wake a halted Pi.

**Cancellation and application exit do not remove the DAC target or cut
motor power.** No electrical stop protocol is known; stopping software observation
does not guarantee physical stopping. Provide a hardware stop where required.

Stop the legacy `app_model_1.py` controller before using real C# outputs: it writes
the same GPIOs and I2C devices. Its startup configures all eight legacy pins as
outputs. Its camera HTTP feed is hosted in that same process, so use the C# USB/CSI
camera fallback or a separate camera-only process when retiring the Python owner.
The existing automatic HTTP camera activation calls only `/camera_toggle`, not
the Python model-selection endpoint; it does not configure legacy hardware mode.

Protocol references: [MCP4725 manufacturer datasheet](https://ww1.microchip.com/downloads/aemDocuments/documents/APID/ProductDocuments/DataSheets/MCP4725-12-Bit-Digital-to-Analog-Converter-with-EEPROM-Memory-DS20002039.pdf),
[ADS1115 manufacturer datasheet](https://www.ti.com/lit/ds/symlink/ads1115.pdf).

## Persistence and verification

Razr I curtain colors and the shared layer order use `colors.json`. Deployment
relay settings use `photobooth.json`. Razr II camera settings and the selected model use per-user
razr.json: %LOCALAPPDATA%/CurtantControl/razr.json on Windows, normally
~/.local/share/CurtantControl/razr.json on Linux. Camera RAZR 3 uses `razr3.json`
in the same directory for its independent colors, motor settings and selection. Missing files
default to Razr I with simulated camera motor settings. Test/design composition
uses an in-memory store; application composition injects the persistent store.

Run the normal relay checks for all three model paths, independent settings,
failed-save/cancel isolation, model switching, old light-field migration,
Razr II startup without curtain GPIO, camera retry and disposal cancellation.
Render helper options: --render-razr2, --render-razr-setup, --render-razr-motor,
and existing --render-user / --render-user-settings for Razr I.

Host tests and cross-publishing do not establish physical Raspberry Pi, motor,
I2C electrical or Buster runtime acceptance. No installer bundle is regenerated
by these source changes.

## Camera RAZR 3 setup and acceptance

1. In **Developer → Switch Razr**, select **Camera RAZR 3**. The default profile
   simulates both the motor and curtains, including installations with GPIO enabled
   for Razr I. Try **Outside → Green → Purple → White**, pressing **Start** after
   each selection. The activity log shows the rotation, wait and backdrop steps.
   Changing colors while the current session already has a completed Room 1
   motor command skips the rotation and wait. Opening a new session or switching
   models requires reestablishing the direction on the next Start.
2. In **Settings**, check the five backdrop assignments against the physical booth.
   The defaults are White for the fixed backdrop, Sky Blue for M1, Green for M2,
   Purple for M3 and Gray for M4. Settings edits these five assignments independently
   of Razr I. The relay pairs, pulse settings and layer order come from the existing
   installation. The layer order is editable in Razr I Developer Settings.
3. For hardware operation, set `SimulateRelays` to `false` in `photobooth.json`
   using the verified polarity and eight BCM pins. Reserve GPIO2/3 for I²C and
   stop the legacy Python hardware controller before running the C# outputs.
   Measure both target angles, verify the mechanical limits and full-scale calibration,
   and measure a settling time that covers travel in both directions. Enter those
   values in RAZR 3 Settings and confirm the wiring, travel and timing before
   clearing **Simulate motor and curtains** and saving. Unset angles use 45° and 135°
   in simulation. Hardware mode requires two explicit measured angles and a positive wait.
4. On the device, verify that the camera and screen move together, Outside leaves
   the curtains in place, and an indoor curtain begins after the assembly has
   settled. **Ready** means the command sequence finished. The motor has no angle
   feedback, and the relays report timed commands. Developer **Stop** cancels
   the remaining steps and releases active curtain relays. The DAC retains its target,
   so stopping the software leaves motor power under the hardware controller.

## Last Razr II button

Each tap saves one shared selected button ID independently of motor-command snapshots.
Production composition also writes camera-button-selection.json in the same per-user
data folder; razr.json retains a copy. Both writes flush to disk before atomic rename.
Timestamped copies resolve the latest choice if either destination misses a write.
Selection is shared across real/simulated/output modes; command completion remains
separate for real and simulated operation. The highlight survives
Back/Save settings, errors, mode navigation and restarts without moving the motor.
Selection is not physical position feedback. Unknown/failed/unexecuted choices
remain available for retry via STATE. Completed motor snapshots still record the
commanded angle/full-scale mapping and are invalidated if those settings change;
this invalidates completion, not the independently selected button.

Pending command state is persisted before motor access; write failure blocks the
command. Restoring a failed command never labels it completed. Older files without
selected IDs fall back to a matching completed snapshot; the next tap saves an
explicit selected ID. Razr II button label is STATE; Razr I remains START.

Razr II repeats now follow Razr I selection semantics: choosing a position whose
completed command snapshot still matches its ID/angle/full-scale leaves STATE
disabled. Browsing another choice then returning does not resend that command.
After actually applying a different position, the original choice can be applied
again. Failed/unknown outcomes stay retryable; settings Back/Save retains highlight.
Saved selection is still last-command history, not physical position sensing.

## Razr II motor / relay selection

Developer > Razr II settings > Advanced control setup has a Control mode dropdown:
Motor (default for existing installations), Relay 1 / Relay 2, or Motor + Relay
(parallel). User settings preserve this Developer-only setting. Save/Back/startup
never activate hardware. There is no automatic failure switchover.

Position 1 pulses channel R1; position 2 pulses R2. The existing photobooth.json
BCM mapping, ActiveLow, SimulateRelays, ActivationMilliseconds (default500ms),
MaximumActivationMilliseconds and BreakBeforeMakeMilliseconds are reused.
Both contacts are off before a pulse, and both are released after it, on cancellation
or fault. This is a timed trigger, not confirmed actuator travel or position.
R3–R8 are never energized by Razr II; output initialization/disposal sets them off.
Do not wire these same R1/R2 contacts to independent mechanisms that must remain isolated.

Relay-only does not initialize or validate motor hardware/calibration. Parallel
starts both operations, waits for both, then applies the existing wait time once.
A failure cancels the other software operation and attempts relay-off; cancelling
motor observation still does not remove its DAC target or stop it electrically.
Real relay GPIO2/3 assignments are rejected to preserve the motor I2C bus.
The motor simulation toggle and photobooth.json relay simulation remain independent;
the Developer page displays the relay simulation status and pulse duration.

Saved completion includes the output-mode/relay installation profile. Changing
mode or relay pulse/wiring invalidates completion so STATE can apply the new
outputs. Before Razr II uses R1/R2, Razr I's M1 command estimate is marked unknown
and persisted, preventing stale curtain skip decisions after switching back.


Razr I selected background is also saved independently in per-user razr.json,
separately for relay simulation and real hardware. Restart restores the selected
button even if START was never pressed or the last command failed. It does not
assert physical position or activate relays on startup. Razr II Settings Save keeps
the selected button even when output mode or simulation changes; completion validity
remains separate, and changed output profiles can require another STATE.


Razr II selection audit (0.8.16): startup resolves the latest independent/config
selection before hardware validation. Invalid hardware settings use safe simulated
motor defaults while keeping the chosen controller/button. Legacy selection and
snapshot IDs migrate even if the old command failed or its angle no longer matches.
Settings drafts cannot overwrite current selection or command history. Back from
the model chooser restores the pending STATE/retry state. UI selection remains in
memory after write failure; if neither persistence destination succeeds, the label
reports that it could not save. Application folder updates must preserve the per-user
JSON files; installing a keycheck ZIP alone does not update these app changes.


0.8.17 removes the former filtered-position completion per user request. Legacy
FeedbackMinVolts/FeedbackMaxVolts/AngleTolerance/MoveTimeoutSeconds/AdcAddress
values remain readable in razr.json for compatibility but do not validate or affect
motor commands. The unused movement-timeout field is removed from Developer UI.
Motor angle limits, wiring confirmation, DAC address/bus and real write failures
remain enforced. Motor, relay-only and parallel modes retain selection persistence.
