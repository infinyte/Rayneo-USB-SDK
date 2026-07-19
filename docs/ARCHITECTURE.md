# Architecture

RayNeo HUD is a managed .NET client for the RayNeo Air 4 Pro AR glasses
(VID `0x1BBB` / PID `0xAF50`), a WPF heads-up-display overlay rendered onto the
glasses' secondary display, and a hands-free voice assistant that streams
Claude replies — with tool use — onto the glass. Nothing executes on the
glasses; everything is host-side.

Author: Kurt Mitchell.

## Solution layout

```
src/RayNeo.Device/         Class library — client, wire parser, IMU sample, filter
src/RayNeo.Console/        Console app — live readout and calibration tool
src/RayNeo.Hud/            WPF overlay — display targeting, HUD compositor,
                           System.Speech + Whisper STT engines, push-to-talk
                           hook, HUD tools
src/RayNeo.Voice/          Class library — voice state machine and controller,
                           conversation history, Claude client, tool-use layer
tests/RayNeo.Device.Tests/ xUnit tests
tests/RayNeo.Hud.Tests/    xUnit tests (Windows-only: Whisper orchestration)
tests/RayNeo.Voice.Tests/  xUnit tests
```

Target framework: .NET 10.0 (`RayNeo.Hud` is `net10.0-windows` with WPF; the
other projects are platform-neutral `net10.0`). Third-party dependencies:
[HidSharp](https://www.nuget.org/packages/HidSharp) for raw HID access,
[Anthropic](https://www.nuget.org/packages/Anthropic) (official SDK) for the
Claude API, `System.Speech` for the Windows dictation and synthesis engines,
and (for the optional Whisper recognizer)
[NAudio](https://www.nuget.org/packages/NAudio) for microphone capture and
[Whisper.net](https://www.nuget.org/packages/Whisper.net) for local
transcription.

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

### `RayNeo.Voice` (class library — platform-neutral)

The voice stack's testable core. No audio, UI, or SDK wire types leak out of
this project's seams; the Windows specifics live in `RayNeo.Hud` behind
interfaces.

| Type | Responsibility |
|------|----------------|
| `VoiceInteractionStateMachine` | Pure, total transition table for the loop (Idle → Listening → Transcribing → Thinking → Streaming → Speaking, barge-in, faults). |
| `VoiceInteractionController` | Orchestrates the loop: drives the state machine from `IPushToTalkSource`, `ISpeechToText`, `IAssistantClient`, `ITextToSpeech`, and (optionally) `TimerService`; raises HUD-facing events (state, partial transcript, reply deltas, tool activity, timer announcements, errors). |
| `ConversationHistory` / `ConversationTurn` | Multi-turn session memory; rejects empty turns. |
| `IAssistantClient` | Streams a reply to a conversation as text deltas. |
| `ClaudeAssistantClient` | `IAssistantClient` over the Anthropic SDK: composes `AnthropicTurnTransport` + `AssistantToolLoop`; forwards tool activity. |
| `AssistantToolLoop` | The model↔tool orchestration loop: streams turns, executes requested tools, feeds results back, repeats until the model finishes (bounded by a round limit). |
| `IModelTurnTransport` / `ModelEvent` / `ModelMessage` | Transport-neutral streaming event and message model; the seam the loop's tests fake. |
| `AnthropicTurnTransport` | The only SDK-touching type: maps neutral messages/tools onto Messages-API params and folds raw stream events (accumulating partial tool-argument JSON) back into `ModelEvent`s. |
| `IVoiceTool` / `VoiceToolParameter` / `VoiceToolRegistry` | Tool declaration and lookup; names validated, registration order preserved. |
| `DelegateVoiceTool` / `VoiceToolArguments` | One-class tool definition and typed JSON argument access with model-readable errors. |
| `TimerService` / `TimerTools` | Named countdown timers on `TimeProvider` (fake-clock testable) and their start / cancel / list tools. |
| `SessionTools` | Current time, TTS mute, conversation clear. |
| `ISpeechToText` / `ITextToSpeech` / `IPushToTalkSource` | Engine-agnostic seams for recognition, synthesis, and the global hold-to-talk key. |

### `RayNeo.Hud` (WPF overlay)

| Area | Types | Responsibility |
|------|-------|----------------|
| Display | `DisplayEnumerator`, `DisplayLocator`, `DisplayInfo` | Enumerate monitors and pick the glasses (native 1920x1080 match → any secondary → primary, with warnings). |
| Overlay | `MainWindow`, `NativeMethods` | Borderless, transparent, click-through, topmost window placed on the target monitor via physical pixel bounds. |
| Compositor | `HudCompositor`, `HudViewport`, `HudElement` | 60 fps render loop arranging `ScreenFixedElement` chrome and `WorldAnchoredElement` visuals (world-locked with FOV-edge clamp and fade). |
| Orientation | `IHeadOrientationProvider`, `DeviceOrientationProvider`, `SimulatedOrientationProvider` | Live filtered orientation from the glasses, or a synthetic sweep without hardware. |
| Voice engines | `SystemSpeechToText`, `WhisperSpeechToText`, `SystemSpeechSynthesizer` | Two swappable `ISpeechToText` recognizers — `System.Speech` dictation (default) and a local Whisper backend — plus `System.Speech` synthesis. Both capture strictly between push-to-talk press and release; audio is never persisted. |
| Whisper backend | `WhisperSpeechToText`, `IAudioCaptureSource` (`WaveInAudioCaptureSource`), `IWhisperTranscriber` (`WhisperNetTranscriber`), `PcmAudio` | Local Whisper recognizer split along two seams — NAudio 16 kHz/16-bit/mono capture and a Whisper.net transcriber — orchestrated by a pure `ISpeechToText` implementation (see below). |
| Push-to-talk | `GlobalPushToTalkHook` | `WH_KEYBOARD_LL` hook: system-wide F8 (configurable via `--ptt`) hold-to-talk with auto-repeat filtering; the HUD window never needs focus. |
| HUD tools | `PinSurface`, `HudTools` | World-anchored note pins placed relative to the current gaze (`pin_note` / `list_pins` / `clear_pins`) and `open_app_or_url` via `Process.Start`. |
| Voice wiring | `VoiceRuntime`, `VoiceHudView`, `VoiceOptions`, `VoiceCommandLine`, `SpeechEngineKind` | Composition root that builds engines + tools + controller (degrading to HUD-only with an on-glass warning when the API key or speech stack is missing), selects the recognizer from `--stt` / `--whisper-model` (`RAYNEO_WHISPER_MODEL` fallback), and the on-glass voice UI: state indicator, transcript/reply panel, tool toast, timer chips. |

## Data flow

Orientation:

```
USB HID (HidSharp)
      │  64-byte input reports
      ▼
RayNeoClient.ReadLoop        (background thread)
      │  validates magic 0x99, dispatches by frame type
      ├── 0x65 IMU  → RayNeoFrameParser.ParseImuFrame → SampleReceived event
      └── 0xC8 ack  → RayNeoFrameParser.ReadAckCommandId → CommandAcknowledged event
      ▼
DeviceOrientationProvider    (dedupe → HeadOrientationFilter → volatile snapshot)
      ▼
HudCompositor                (UI thread, ~60 fps, reads latest snapshot)
      ▼
HudElement.Arrange           (screen-fixed chrome, world-anchored visuals)
```

Voice turn:

```
GlobalPushToTalkHook (F8 down)                    hook / UI thread
      ▼
VoiceInteractionController ── ISpeechToText.Start        Idle → Listening
      │  (F8 up) Stop → final transcript                 → Transcribing
      ▼
ConversationHistory.AddUserTurn                          → Thinking
      ▼
ClaudeAssistantClient.StreamReplyAsync
      │        AssistantToolLoop ⇄ AnthropicTurnTransport (Claude API)
      │              │ tool_use → VoiceToolRegistry → IVoiceTool.ExecuteAsync
      │              └ tool results fed back; loop continues
      ▼  text deltas                                     → Streaming
VoiceHudView (reply panel, tool toasts)  +  ConversationHistory.AddAssistantTurn
      ▼
ITextToSpeech.SpeakAsync                                 → Speaking → Idle
```

Pressing F8 during Thinking / Streaming / Speaking is barge-in: the reply
task's `CancellationTokenSource` is cancelled, playback stops, and the loop
re-enters Listening. The partial reply is discarded by design.

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

## Voice loop and tool use

The interaction is a formal state machine
(`Idle → Listening → Transcribing → Thinking → Streaming → Speaking → Idle`)
whose complete legal-transition table lives in
`VoiceInteractionStateMachine` and is cross-checked by an independent copy in
its tests. Data-dependent branches are distinct triggers (empty transcript,
silent completion) so the table stays total and side-effect-free.

`VoiceInteractionController` owns the machine and is the only writer. Design
rules it enforces:

- **Push-to-talk only** — the microphone captures strictly between press and
  release; audio is never written to disk (root `CLAUDE.md`, Phase 3).
- **Barge-in** — a press in any reply state cancels the request/playback and
  returns to Listening; a stale recognizer result arriving afterwards is
  discarded by a state check.
- **Faults** — recognizer or API failures fire `Fault` back to Idle and raise
  `ErrorOccurred`; nothing crashes the overlay.
- **History integrity** — the assistant turn enters `ConversationHistory` only
  when its completion trigger is accepted, so a barge-in race can never record
  a half reply.

Tool use is an agentic loop in `AssistantToolLoop`: stream a model turn; if it
stops with `tool_use`, execute the requested tools via `VoiceToolRegistry`
(unknown tools and tool exceptions become error results the model can react
to, never crashes), append the assistant turn and results to the conversation,
and request the next turn — bounded by a round limit against runaway chains.
The loop sees only `ModelEvent` / `ModelMessage`; `AnthropicTurnTransport`
adapts those to the Anthropic SDK, accumulating streamed partial JSON until
each tool call is complete. The v1 tool set: `start_timer`, `cancel_timer`,
`list_timers`, `get_current_time`, `set_speech_muted`, `clear_conversation`,
`pin_note`, `list_pins`, `clear_pins`, `open_app_or_url`.

### Whisper speech backend

`WhisperSpeechToText` is a second `ISpeechToText` engine, selected with
`--stt whisper` (System.Speech remains the default). It is deliberately split
along two seams so its orchestration is pure and unit-testable without a
microphone or a model:

- `IAudioCaptureSource` — push-to-talk microphone capture. `WaveInAudioCaptureSource`
  adapts NAudio's `WaveInEvent` at 16 kHz / 16-bit / mono (Whisper's native
  input, so no resampling), converting each buffer to normalized floats via the
  unit-tested `PcmAudio.ToFloatSamples`.
- `IWhisperTranscriber` — float samples in, transcript out. `WhisperNetTranscriber`
  loads a ggml model once via Whisper.net and runs a CPU transcription per call.

Whisper has no native streaming mode, so `WhisperSpeechToText` accumulates the
held audio in an in-memory buffer and produces **live partials by periodic
re-transcription**: once ~1.5 s of new audio has arrived and no pass is in
flight, it snapshots the buffer and transcribes it on a background task, raising
`PartialRecognized` (single-flight; suppressed past ~30 s to bound cost). `Stop`
cancels any in-flight partial and, unless the buffer is near-silence (in which
case it emits the empty final directly, avoiding Whisper's hallucination on
silence), transcribes the whole buffer once for exactly one `FinalRecognized`.

**Privacy:** the buffer is memory-only and cleared at the start of every turn;
no file, temp file, or stream sink exists in the path — only the *model* file is
on disk, and that is configuration. **Degradation:** a missing or unloadable
model does not crash the overlay — `VoiceRuntime` falls back to System.Speech
with an on-glass warning (CLAUDE.md Phase 3).

## Threading

- `RayNeoClient` runs a single background reader thread (`IsBackground =
  true`); `SampleReceived` / `CommandAcknowledged` are raised on it, so
  consumers must not block. `DeviceOrientationProvider` publishes snapshots to
  the render loop via a single volatile reference — no locks, no torn reads.
- `HudCompositor` renders on the UI thread off `CompositionTarget.Rendering`,
  throttled to ~60 fps.
- `GlobalPushToTalkHook` fires on the UI thread (the thread that installed
  it); `System.Speech` and `TimerService` callbacks arrive on worker threads;
  the reply stream runs as a task. `WhisperSpeechToText` runs its partial and
  final transcription passes on background tasks and raises `PartialRecognized`
  / `FinalRecognized` from them, serialising its buffer and single-flight state
  under one gate. `VoiceInteractionController` serialises all transitions under
  one gate and may raise its events on any thread.
- `VoiceHudView` never marshals: event handlers only write immutable snapshot
  strings, and the per-frame callbacks (always on the UI thread) read them —
  so every voice state change is on-glass within a frame. The one exception is
  `PinSurface`, which mutates the canvas and therefore dispatches to the UI
  thread explicitly.

## Testing strategy

Everything except the live demo and the Windows-only adapters builds and tests
without hardware. Coverage:

- **Golden decode vector** — a real captured 64-byte Air 4 Pro frame decoded
  field-by-field, guarding the wire layout against regression. Immutable per
  the root `CLAUDE.md`.
- **Command builder / ack decode / sample dedupe / filter maths** — as in
  Phase 1.
- **State machine** — every legal transition and every illegal (state,
  trigger) pair, driven from an independent copy of the table.
- **Controller** — happy path, empty transcript, empty reply, mute, barge-in
  from Thinking / Streaming / Speaking, assistant and recognizer faults, stray
  inputs, stale transcripts, multi-turn history growth, and timer
  announcements — all against fakes, with the assistant's stream driven
  delta-by-delta through a channel.
- **Tool loop** — text-only turns, single and chained tool calls, argument
  passthrough, unknown tools, tool exceptions, the round limit, cancellation,
  activity events, and the exact messages sent back to the model.
- **Timers and tools** — `TimerService` under a fake `TimeProvider` (no real
  time passes) and every built-in tool's result strings and argument errors.
- **Whisper orchestration** (`RayNeo.Hud.Tests`, `net10.0-windows`) — the full
  `WhisperSpeechToText` push-to-talk contract against fakes for the microphone
  and transcriber (the fake transcriber completes via `TaskCompletionSource`, so
  tests drive timing exactly): capture lifecycle, idempotent Start/Stop,
  exactly-one-final, empty-on-silence, periodic partials with single-flight and
  post-Stop suppression, faults, turn reuse, and disposal. Plus `PcmAudio`
  16-bit→float conversion and `VoiceCommandLine` `--stt` / `--whisper-model`
  parsing. No microphone, model, network, or glasses required — but, because the
  backend lives in the `net10.0-windows` Hud project, these tests are
  Windows-only.

The remaining Windows adapters (`SystemSpeechSynthesizer`, `GlobalPushToTalkHook`,
`PinSurface`, HUD wiring) and the thin `WaveInAudioCaptureSource` /
`WhisperNetTranscriber` device edges are deliberately minimal and are verified
by running the overlay; the logic they wrap lives behind the tested interfaces.
