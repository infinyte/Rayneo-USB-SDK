# TODO

Status of RayNeo HUD work. Items are grouped by whether they are implemented and
covered by passing tests, or explicitly flagged as pending in the source code.
This file records only verified state — it does not track speculative features.

## Done

- [x] **Vendor HID connection** — `RayNeoClient.Open` finds the glasses by
  VID/PID and opens the HID stream, with clear errors when absent or locked.
- [x] **Background read loop** — validates the `0x99` magic and dispatches IMU
  and ack frames on a background thread.
- [x] **Command frame builder** — `66 | cmd | value | payload` with report-ID
  prefix and zero padding. *(verified: `BuildCommandReport` tests)*
- [x] **IMU frame decode** — full field-by-field decode against a real captured
  Air 4 Pro frame. *(verified: golden-vector test)*
- [x] **Command-ack decode** — tick and acked command ID. *(verified:
  `ReadAckCommandId` test)*
- [x] **Sample model + dedupe** — `RayNeoImuSample` with tick-based
  `IsNewerThan`. *(verified: `RayNeoImuSample` tests)*
- [x] **Complementary orientation filter** — pitch/roll gravity correction and
  gyro yaw integration. *(verified: at-rest convergence and constant-yaw-rate
  integration tests)*
- [x] **Console `run` command** — per-device tick-rate measurement then live
  pitch / roll / yaw / temperature readout.
- [x] **Console `calibrate` command** — tick-rate measurement plus nod / shake /
  roll RMS test for gyro axis mapping.

## Pending

These are flagged directly in the source, not verified by any test:

- [ ] **Magnetometer yaw correction** — yaw is currently gyro-only and drifts.
  The glasses do report magnetometer data (`MagX/Y/Z`), noted in
  `HeadOrientationFilter`'s summary and `RayNeoImuSample`.
- [ ] **Empirical gyro axis-to-body mapping** — the pitch/roll/yaw axis
  assignment in `HeadOrientationFilter.Update` is marked as needing empirical
  verification via the `calibrate` nod/shake/roll test.
