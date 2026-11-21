# Agent instructions

## Start here

Read `README.md`, `docs/DEVELOPMENT.md`, and `docs/AGENT_MEMORY.md` before making
changes. Treat source and configuration as authoritative when documentation differs.
These instructions apply to the entire repository.

## Project intent

Build a C# Avalonia desktop application that develops on Windows and runs on a
Raspberry Pi 4. The user's device has a 32-bit OS with a 64-bit kernel (confirmed
by getconf LONG_BIT=32 and uname -m=aarch64); use linux-arm for that device.
The device is Raspbian 10 Buster and cannot run the modern .NET 10 package.
Use the optional LegacyBuster=true / .NET 8 compatibility build for current-device
experiments; it is not a supported Buster runtime configuration or a verified Pi run.
Both 32-bit and 64-bit publish profiles require a graphical desktop. The photobooth uses four curtains to change backgrounds,
with two relay channels per curtain (open/close), eight channels total. Read
`docs/RELAY_CONTROL.md` before modifying relay control. GPIO wiring, polarity,
actuator timing, and trigger semantics still need confirmation.

## Implementation conventions

- Keep UI layout in Avalonia AXAML views and presentation state/commands in
  CommunityToolkit.Mvvm view models. Keep code-behind focused on view behavior.
- Keep platform-specific hardware and OS access behind service interfaces when
  introduced. Do not add Windows-only dependencies to shared application logic.
- Follow existing C# style, file-scoped namespaces, nullable annotations, and
  generated observable properties and relay commands.
- Preserve the stable .NET 10 SDK selection and pinned package versions unless
  the task calls for an upgrade. Do not silently switch to a preview SDK.
- Preserve the self-contained `linux-arm64` publish profile. Trimming is disabled;
  review view discovery and Avalonia compatibility before changing it.
- Never infer hardware protocols, pin assignments, or actuator behavior from
  the project name. Record unresolved requirements explicitly.
- Preserve unrelated user changes. Do not commit generated `bin`, `obj`, or
  `artifacts` output, secrets, credentials, or device connection details.

## Validation and delivery

Run checks appropriate to the change; documentation-only changes need link/path
and diff checks, not an application rebuild. For code changes, build Debug and
Release. Verify the Raspberry Pi publish when changing dependencies, startup,
native integration, or deployment configuration. See `docs/DEVELOPMENT.md`.
Add focused tests for meaningful logic when introduced; do not add tests that
only repeat static layout or trivial starter behavior.

Report what changed, what was verified, and any remaining limitations. A successful
cross-publish is not evidence that the app was tested on a physical Pi.

## Persistent project memory

Update `docs/AGENT_MEMORY.md` when durable decisions, constraints, verification
results, or blockers change. Keep entries concise and dated. Separate confirmed
facts from assumptions and pending work. Replace stale facts instead of accumulating
contradictions. Do not store secrets or a transcript of every action.

Update the README or development guide when commands, architecture, or setup
change. Repository memory is available to future agents that read these files;
it is not a global or automatic memory service.
