# RayNeo HUD

Managed .NET client for the RayNeo Air 4 Pro AR glasses (VID `0x1BBB`,
PID `0xAF50`) plus a WPF heads-up-display overlay with a hands-free,
tool-using Claude voice assistant. The device layer talks to the vendor HID
interface, decodes the IMU stream, and produces head orientation via a
complementary filter; the HUD renders screen-fixed and world-anchored
elements on the glasses' display; the voice layer runs a push-to-talk loop
through Claude with tool use (timers, world-anchored note pins, app
launching, session control).

Author: Kurt Mitchell.

## Layout

```
src/RayNeo.Device/         Class library — client, filter, wire parser
src/RayNeo.Console/        Console app — live demo and calibration tool
src/RayNeo.Hud/            WPF overlay — HUD compositor, Windows speech
                           engines, push-to-talk hook, HUD tools
src/RayNeo.Voice/          Class library — voice loop controller, state
                           machine, Claude client, tool-use layer
tests/RayNeo.Device.Tests/ xUnit tests (golden decode vector + filter maths)
tests/RayNeo.Voice.Tests/  xUnit tests (state machine, controller, tool loop,
                           timers, tools, history)
```

## Prerequisites

- Windows 11 (HidSharp handles the raw HID interface; the HUD is WPF).
- .NET SDK 10.0 (LTS). Check with `dotnet --list-sdks`.
- RayNeo Air 4 Pro glasses connected via USB-C for the live demo and
  calibration — everything else builds and tests without the device.
- For voice: the `ANTHROPIC_API_KEY` environment variable (the key is read
  from the environment only — never from a file or the command line), a
  microphone, and the Windows speech components (present by default).
  Without the key the HUD runs with voice disabled and says so on-glass.

## Build

```
dotnet build RayNeoHud.slnx
```

## Test

```
dotnet test RayNeoHud.slnx
```

The tests cover a real captured 64-byte IMU frame (the golden vector), the
command-frame builder, the sample dedupe key, both convergence paths of
`HeadOrientationFilter`, and the full voice stack: every legal and illegal
state-machine transition, the controller's happy path / barge-in / fault /
mute branches, the model↔tool orchestration loop against a scripted
transport, timers under a fake clock, and each built-in tool.

## Run

Console live orientation readout (default):

```
dotnet run --project src/RayNeo.Console -- run
```

Console calibration walkthrough (tick rate + nod / shake / roll axis test):

```
dotnet run --project src/RayNeo.Console -- calibrate
```

HUD overlay with voice assistant:

```
dotnet run --project src/RayNeo.Hud
```

The overlay auto-targets the glasses' 1920x1080 display (`--display N`
overrides). **Hold F8** anywhere in Windows to talk — the HUD window never
takes focus — and release to send. The reply streams onto the glass and is
read aloud; press F8 again mid-reply to barge in. `--ptt F13` (or any
`F1`–`F24`, or a hex virtual-key code like `--ptt 0x77`) changes the key.

Things to say: "start a five minute tea timer", "pin buy milk to my left",
"what timers are running?", "open notepad", "mute yourself", "clear the
conversation".

If the glasses are not connected the console app exits with a clear message
and the HUD falls back to simulated head motion.

## Protocol

The wire format is documented in the header comment of
[`src/RayNeo.Device/RayNeoClient.cs`](src/RayNeo.Device/RayNeoClient.cs).
Summary:

- **Host → Device** (64-byte output report, report ID 0):
  `[0]=0x66  [1]=command  [2]=value  [3..54]=payload (zero padded)`
- **Device → Host** (64-byte input report, report ID 0):
  `[0]=0x99  [1]=frame type`
  - `0x65` — IMU sample: little-endian floats for accel, gyro, temp, mag,
    a `uint32` tick @ offset 40, plus proximity and ambient light.
  - `0xC8` — command ack: `uint32` tick @ 4, acked command ID @ 8.

`RayNeoFrameParser` (internal to `RayNeo.Device`, exposed to the test
project via `InternalsVisibleTo`) is the single source of truth for wire
decoding.

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — components, data flow,
  protocol, filter design, voice loop, tool-use layer, threading, and
  testing strategy.
- [`docs/todo.md`](docs/todo.md) — completed vs. pending work.
