# Whisper Speech Backend — Implementation Plan

**Item:** `docs/todo.md` → Pending → *Whisper speech backend* — `ISpeechToText` is
engine-agnostic so a local Whisper recognizer can replace `SystemSpeechToText`.

**Author:** Kurt Mitchell
**Status:** Approved plan — tests are written first, implementation follows, and
documentation is updated only after every test passes.

---

## 1. Goal and constraints

Add a fully local Whisper speech recognizer as a second `ISpeechToText`
implementation, selectable at launch, with `SystemSpeechToText` remaining the
default. All existing behavior, tests, and contracts are preserved.

Constraints carried over from `CLAUDE.md` (Phase 3) and the interface contract:

- **Push-to-talk only.** Microphone capture runs strictly between `Start()` and
  `Stop()`. Never always-listening.
- **No audio ever touches disk.** All captured samples live in memory and are
  discarded when the turn completes. (The Whisper *model file* is on disk — that
  is configuration, not audio.)
- **The `ISpeechToText` contract is honored exactly:** `PartialRecognized` fires
  with live hypotheses while capturing; after `Stop()` exactly one
  `FinalRecognized` fires with the best final transcript (empty string when
  nothing was recognized); `Failed` reports faults and the loop treats them as
  such. Consumers (`VoiceInteractionController`) are not touched.
- **Everything builds and tests without hardware** — no microphone, no network,
  and no model file are needed by any unit test.
- **Graceful degradation.** A missing/invalid model must not crash the overlay;
  the HUD falls back with an on-glass warning, consistent with how voice
  degrades today.
- Microsoft naming conventions, XML doc comments on public APIs, file headers
  with author attribution — matching the rest of the repository.

## 2. Technology choices

| Concern | Choice | Rationale |
| --- | --- | --- |
| Whisper runtime | **Whisper.net 1.9.1** + **Whisper.net.Runtime** (NuGet) | Managed bindings over whisper.cpp. Runs in-process, accepts in-memory float samples (satisfies the never-persist-audio rule), CPU-only by default, no service or Python dependency. |
| Microphone capture | **NAudio 2.3.0** (`WaveInEvent`) | Battle-tested WinMM capture; 16 kHz / 16-bit / mono directly matches Whisper's required input, so no resampling stage is needed. |
| Model | User-supplied ggml model file (e.g. `ggml-base.en.bin`, ~142 MB, from the whisper.cpp Hugging Face repo) | No surprise downloads at runtime; the path comes from `--whisper-model` or the `RAYNEO_WHISPER_MODEL` environment variable. |

Both packages are added **only** to `RayNeo.Hud.csproj`. `RayNeo.Voice` stays
dependency-pure (Anthropic SDK only), exactly as its csproj comment promises.

## 3. Placement and design

Per Kurt's decision, the backend lives **inside `src/RayNeo.Hud/Voice/`**,
alongside `SystemSpeechToText`, keeping the "Windows engines live in the Hud
project" convention. To keep the orchestration logic unit-testable without a
microphone or a model, the class is split along two small seams:

```
src/RayNeo.Hud/Voice/
├── IAudioCaptureSource.cs       seam: push-to-talk microphone capture
├── WaveInAudioCaptureSource.cs  NAudio adapter (16 kHz / 16-bit / mono → float)
├── IWhisperTranscriber.cs       seam: float samples in, transcript out
├── WhisperNetTranscriber.cs     Whisper.net adapter (loads the ggml model once)
├── WhisperSpeechToText.cs       ISpeechToText orchestrator (pure logic, testable)
└── VoiceRuntime.cs              (modified) engine selection + fallback
```

### 3.1 `IAudioCaptureSource`

```csharp
public interface IAudioCaptureSource : IDisposable
{
    /// <summary>A block of captured 16 kHz mono samples, normalized to [-1, 1].</summary>
    event EventHandler<float[]>? SamplesAvailable;

    /// <summary>Raised when capture fails (device unplugged, driver error).</summary>
    event EventHandler<Exception>? Failed;

    void Start();
    void Stop();
}
```

`WaveInAudioCaptureSource` implements it with `WaveInEvent` at
16 kHz/16-bit/mono, converting each `DataAvailable` buffer to normalized floats
(via a small internal `PcmAudio.ToFloatSamples` helper — unit-tested). Nothing
is ever written to disk.

### 3.2 `IWhisperTranscriber`

```csharp
public interface IWhisperTranscriber : IDisposable
{
    /// <summary>Transcribes 16 kHz mono samples; returns the joined transcript text.</summary>
    Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken);
}
```

`WhisperNetTranscriber` builds a `WhisperFactory` from the model path once at
construction (throws `FileNotFoundException` with a helpful message — including
where to download models — when the path is missing or wrong) and creates a
processor per call. Segment texts are concatenated and trimmed.

### 3.3 `WhisperSpeechToText` — the orchestrator

Constructor-injected seams plus two tuning knobs so tests are deterministic:

```csharp
public WhisperSpeechToText(
    IAudioCaptureSource capture,
    IWhisperTranscriber transcriber,
    int partialIntervalSamples = 24_000,  // ≈1.5 s of new audio per partial pass
    int minFinalSamples       = 4_800)    // ≈0.3 s: below this, final = ""
```

Behavior (mirrors `SystemSpeechToText`'s locking and idempotency style):

- **`Start()`** — no-op when disposed or already capturing. Clears the sample
  buffer, bumps a turn-generation counter (so stragglers from the previous turn
  are ignored), and starts capture. A capture start failure raises `Failed` and
  resets state.
- **While capturing** — samples accumulate in an in-memory buffer. Whisper has
  no native streaming mode, so partials are produced by **periodic
  re-transcription**: once ≥ `partialIntervalSamples` of *new* audio has arrived
  and no pass is in flight, a snapshot of the whole buffer is transcribed on a
  background task and the result raises `PartialRecognized` — but only if the
  turn is still capturing and the generation matches. At most one partial pass
  runs at a time; partial passes stop once the buffer exceeds ~30 s (guard
  against unbounded re-transcription cost on very long holds — the final pass
  still covers everything).
- **`Stop()`** — no-op when not capturing. Stops capture, cancels any in-flight
  partial pass, then: if the buffer is shorter than `minFinalSamples`, emits
  `FinalRecognized("")` immediately (running Whisper on near-silence wastes
  time and invites hallucinated text); otherwise transcribes the full buffer
  and raises **exactly one** `FinalRecognized`. A transcription error raises
  `Failed` instead (never both), matching `SystemSpeechToText`.
- **Reuse** — the instance survives across turns; each `Start()` begins with an
  empty buffer.
- **`Dispose()`** — cancels any in-flight work, stops and disposes the injected
  capture source and transcriber (it takes ownership), and suppresses all
  further events. `Start()` after dispose is a no-op.
- **Threading** — events fire on capture/thread-pool threads, same as the
  `System.Speech` engine's events today; `VoiceInteractionController` already
  handles that.
- **Privacy** — the buffer is memory-only and cleared at the start of every
  turn; no file, temp file, or stream sink exists anywhere in the path.

### 3.4 Engine selection and wiring

- `VoiceOptions` gains two optional members (defaults preserve today's
  behavior): `SpeechEngineKind Engine` (`System` | `Whisper`, default `System`)
  and `string? WhisperModelPath`.
- Command-line: `--stt whisper` selects the engine; `--whisper-model <path>`
  supplies the model, falling back to the `RAYNEO_WHISPER_MODEL` environment
  variable. Parsing is extracted into a small pure, testable helper alongside
  the existing `--ptt` / `--display` handling.
- `VoiceRuntime.TryCreate` builds the selected engine and holds it as
  `ISpeechToText` (today it holds the concrete `SystemSpeechToText`). If
  Whisper is requested but the model path is missing or the file doesn't
  exist, the runtime **falls back to `SystemSpeechToText` and surfaces an
  on-glass warning** — voice stays usable, and the wearer can see why, per the
  "every state change visible on the glasses" rule.
- Model load happens once at startup (1–3 s for `base.en` on CPU), not per
  turn.

## 4. Tests first (written before any implementation, confirmed red)

New project **`tests/RayNeo.Hud.Tests`** (`net10.0-windows`, `UseWPF=true` so
the project reference to the WPF exe resolves; same xunit/coverlet/Test.Sdk
versions as the existing test projects; added to `RayNeoHud.slnx`). Because the
backend lives in the Windows-targeted Hud project, these tests are
Windows-only — they need no microphone, model, network, or glasses, but they
run on `littlemistress`, not in a Linux container.

Fakes mirror the existing test style (`FakeAudioCaptureSource` raises sample
blocks on demand; `FakeWhisperTranscriber` completes via `TaskCompletionSource`
so tests control timing exactly — no sleeps, no real clocks).

**`WhisperSpeechToTextTests`** — the contract, exhaustively:

| Test | Asserts |
| --- | --- |
| `StartBeginsCapture` | capture started exactly once |
| `StartWhileCapturingIsNoOp` | second `Start` ignored |
| `StopWithoutStartIsNoOp` | no events, capture untouched |
| `StopEmitsExactlyOneFinalTranscript` | happy path; transcriber saw the full buffer; one `FinalRecognized` |
| `StopWithNoAudioEmitsEmptyFinal` | sub-threshold buffer → `""` final, transcriber never called |
| `PartialEmittedAfterThresholdAudio` | ≥ interval of new audio → one partial pass, `PartialRecognized` raised |
| `SecondPartialCoversWholeBuffer` | later pass receives all accumulated samples |
| `OnlyOnePartialPassInFlight` | more audio while a pass is pending → no concurrent second call |
| `PartialCompletingAfterStopIsSuppressed` | stale hypothesis never fires after `Stop` |
| `TranscriberErrorRaisesFailedNotFinal` | fault path: `Failed` once, no `FinalRecognized`, next turn works |
| `CaptureFailureRaisesFailed` | capture `Failed` propagates; state resets |
| `SecondTurnStartsWithEmptyBuffer` | reuse: turn 2's transcriber input contains only turn 2's audio |
| `DisposeStopsCaptureAndSuppressesEvents` | dispose mid-capture is clean |
| `StartAfterDisposeIsNoOp` | disposed instance is inert |

**`PcmAudioTests`** — 16-bit PCM → float conversion: silence → 0, `short.MaxValue`
→ ~1.0, `short.MinValue` → -1.0, little-endian order, odd trailing byte ignored.

**`WhisperNetTranscriberTests`** — missing model path/file throws
`FileNotFoundException` with an actionable message (no model download in tests).

**`VoiceCommandLineTests`** — defaults (`System` engine); `--stt whisper` with
`--whisper-model`; environment-variable fallback; unknown `--stt` value
rejected with a clear error.

Existing test projects are untouched; the golden-vector protocol tests remain
immutable.

## 5. Execution order (each milestone builds and passes before the next)

1. **Baseline** — stage the tree into the build workspace, restore, build the
   solution, and run the existing test suite to confirm a green starting point.
2. **Red** — add `tests/RayNeo.Hud.Tests` with all tests above plus the seam
   interfaces they compile against; confirm the new tests fail (or fail to
   pass) for the right reason.
3. **Green** — implement `PcmAudio`, `WhisperSpeechToText`,
   `WaveInAudioCaptureSource`, `WhisperNetTranscriber`, the `VoiceOptions` /
   command-line changes, and the `VoiceRuntime` wiring. Run the full suite to
   green: platform-neutral projects (`RayNeo.Device.Tests`,
   `RayNeo.Voice.Tests`) in the build container, `RayNeo.Hud.Tests` on
   `littlemistress` via `dotnet test`.
4. **Docs** — only after everything is green (section 6).
5. **Delivery** — write all new/changed files back to `E:\work\Rayneo-USB-SDK`;
   Kurt reviews and commits (no git writes by Claude, per `CLAUDE.md`).
   A live smoke test with a real model and microphone remains a manual step
   for Kurt: `dotnet run --project src/RayNeo.Hud -- --stt whisper
   --whisper-model C:\models\ggml-base.en.bin`.

## 6. Documentation updates (after all tests pass)

| File | Change |
| --- | --- |
| `README.md` | Layout tree gains `tests/RayNeo.Hud.Tests`; Prerequisites gain the optional Whisper model note (what to download, where from); Run section documents `--stt whisper`, `--whisper-model`, and `RAYNEO_WHISPER_MODEL`; the test-coverage paragraph mentions the Whisper orchestration tests. |
| `docs/ARCHITECTURE.md` | Voice-engines row covers both engines; new Whisper backend subsection (seams, partial-pass design, privacy); threading notes (partial passes on background tasks); testing-strategy section adds the new test project. |
| `docs/todo.md` | *Whisper speech backend* moves from Pending to Done under Phase 3, with the *(verified: `WhisperSpeechToText` tests)* annotation style used elsewhere. |
| `docs/capabilities.md` | 🟡 Whisper STT rows flip to implemented; the 🔴 "post-Whisper roadmap" row stays open. |
| `CLAUDE.md` | Phase 3 bullet updated: engines are swappable and *both* are now implemented behind the seams. |
| Code comments | `ISpeechToText` summary, `SystemSpeechToText` header, and the `RayNeo.Voice.csproj` comment stop describing Whisper as future work and name the actual sibling implementation. |

## 7. Risks and mitigations

- **Partial-transcript latency.** Whisper re-transcribes the whole buffer per
  pass, so hypotheses update every ~1.5–3 s rather than word-by-word like
  `System.Speech`. Acceptable for short push-to-talk utterances; the 30 s
  partial guard bounds the cost, and the final transcript quality is the point
  of using Whisper.
- **Model load time.** 1–3 s at startup for `base.en`; done once in
  `VoiceRuntime.TryCreate`, never per turn.
- **Native runtime.** `Whisper.net.Runtime` ships win-x64 CPU binaries in the
  NuGet package — no extra install. GPU (`Whisper.net.Runtime.Cuda`) is out of
  scope; can be a later opt-in.
- **Windows-only tests.** `RayNeo.Hud.Tests` can't run on Linux; the container
  build uses `-p:EnableWindowsTargeting=true` for compile verification and the
  actual test run happens on `littlemistress`. This is a direct consequence of
  placing the backend in the Hud project and is accepted.
- **Hallucination on silence.** Whisper invents text on empty audio; the
  `minFinalSamples` floor short-circuits to the contract's empty-string final
  instead.
