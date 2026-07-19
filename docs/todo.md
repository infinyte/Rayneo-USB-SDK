# TODO

Status of RayNeo HUD work. Items are grouped by whether they are implemented and
covered by passing tests, or explicitly flagged as pending in the source code.
This file records only verified state — it does not track speculative features.

## Done

### Phase 1 — Device

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

### Phase 2 — HUD

- [x] **Overlay window** — borderless, transparent, click-through, topmost,
  hidden from alt-tab, placed on the glasses' monitor by physical pixel bounds.
- [x] **Display targeting** — native-resolution match → secondary → primary
  fallback with on-glass warnings; `--display N` override.
- [x] **Compositor** — ~60 fps render loop; screen-fixed chrome and
  world-anchored elements with FOV-edge clamp and fade; world-lock signs
  verified live on the glasses.
- [x] **Orientation providers** — live device provider (volatile snapshot
  hand-off from the ~495 Hz sample thread) and simulated sweep fallback.

### Phase 3 — Voice + tool use

- [x] **Interaction state machine** — total transition table with barge-in and
  fault paths. *(verified: exhaustive legal/illegal transition tests)*
- [x] **Conversation history** — ordered turns, empty-turn rejection, clear.
  *(verified: `ConversationHistory` tests)*
- [x] **Voice controller** — full loop orchestration over the engine-agnostic
  interfaces: push-to-talk, transcript handling, streaming, speech, barge-in,
  faults, mute, timer announcements. *(verified: `VoiceInteractionController`
  tests against fakes)*
- [x] **Tool-use layer** — `IVoiceTool` / registry / typed arguments /
  `AssistantToolLoop` agentic loop with error results, round limit, activity
  events, and cancellation. *(verified: `AssistantToolLoop`, registry, and
  argument tests against a scripted transport)*
- [x] **Anthropic transport** — neutral message/tool mapping onto the official
  SDK with streamed partial-JSON tool-call accumulation (`ClaudeAssistantClient`
  = transport + loop; API key from `ANTHROPIC_API_KEY` only).
- [x] **Built-in tools** — timers (`start_timer` / `cancel_timer` /
  `list_timers` over `TimerService` on a fake-clock-testable `TimeProvider`),
  `get_current_time`, `set_speech_muted`, `clear_conversation`. *(verified:
  `TimerService` and built-in tool tests)*
- [x] **Windows speech engines** — `System.Speech` dictation strictly between
  press and release (no audio persisted) and synthesis with the
  single-completion-signal contract.
- [x] **Whisper speech backend** — local `WhisperSpeechToText` (NAudio capture
  + Whisper.net) as a second `ISpeechToText` engine, selectable with
  `--stt whisper` / `--whisper-model` (or `RAYNEO_WHISPER_MODEL`); System.Speech
  stays the default and Whisper degrades to it with an on-glass warning when the
  model is missing. Audio stays in memory, cleared each turn. *(verified:
  `WhisperSpeechToText`, `PcmAudio`, `WhisperNetTranscriber`, and
  `VoiceCommandLine` tests in `RayNeo.Hud.Tests`)*
- [x] **Global push-to-talk** — `WH_KEYBOARD_LL` hook on F8 (configurable via
  `--ptt`), auto-repeat filtered, works system-wide without window focus.
- [x] **HUD tools** — world-anchored note pins relative to the current gaze
  (`pin_note` / `list_pins` / `clear_pins`) and `open_app_or_url`.
- [x] **Voice HUD** — on-glass state indicator, live transcript and streaming
  reply panel, tool-activity toasts, timer chips; graceful degradation with an
  on-glass warning when the API key or speech stack is unavailable.

## Pending

These are flagged directly in the source, not verified by any test:

- [ ] **Magnetometer yaw correction** — yaw is currently gyro-only and drifts.
  The glasses do report magnetometer data (`MagX/Y/Z`), noted in
  `HeadOrientationFilter`'s summary and `RayNeoImuSample`.
- [ ] **Empirical gyro axis-to-body mapping** — the pitch/roll/yaw axis
  assignment in `HeadOrientationFilter.Update` is marked as needing empirical
  verification via the `calibrate` nod/shake/roll test.
- [ ] **Pin left/right polarity check** — `PinSurface` places lateral pins
  using the device yaw polarity assumed from the world-anchoring fix; the
  header comment flags flipping `SideYawOffsetDeg`'s sign if a live smoke test
  shows left/right mirrored.
