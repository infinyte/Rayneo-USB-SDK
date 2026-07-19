# Capabilities

Current implementation status for RayNeo HUD capabilities.

## Status legend

- 🟢 Implemented and verified
- 🟡 Planned / pending implementation (explicitly called out in source/docs)
- 🔴 Not started / future backlog

## Compact capability table

| Status | Area | Capability | Notes |
|---|---|---|---|
| 🟢 | Device | Vendor HID connection and open/lock handling | VID/PID detection with clear failure behavior |
| 🟢 | Device | Background read loop | Validates report framing and dispatches IMU/ack frames |
| 🟢 | Device | Command frame builder | `0x66 | cmd | value | payload` wire shape |
| 🟢 | Device | IMU + ack decoding | Golden-vector IMU decode and command-ack decode covered by tests |
| 🟢 | Device | Orientation filter core | Complementary filter with drift-corrected pitch/roll and gyro yaw |
| 🟡 | Device | Magnetometer yaw fusion | Planned to reduce yaw drift |
| 🟡 | Device | Empirical gyro-axis mapping lock-in | Finalize mapping using calibration motion validation |
| 🟢 | Console | Live readout mode (`run`) | Orientation + temperature stream after tick-rate measure |
| 🟢 | Console | Calibration mode (`calibrate`) | Nod/shake/roll RMS axis verification flow |
| 🟢 | HUD | Click-through overlay window | Borderless, transparent, topmost, non-focus UX |
| 🟢 | HUD | Display targeting and override | Native-match -> secondary -> primary, plus `--display N` |
| 🟢 | HUD | HUD compositor | ~60 FPS screen-fixed + world-anchored rendering |
| 🟢 | HUD | Orientation providers | Live device provider with simulated fallback |
| 🟢 | Voice core | Interaction state machine | Exhaustive legal/illegal transition coverage |
| 🟢 | Voice core | Push-to-talk orchestration | End-to-end loop control with fault handling |
| 🟢 | Voice core | Barge-in behavior | Interrupt during thinking/streaming/speaking |
| 🟢 | Voice core | Conversation history | Ordered turns with validation and clear support |
| 🟢 | Assistant | Agentic tool-use loop | Multi-round execution, round limits, cancellation, activity events |
| 🟢 | Assistant | Anthropic transport integration | Streamed partial JSON tool-argument accumulation |
| 🟢 | Assistant | API key policy | Environment-only key sourcing via `ANTHROPIC_API_KEY` |
| 🟢 | Tools | Timers | `start_timer`, `cancel_timer`, `list_timers` |
| 🟢 | Tools | Session controls | `get_current_time`, `set_speech_muted`, `clear_conversation` |
| 🟢 | Tools | HUD actions | `pin_note`, `list_pins`, `clear_pins`, `open_app_or_url` |
| 🟡 | Tools | Pin lateral polarity final check | Live hardware validation and optional sign correction |
| 🟢 | Speech/Input | System speech adapters | STT during hold-to-talk only; TTS output support |
| 🟢 | Speech/Input | Global push-to-talk hook | System-wide key capture (default F8, configurable) |
| 🟢 | Speech/Input | Voice HUD view | State, transcript, streaming reply, tool toasts, timer chips |
| 🟢 | Speech/Input | Whisper STT backend | Local Whisper.net recognizer behind `ISpeechToText`; `--stt whisper` (default remains System.Speech) |
| 🟢 | Quality | Automated tests and gates | Device/voice/tool/timer/state machine coverage; non-live runs pass without hardware |
| 🔴 | Roadmap | Post-Whisper voice enhancement scope | Not yet defined in repository docs |

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
- 🟢 Local Whisper speech-to-text recognizer behind `ISpeechToText`
  (`WhisperSpeechToText` over NAudio capture + Whisper.net), selected with
  `--stt whisper`; falls back to System.Speech with an on-glass warning when
  the model is missing.

## Test coverage and quality gates

- 🟢 Golden-vector protocol tests for immutable wire decode behavior.
- 🟢 Unit coverage for frame parsing, ack parsing, dedupe, and filter behavior.
- 🟢 Exhaustive state machine transition tests.
- 🟢 Voice controller tests (happy path, faults, barge-in, mute, timers).
- 🟢 Tool loop tests (chained tools, errors, cancellation, round limits).
- 🟢 Timer and built-in tool tests.
- 🟢 Whisper orchestration tests (capture lifecycle, partials, single final,
  faults, reuse), PCM→float conversion, and speech-engine command-line parsing
  (`RayNeo.Hud.Tests`, Windows-only, no microphone/model/network).
- 🟢 Build + test pass without hardware for non-live scenarios.

## Upcoming implementation focus

- 🟡 Add magnetometer yaw fusion in `HeadOrientationFilter`.
- 🟡 Complete empirical axis mapping verification and lock in final orientation mapping.
- 🟡 Finalize pin lateral polarity validation on live hardware.
- 🔴 Define and prioritize post-Whisper voice enhancements (not yet scoped in repo docs).
