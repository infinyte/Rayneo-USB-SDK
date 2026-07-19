# Architecture

RayNeo HUD is a managed .NET client for the RayNeo Air 4 Pro AR glasses
(VID `0x1BBB` / PID `0xAF50`). It opens the vendor HID interface, decodes the
IMU stream, and derives head orientation with a complementary filter.

Author: Kurt Mitchell.

## Solution layout

```
src/RayNeo.Device/         Class library — client, wire parser, IMU sample, filter
src/RayNeo.Console/        Console app — live readout and calibration tool
tests/RayNeo.Device.Tests/ xUnit tests
```

Target framework: .NET 10.0. The only third-party dependency is
[HidSharp](https://www.nuget.org/packages/HidSharp), used for raw HID access.

## Component overview

### `RayNeo.Device` (class library)

| Type | Visibility | Responsibility |
|------|------------|----------------|
| `RayNeoClient` | public | Finds and opens the glasses, runs the background HID read loop, sends commands, raises decoded samples and acks. |
| `RayNeoFrameParser` | internal | Stateless wire decode/encode: parse IMU frames, read acks, build command reports. Single source of truth for the frame layout. |
| `RayNeoImuSample` | public | Immutable decoded IMU sample (`readonly record struct`) plus tick-based `IsNewerThan` dedupe. |
| `HeadOrientationFilter` | public | Complementary filter producing pitch / roll / yaw from samples. |

`RayNeoFrameParser` is `internal` and exposed to the test project through
`InternalsVisibleTo("RayNeo.Device.Tests")` (declared in `RayNeoClient.cs`), so
wire decoding is unit-tested without a device.

### `RayNeo.Console` (demo app)

`Program.cs` provides two subcommands:

- **`run`** (default) — measures the device tick rate over a 2 s hold-still
  window, then streams a live pitch / roll / yaw / temperature readout until a
  key is pressed.
- **`calibrate`** — measures the tick rate over 10 s, then walks the user
  through a nod / shake / roll test, reporting the gyro axis with the highest
  RMS magnitude for each motion so the axis-to-body mapping can be confirmed.

The app exits with code `2` and a human-readable message when the glasses are
not connected.

## Data flow

```
USB HID (HidSharp)
      │  64-byte input reports
      ▼
RayNeoClient.ReadLoop        (background thread)
      │  validates magic 0x99, dispatches by frame type
      ├── 0x65 IMU  → RayNeoFrameParser.ParseImuFrame → SampleReceived event
      └── 0xC8 ack  → RayNeoFrameParser.ReadAckCommandId → CommandAcknowledged event
      ▼
consumer (Console app)
      │  RayNeoImuSample.IsNewerThan drops repeated transport frames
      ▼
HeadOrientationFilter.Update → PitchDegrees / RollDegrees / YawDegrees
```

## Wire protocol

The authoritative description lives in the header comment of
[`src/RayNeo.Device/RayNeoClient.cs`](../src/RayNeo.Device/RayNeoClient.cs) and
must not be altered (see the root `CLAUDE.md`). Summary:

**Host → Device** — 64-byte output report, report ID `0`:

```
[0]=0x66  [1]=command  [2]=value  [3..54]=payload (zero padded)
```

Commands: `0x00` device info, `0x01` IMU on, `0x02` IMU off. HidSharp prepends a
leading `0x00` report-ID byte, so the written buffer is 65 bytes.

**Device → Host** — 64-byte input report, report ID `0`, magic `0x99`:

- **`0x65` IMU sample** — little-endian `float32`:
  accel x/y/z @ 4/8/12, gyro x/y/z @ 16/20/24, temperature @ 28,
  mag x @ 32, mag y @ 36, mag z @ 52, `uint32` tick @ 40,
  proximity @ 44, ambient light @ 48. On the Air 4 Pro, offsets 56–63 carry a
  duplicated `uint32` timestamp.
- **`0xC8` command ack** — `uint32` tick @ 4, acked command ID @ 8.

## Orientation filter

`HeadOrientationFilter` is a complementary filter (`GyroWeight = 0.98`):

- **Pitch / roll** — gyro integration blended toward the accelerometer's
  gravity reference, so they are drift-corrected and converge at rest.
- **Yaw** — gyro-only integration; it drifts over time because magnetometer
  correction is not yet applied.
- **`dt`** — derived from the device tick delta divided by `TickRateHz`. The
  device tick counter runs far faster than 1 kHz, so `TickRateHz` must be
  measured per device (the Console app does this) or yaw integrates far too
  fast.
- Samples whose tick equals the previous tick are ignored (duplicated transport
  frames carry no new sensor data).

## Threading

`RayNeoClient` runs a single background reader thread (`IsBackground = true`).
`SampleReceived` and `CommandAcknowledged` are raised on that thread, so
consumers must not block in their handlers. `Dispose` stops the loop, attempts
to disable the IMU, closes the stream, and joins the thread (500 ms timeout).

## Testing strategy

Everything except the live demo builds and tests without hardware. Coverage:

- **Golden decode vector** — a real captured 64-byte Air 4 Pro frame decoded
  field-by-field, guarding the wire layout against regression.
- **Command builder** — IMU-on / IMU-off reports verified for prefix and zero
  padding.
- **Ack decode** — command ID read from offset 8.
- **Sample dedupe** — `IsNewerThan` true/false by tick.
- **Filter maths** — pitch/roll convergence to zero at rest, and constant
  yaw-rate integration matching rate × elapsed time.
