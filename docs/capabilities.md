# Capabilities

Current implementation status for RayNeo HUD capabilities.

## Status legend

- 🟢 Implemented and verified
- 🟡 Planned / pending implementation (explicitly called out in source/docs)
- 🔴 Not started / future backlog

## Device layer (RayNeo.Device)

- 🟢 Vendor HID connection and open/lock handling (VID/PID detection).
- 🟢 Background read loop for device input reports.
- 🟢 Command frame builder (`0x66 | cmd | value | payload`).
- 🟢 IMU frame decode from real captured Air 4 Pro frame (golden vector).
- 🟢 Command-ack decode (tick + acked command id).
- 🟢 IMU sample model with tick-based dedupe (`IsNewerThan`).
- 🟢 Complementary orientation filter:
  - Pitch/roll gravity correction.
  - Gyro yaw integration.
- 🟡 Magnetometer-based yaw correction to reduce drift.
- 🟡 Final empirical gyro axis-to-body mapping validation via calibration motions.

## Console tooling (RayNeo.Console)

- 🟢 `run` mode for live orientation/temperature readout.
- 🟢 Tick-rate measurement before live readout.
- 🟢 `calibrate` mode (nod/shake/roll RMS axis validation flow).
- 🟢 Clear failure behavior when glasses are unavailable.

## HUD overlay (RayNeo.Hud)

- 🟢 Borderless transparent click-through topmost overlay window.
- 🟢 Placement on glasses display using physical pixel bounds.
- 🟢 Display targeting strategy:
  - Native-resolution monitor match.
  - Secondary monitor fallback.
  - Primary monitor fallback.
  - `--display N` override.
- 🟢 HUD compositor (~60 FPS) with:
  - Screen-fixed elements.
  - World-anchored elements.
  - FOV edge clamp and fade behavior.
- 🟢 Orientation provider abstraction with:
  - Live device provider.
  - Simulated fallback provider.

## Voice interaction core (RayNeo.Voice)

- 🟢 Formal interaction state machine with legal/illegal transition coverage.
- 🟢 Push-to-talk interaction loop orchestration.
- 🟢 Barge-in during thinking/streaming/speaking.
- 🟢 Conversation history with ordering and validation.
- 🟢 Fault handling path back to idle state.
- 🟢 Mute/unmute speech behavior integrated into control flow.

## Assistant and tool-use layer

- 🟢 Assistant/model transport abstraction (`IModelTurnTransport`).
- 🟢 Agentic tool loop (`AssistantToolLoop`) with:
  - Multi-round tool execution.
  - Round limit safety.
  - Cancellation support.
  - Tool activity events.
  - Error result propagation for unknown/failing tools.
- 🟢 Anthropic transport mapping and streamed partial-JSON tool argument accumulation.
- 🟢 API key handling from environment variable only (`ANTHROPIC_API_KEY`).

## Built-in tools

- 🟢 Timer tools:
  - `start_timer`
  - `cancel_timer`
  - `list_timers`
- 🟢 Session tools:
  - `get_current_time`
  - `set_speech_muted`
  - `clear_conversation`
- 🟢 HUD tools:
  - `pin_note`
  - `list_pins`
  - `clear_pins`
  - `open_app_or_url`
- 🟡 Pin left/right polarity live verification and possible sign correction.

## Speech and input (Windows adapters)

- 🟢 System speech-to-text adapter (capture only while push-to-talk is held).
- 🟢 System text-to-speech adapter.
- 🟢 Global push-to-talk low-level keyboard hook (default F8, configurable).
- 🟢 HUD voice view:
  - State indicator.
  - Transcript panel.
  - Streaming reply panel.
  - Tool activity toasts.
  - Timer chips.
- 🟢 Graceful HUD-mode degradation when speech stack/API key is unavailable.
- 🟡 Whisper-backed speech-to-text implementation behind `ISpeechToText`.

## Test coverage and quality gates

- 🟢 Golden-vector protocol tests for immutable wire decode behavior.
- 🟢 Unit coverage for frame parsing, ack parsing, dedupe, and filter behavior.
- 🟢 Exhaustive state machine transition tests.
- 🟢 Voice controller tests (happy path, faults, barge-in, mute, timers).
- 🟢 Tool loop tests (chained tools, errors, cancellation, round limits).
- 🟢 Timer and built-in tool tests.
- 🟢 Build + test pass without hardware for non-live scenarios.

## Upcoming implementation focus

- 🟡 Add magnetometer yaw fusion in `HeadOrientationFilter`.
- 🟡 Complete empirical axis mapping verification and lock in final orientation mapping.
- 🟡 Finalize pin lateral polarity validation on live hardware.
- 🟡 Add local Whisper speech recognizer implementation behind `ISpeechToText`.
- 🔴 Define and prioritize post-Whisper voice enhancements (not yet scoped in repo docs).
