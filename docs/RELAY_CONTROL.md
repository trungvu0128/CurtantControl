# Relay control wrapper

The photobooth application will select backgrounds by opening and closing four
curtains. Eight logical relay channels are paired as follows:

| Curtain ID | Open channel | Close channel |
| --- | --- | --- |
| 1 | 1 | 2 |
| 2 | 3 | 4 |
| 3 | 5 | 6 |
| 4 | 7 | 8 |

These are software channel IDs, not Raspberry Pi header pin numbers. The physical
GPIO mapping must be supplied separately in channel order. All eight pins must be
distinct BCM GPIO numbers. Confirm the actual wiring before enabling hardware.

## API

`CurtainRelayController` owns an `IRelayOutput` and exposes:

- `OpenAsync(curtain, activation, cancellationToken)`
- `CloseAsync(curtain, activation, cancellationToken)`
- `ExecuteAsync(curtain, direction, activation, cancellationToken)`
- `Stop(curtain)` and `StopAll()`
- `Dispose()` for cancellation, all-off attempts, and GPIO release.

Curtain IDs are 1 through 4. Each command turns both direction relays off, waits
the configured break-before-make interval, energizes the selected channel for the
requested interval, then turns both off in a `finally` block. Cancellation also
turns both off. Commands for different curtains may run concurrently; a second
command for a busy curtain is rejected rather than queued. To reverse, stop the
current command and await its cancellation before starting the opposite command.

The constructor requires a maximum activation duration and a positive
break-before-make interval. Every activation must be positive and within that
maximum. These values must come from the actual actuator/control specification.
Timing is managed by .NET tasks and is not a real-time hardware guarantee.

On an output error the controller latches a fault, cancels active commands, and
rejects new movement commands. Shutdown attempts every applicable off write even
when one fails, and reports errors. `StopAll()` can retry off writes after a fault;
it does not clear the fault. Inspect the hardware and recreate the controller.

The wrapper has no position feedback. Successful completion means a timed relay
command completed, not that a curtain reached its physical open or closed limit.
Whether the curtain controller expects a short trigger pulse or a maintained
direction signal still needs confirmation. `BackgroundController` and the UI now
compose this wrapper for background selection; see `PHOTOBOOTH_UI.md`.

## Simulated development

```csharp
using CurtantControl.Services.Relays;

var output = new SimulatedRelayOutput();
using var relays = new CurtainRelayController(
    output,
    maximumActivation: TimeSpan.FromSeconds(2),
    breakBeforeMake: TimeSpan.FromMilliseconds(100));

await relays.OpenAsync(1, TimeSpan.FromMilliseconds(200));
await Task.WhenAll(
    relays.CloseAsync(1, TimeSpan.FromMilliseconds(200)),
    relays.OpenAsync(2, TimeSpan.FromMilliseconds(200)));
relays.StopAll();
```

The numbers above are demonstration values for simulation, not approved hardware
timing. `SimulatedRelayOutput.IsEnergized(channel)` exposes the simulated output.

## Raspberry Pi GPIO

Use `GpioRelayOutput` instead of the simulated output when composing the controller:

```csharp
// Supply these values from verified wiring and relay-board configuration.
IRelayOutput output = new GpioRelayOutput(bcmPinsByChannel, activeLow);
using var relays = new CurtainRelayController(output, maximumActivation, breakBeforeMake);
await relays.OpenAsync(1, activation, cancellationToken);
```

`bcmPinsByChannel` contains eight BCM numbers in channel order, not physical header
numbers. `activeLow: true` means a low GPIO output energizes a relay; `false` means
high energizes it. The adapter uses System.Device.Gpio 4.2.0, opens outputs with
their inactive initial value, and attempts to drive them inactive on disposal.
Application startup creates GPIO only when `SimulateRelays` is explicitly false
and configuration validates. Do not share
the output instance with other callers after handing it to the controller.

The user supplied [this selectable high/low relay board listing](https://linhkienvietnam.vn/module-relay-8-kenh-24v-10a-chon-muc-kich-hoat-high-low?variantId=73232317).
Its specifications could not be retrieved during implementation. GPIO pins,
jumper setting, 3.3 V input compatibility, isolation/power wiring, and actuator
control semantics remain unverified. Do not connect a board's 24 V supply to Pi
GPIO. Confirm the interface specifications before wiring. Software interlocking
does not replace electrical interlocking or protection against startup, process
crash, power loss, and relay contact faults. Output state is not guaranteed after
a failed write or process termination.

## Automated verification

```powershell
dotnet run --project tests/CurtantControl.RelayChecks
```

This console verification project exits nonzero on a failed assertion. It uses a
recording output with injected failures; no GPIO hardware is accessed. It verifies
channel mapping, startup/off states, timed cutoff, off intervals, conflicting
commands, independent curtains, cancellation, validation, fault latching, and
disposal. Tests do not prove electrical compatibility or physical curtain motion.

API references: [System.Device.Gpio package](https://www.nuget.org/packages/System.Device.Gpio/4.2.0)
and [GPIO output initialization](https://learn.microsoft.com/en-us/dotnet/api/system.device.gpio.gpiocontroller.openpin?view=iot-dotnet-latest).


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


## RAZR I layer order and color assignments

Developer mode > Settings now shows Relay layers, with two nearest-to-farthest
choices: R1/R2→R3/R4→R5/R6→R7/R8, or R7/R8→R5/R6→R3/R4→R1/R2.
The four color rows stay in the same order. Settings previews the pair beside
each color. Save applies the layer order and assignments together while idle.
Both operating modes then use them at START. Open uses the odd relay and Close
uses the even relay for the assigned motor.

| Color button position | R7/R8 nearest | R1/R2 nearest |
| --- | --- | --- |
| 1st motorized color (default Pink) | R1/R2 | R7/R8 |
| 2nd motorized color (default Sky Blue) | R3/R4 | R5/R6 |
| 3rd motorized color (default Green) | R5/R6 | R3/R4 |
| 4th motorized color (default Purple) | R7/R8 | R1/R2 |

`colors.json` persists `RelayOrderFrontToBack` as physical motor IDs 1..4 and
`ReverseRelayAssignments` alongside the ordered `Slots`. Existing files without
`ReverseRelayAssignments` retain their saved assignments. Older R1/R2-nearest
configurations and custom permutations are shown as a current setup until the
operator selects a preset and saves.

Physical motor command states stay with their relay pairs. The highlight is
recalculated after Save. In the regular profile, White closes any motor that is
open or has an unconfirmed state. RAZR 3 shares the preset and stores its
backdrop colors separately.

### Shared paper relay sequence

The R1/R2-nearest preset with reversed assignments runs the same sequence for
each backdrop slot in RAZR I and RAZR 3. The models save their colors separately.
RAZR 3 sends indoor pulses after the camera settles in Room 1. Open landscape
only moves the camera.

| RAZR I default | RAZR 3 default | Relay pulses in order |
| --- | --- | --- |
| White | White | R1 → R3 → R5 → R7 |
| Pink | Sky Blue | R8 → R5 → R3 → R1 |
| Sky Blue | Green | R6 → R3 → R1 |
| Green | Purple | R4 → R1 |
| Purple | Gray | R1 |

Each started sequence sends its complete row. The controller releases each relay
before starting the next and cancels the remaining pulses on Stop or error. The
application records successful sequence completion for the repeat-selection guard.
Touched motor positions stay Unknown in the shared relay history. In particular,
R1 appears in the fixed and fourth motorized slots. A completed R1 pulse does
not establish which backdrop is visible. The regular profile continues using
the state-aware foreground planner. RAZR II uses its camera-position commands.
