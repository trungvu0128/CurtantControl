# CurtantControl

Source repository: [trungvu0128/CurtantControl](https://github.com/trungvu0128/CurtantControl).
Generated releases and runtime files under `artifacts/` are excluded from Git;
publish installer archives as release assets rather than committing them.

The app checks GitHub Releases in the background at startup. Developer mode →
Updates downloads a compatible small patch or the full Buster ARM32 bundle,
verifies it and backs up installed files. See [update setup and publishing](docs/UPDATES.md).

C# desktop starter using Avalonia 12.1.5, .NET 10, and CommunityToolkit.Mvvm.
The same source builds for Windows and Raspberry Pi 4.

The application selects five photobooth backgrounds: a configurable fixed background (White by default) plus four
motorized curtains using eight relays (one open and one close relay per curtain).
Selecting White closes all four curtains; there is no separate Close all button.

## Project and agent documentation

- [Agent instructions](AGENTS.md): repository-wide guidance for coding agents.
- [Development guide](docs/DEVELOPMENT.md): architecture, commands, and validation.
- [Agent memory](docs/AGENT_MEMORY.md): durable decisions, verified state, and pending work.
- [Relay control](docs/RELAY_CONTROL.md): command wrapper, simulation, and GPIO configuration.
- [Photobooth screen](docs/PHOTOBOOTH_UI.md): curtain selection, USB webcam preview, and settings.
- [Releases](docs/RELEASES.md): versioned Raspberry Pi package and release workflow.

## Develop on Windows

Install the .NET 10 SDK, then run from this directory:

```powershell
dotnet restore
dotnet build
dotnet run --project src/CurtantControl
```

Open `CurtantControl.slnx` in an IDE that supports .NET 10. The SDK selection in
`global.json` avoids using the installed .NET 11 preview; it accepts newer .NET 10
SDK feature bands. The UI highlights the chosen color and runs its curtain sequence.

## Publish for Raspberry Pi 4

The user's existing Raspbian 10 Buster OS needs the experimental .NET 8 legacy
candidate; the modern .NET 10 builds below cannot load on it. See
[release details](docs/RELEASES.md) and the Buster package's README before testing.

Check `getconf LONG_BIT` on the Pi to determine OS bitness, not just `uname -m`.
The user's device reports 32-bit despite an aarch64 kernel; use `RaspberryPi32`
for that device. Use `RaspberryPi64` only for a 64-bit OS. Both need a graphical desktop.
Raspberry Pi OS Lite needs additional display/backend
configuration and is outside this desktop starter.

From the development computer:

```powershell
dotnet publish src/CurtantControl -p:PublishProfile=RaspberryPi64
# For the user's confirmed 32-bit OS:
dotnet publish src/CurtantControl -p:PublishProfile=RaspberryPi32
```

Output: `artifacts/raspberry-pi/linux-arm64/` or `artifacts/raspberry-pi/linux-arm/`.
Copy the **entire matching folder** to the Pi,
for example (replace `YOUR_USER` and `PI_HOST`):

```powershell
scp -r artifacts/raspberry-pi/linux-arm64 YOUR_USER@PI_HOST:~/CurtantControl
```

On the Pi, install the Avalonia desktop libraries:

```bash
sudo apt update
sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1
cd ~/CurtantControl
chmod +x CurtantControl
./CurtantControl
```

Launch from a terminal inside the Pi's graphical desktop session. This desktop
backend requires X11 or XWayland; enable XWayland or select an X11 desktop session
if necessary. An SSH-only shell does not provide a graphical display by default.

The publish includes the .NET runtime, so the Pi does not need a .NET SDK or
runtime installation. Native OS dependencies still apply; use a current 64-bit
Raspberry Pi OS with the .NET 10 native runtime dependencies installed.
Do not enable trimming without checking Avalonia and view discovery compatibility.

## Project layout

- `Views/MainWindow.axaml`: desktop layout.
- `ViewModels/MainViewModel.cs`: observable state and commands.
- `App.axaml`: theme; `App.axaml.cs`: window startup.
- `Properties/PublishProfiles/RaspberryPi64.pubxml`: self-contained ARM64 deployment.

The relay command wrapper supports timed activation, interlocking, stop, and
cancellation through simulated or GPIO outputs. The UI defaults to simulated relays.
Configure `photobooth.json` with verified GPIO pins, polarity, layer order, and
timing before enabling hardware. USB webcam preview uses an installed FFmpeg
executable. Automatic camera mode tries the existing HTTP feed, USB/V4L2, then legacy CSI, with failure recovery; see the photobooth screen guide.

References: [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms),
[Linux deployment](https://docs.avaloniaui.net/docs/deployment/linux),
[.NET on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux).


## One-command device setup

Use the generated CurtantControl-Setup bundle for a new Raspberry Pi 4 Desktop.
Extract it as your normal desktop user and run `sh install.sh` in that folder.
The online installer chooses Buster 32-bit or Bookworm/Trixie 32/64-bit, installs
OS dependencies, checks native libraries and creates a desktop/menu shortcut.
New bundles use the explicit device profile in packaging/bundle/photobooth.default.json; updates preserve existing
settings and retain the previous app folder. No .NET SDK/runtime install is needed.
See [bundle instructions](packaging/bundle/README.md) for prerequisites, diagnostics,
updates and current camera/controller limitations.

Curtain colors are configurable in colors.json beside the executable. Add palette entries there, reopen, then Settings > existing four dropdowns > Save. Exactly four physical curtains remain; appearance choices do not change relay mapping. See docs/PHOTOBOOTH_UI.md.

Last curtain command states are saved per user and restored as '(saved)' estimates at startup. They do not measure physical curtain positions. See docs/PHOTOBOOTH_UI.md for file locations and interrupted-command handling.

The app starts in User mode (select a color, then START). Hold the bottom-right corner for three seconds while idle to open the mode chooser. Developer mode offers preview/START controls, logs and settings, with a visible Modes button. Camera starts with the window. This gesture is not an access-control feature.


## Color sample previews

Choose from 18 background colors/themes in the four existing Settings dropdowns.
User mode shows a matching sample portrait for two seconds after selection;
START applies the curtain selection. Keep `Backgrounds/` and `colors.defaults.json`
beside the executable. Existing colors.json assignments are preserved. See
[preview assets](docs/BACKGROUND_PREVIEWS.md).


The application starts full screen in User mode. Hold the bottom-right corner for three seconds while idle to choose a mode.


Settings also configures Background 1, the fixed background. Its name, button color and sample photo follow the selected palette choice; selecting it still closes all four motorized curtains.


Background changes skip individual motors already in the requested completed command state. Saved or unknown states are re-commanded; this is not physical position sensing.


Developer Settings can choose whether R1/R2 or R7/R8 is nearest to the camera. Save applies immediately without movement and persists across restarts; User mode uses the same saved order but hides this control.


RAZR I shows the fixed background first, followed by four color slots. In Developer Settings, selecting a relay-layer order also assigns the relay pairs to those slots. R7/R8 nearest uses pairs R1/R2 through R7/R8. R1/R2 nearest uses the reverse assignments, with R1/R2 on the last color. Save applies both settings for the next START.


The complete Pi Buster ARM32 bundle includes the app and keycheck. Extract it on
the Pi and run `sh install.sh` as the desktop user. The installer adds OS dependencies
and carries forward existing hardware, color, and layer settings. [Latest GitHub release](https://github.com/trungvu0128/CurtantControl/releases/latest)

## Separate Razr controllers

Switch Razr selects Razr I (curtain relays), Razr II (camera angles), or Camera RAZR 3
(camera rotation and five indoor backdrops). User mode requires the switch PIN.
Camera RAZR 3 starts in simulation and uses a separate Developer settings profile.
Modes is visible only in Developer; User opens the mode chooser by holding the
bottom-right corner for three seconds. Razr II settings show only
two choice names, two angles and wait time; advanced motor setup is collapsed in
Developer mode. The camera motor defaults to simulation. See [Razr setup](docs/RAZR_MODES.md).
