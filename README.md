# RayNeo HUD

Managed .NET client for the RayNeo Air 4 Pro AR glasses (VID `0x1BBB`,
PID `0xAF50`). Talks to the vendor HID interface, decodes the IMU stream,
and produces head orientation via a complementary filter.

Author: Kurt Mitchell.

## Layout

```
src/RayNeo.Device/         Class library — client, filter, wire parser
src/RayNeo.Console/        Console app — live demo and calibration tool
tests/RayNeo.Device.Tests/ xUnit tests (golden decode vector + filter maths)
```

## Prerequisites

- Windows 11 (HidSharp handles the raw HID interface).
- .NET SDK 10.0 (LTS). Check with `dotnet --list-sdks`.
- RayNeo Air 4 Pro glasses connected via USB-C for the live demo and
  calibration — everything else builds and tests without the device.

## Build

```
dotnet build RayNeoHud.slnx
```

## Test

```
dotnet test RayNeoHud.slnx
```

The tests cover a real captured 64-byte IMU frame (the golden vector),
the command-frame builder, the sample dedupe key, and both convergence
paths of `HeadOrientationFilter`.

## Run

Live orientation readout (default):

```
dotnet run --project src/RayNeo.Console -- run
```

Prints pitch / roll / yaw and temperature until you press a key.

Calibration walkthrough:

```
dotnet run --project src/RayNeo.Console -- calibrate
```

Streams 10 seconds of samples to measure the actual tick counter rate
(against `Stopwatch`), then prompts the user to nod / shake / roll their
head for 3 seconds each. It reports the gyro axis with the highest RMS
for each motion, prints the concluded axis-to-motion mapping, and shows
the measured `TickRateHz` for feeding back into `HeadOrientationFilter`.

If the glasses are not connected the console app exits with the
message `RayNeo glasses not found. Confirm they are plugged in
(expected VID 0x1BBB, PID 0xAF50).`

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
  protocol, filter design, threading, and testing strategy.
- [`docs/todo.md`](docs/todo.md) — completed vs. pending work.
