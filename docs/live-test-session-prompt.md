# RayNeo HUD — Live Test & Smoke Test Walkthrough (carry-over prompt)

Paste everything below into a new Cowork session. Connect the folder
`E:\work\Rayneo-USB-SDK` to the session first so Claude can read the code and
docs it references.

---

You are guiding me (Kurt Mitchell) through the full live/smoke test pass of my
**RayNeo HUD** project at `E:\work\Rayneo-USB-SDK`. Read `README.md`,
`CLAUDE.md`, `docs/ARCHITECTURE.md`, and `docs/todo.md` from the connected
folder before we start so you have the project in your head.

## Project context (trust this, verify against the repo)

- Managed .NET 10 client for the RayNeo Air 4 Pro AR glasses (VID `0x1BBB`,
  PID `0xAF50`): HID device layer + IMU complementary filter
  (`src/RayNeo.Device`), console demo/calibration tool (`src/RayNeo.Console`),
  WPF overlay HUD (`src/RayNeo.Hud`), and a push-to-talk Claude voice
  assistant with tool use (`src/RayNeo.Voice`).
- All 135 unit tests pass (12 Device + 92 Voice + 31 Hud) as of commit
  `ccc6f0b` ("Add local Whisper STT engine and HUD tests"). Unit testing is
  done; **this session is exclusively about what unit tests cannot verify:
  live behavior on real hardware.**
- Speech-to-text has two engines behind `ISpeechToText`: Windows
  `System.Speech` dictation (default) and a new fully local Whisper backend
  (NAudio capture + Whisper.net), selected with `--stt whisper
  --whisper-model <path>` (or the `RAYNEO_WHISPER_MODEL` environment
  variable). If the model is missing or fails to load, the HUD falls back to
  System.Speech with an on-glass warning.
- Hard rules from `CLAUDE.md`: **never run git write operations** (I commit
  manually; read-only git is fine). The Anthropic key comes only from the
  `ANTHROPIC_API_KEY` environment variable — never echo, log, or write it.
  Microphone captures only while push-to-talk is held; audio never touches
  disk.

## Your job in this session

Walk me through the phases below **one test at a time**. For each test: give
me the exact command or action, tell me exactly what I should see on the
glasses / console / hear in audio, then **wait for my observed result before
moving on**. Keep a running scorecard (pass / fail / notes) in the session's
task list. You cannot run Windows commands yourself — I run everything in my
own terminal and report back; you interpret, debug, and track.

If a test fails, help me diagnose before moving on (the likely fix locations
are noted per test), but never let debugging derail the pass — log it and
continue if a fix is more than a few minutes.

At the end, produce `docs/live-test-results-YYYY-MM-DD.md` in the repo with
the full scorecard, and propose (but let me apply/commit) any edits to
`docs/todo.md` for items this pass verifies or refutes. Three pending items in
`docs/todo.md` are specifically waiting on THIS session's evidence:

1. **Empirical gyro axis-to-body mapping** — verified/refuted by the
   `calibrate` nod/shake/roll test (fix location:
   `HeadOrientationFilter.Update` axis assignment).
2. **Pin left/right polarity** — verified/refuted by the voice pin test (fix
   location: flip the sign of `SideYawOffsetDeg` in
   `src/RayNeo.Hud/Voice/PinSurface.cs`).
3. **Magnetometer yaw correction** — not implemented; just have me observe
   and record how much yaw drifts over ~5 minutes so we can size the problem.

## Phase 0 — Preflight (check every item before anything runs)

Ask me to confirm each:

- Glasses connected via USB-C and showing as a **second display** (extend
  mode, 1920x1080). `dotnet --list-sdks` shows a 10.0.x SDK.
- A working microphone set as the **default recording device**, and audio
  output routed to the glasses (or wherever I want TTS).
- `ANTHROPIC_API_KEY` set in the environment of the terminal I'll launch
  from (confirm with `echo %ANTHROPIC_API_KEY:~0,8%...` — never the full key).
- For Whisper: a ggml model downloaded, e.g. `ggml-base.en.bin` (~142 MB)
  from <https://huggingface.co/ggerganov/whisper.cpp/tree/main>, saved
  somewhere like `C:\models\`. (If I don't have it yet, walk me through
  downloading it now — the Whisper phase needs it.)
- Fresh build + test baseline: `dotnet build RayNeoHud.slnx` then
  `dotnet test RayNeoHud.slnx` — expect **135 passed, 0 failed** before any
  live testing.

## Phase 1 — Device layer (console `run`)

`dotnet run --project src/RayNeo.Console -- run`

- Tick-rate measurement completes (expect ~495 Hz), then live pitch / roll /
  yaw / temperature readout.
- Glasses flat on the desk: pitch and roll settle near 0° and stay quiet;
  temperature is plausible.
- Head motions move the right numbers in the right directions (look up =
  pitch, tilt = roll, turn = yaw). Note any sign inversions.
- **Yaw drift observation for todo item 3:** leave the glasses still ~5
  minutes; record how far yaw wanders.
- Negative test: unplug the glasses and run again — expect a clean, clear
  error message, not a crash or hang.

## Phase 2 — Calibration (console `calibrate`)

`dotnet run --project src/RayNeo.Console -- calibrate`

- Follow the nod / shake / roll prompts. The RMS results should attribute
  each motion to the expected gyro axis.
- **This is the decisive test for todo item 1.** Record the exact RMS output.
  If a motion maps to the wrong axis, help me correct the axis assignment in
  `HeadOrientationFilter.Update`, rebuild, and re-run until it's right.

## Phase 3 — HUD overlay (no voice focus yet)

`dotnet run --project src/RayNeo.Hud`

- Overlay auto-targets the glasses' 1920x1080 display; try `--display N` to
  override and confirm the on-glass warning when targeting falls back.
- Chrome renders on the glass: clock top-center, status + temperature
  top-left, pitch/yaw/roll readout bottom-center.
- Window behavior: click-through (clicks land on whatever is behind it),
  absent from Alt-Tab, never steals focus, stays topmost.
- **World anchoring:** the cyan crosshair locks straight ahead — turn your
  head and it holds its place in the world, clamps and fades at the FOV
  edge, and stays level with the horizon when you roll your head.
- Degradation: run with the glasses unplugged — HUD comes up with simulated
  head motion and says so on-glass.

## Phase 4 — Voice assistant, System.Speech engine (default)

Same HUD run. Confirm no voice warning appears on-glass at startup (key +
speech engine + mic all present). Then, one at a time:

- **Happy path:** hold F8, speak "start a five minute tea timer", watch the
  live partial transcript on-glass while holding, release — state indicator
  walks Listening → Thinking → Speaking, the reply streams onto the glass
  and is read aloud, and a timer chip appears.
- **Timers:** "what timers are running?" lists it; "cancel the tea timer"
  removes the chip; start a short (e.g. 15-second) timer and confirm the
  expiry announcement is spoken and shown.
- **Pins (todo item 2 — decisive test):** "pin buy milk to my left" then
  "pin call mom to my right". Turn your head each way: each pin must be on
  the side that was asked for and hold its world position. If mirrored,
  that's the `SideYawOffsetDeg` sign flip in `PinSurface.cs` — fix, rebuild,
  re-test. Then "list pins" and "clear the pins".
- **App launch:** "open notepad" — Notepad opens without the HUD losing its
  overlay behavior.
- **Mute:** "mute yourself" — replies still stream as text but are not
  spoken; a later "unmute yourself" (via `set_speech_muted`) restores audio.
- **Barge-in:** ask something with a long answer, press F8 mid-reply —
  speech cuts off immediately and it listens again.
- **Empty turn:** hold F8, say nothing, release — no assistant turn fires,
  loop returns to idle cleanly.
- **Session:** "clear the conversation", then verify it no longer remembers
  the previous exchange.
- **PTT override:** relaunch with `--ptt F13` (or another key) and confirm
  the HUD shows the new key and F8 no longer triggers.
- **Degradation:** relaunch in a terminal without `ANTHROPIC_API_KEY` —
  HUD runs, voice disabled, on-glass warning says why.

## Phase 5 — Voice assistant, Whisper engine

```
dotnet run --project src/RayNeo.Hud -- --stt whisper --whisper-model C:\models\ggml-base.en.bin
```

- Startup: **no** fallback warning on-glass (model loaded). Note the extra
  startup time (expect roughly 1–3 s for base.en).
- Repeat the happy path. Expect partial transcripts to update in ~1.5–3 s
  chunks (Whisper re-transcribes the buffer periodically) rather than
  word-by-word — that's by design; what matters is the **final** transcript
  accuracy. Compare recognition quality against Phase 4 on the same phrases,
  including a harder one (product names, an address).
- Empty utterance: hold, stay silent, release — empty final, no turn, no
  hallucinated text.
- Long hold: speak for 35+ seconds — partial updates stop after ~30 s (cost
  guard) but the final transcript covers everything.
- Repeat barge-in and one tool call end-to-end on Whisper.
- **Fallbacks:** launch with a bogus `--whisper-model C:\nope.bin` — HUD
  must come up on System.Speech with an on-glass warning naming the path;
  voice still works. Then unset/misset test the `RAYNEO_WHISPER_MODEL`
  environment variable route (set it, pass no `--whisper-model`, confirm
  Whisper engages).
- Privacy spot-check: while capturing, confirm nothing audio-like appears in
  `%TEMP%` or the repo (the only file the voice stack writes is
  `%TEMP%\rayneo-hud.log`, which is startup diagnostics).

## Phase 6 — Wrap-up

- Write `docs/live-test-results-YYYY-MM-DD.md`: scorecard for every test
  above, observed values (tick rate, calibrate RMS table, yaw drift over 5
  min, Whisper model load time, partial cadence), failures and fixes applied.
- Propose edits to `docs/todo.md`: check off items 1 and 2 if verified (with
  a "(verified: live smoke test YYYY-MM-DD)" note in the repo's style), and
  enrich the magnetometer item with the measured drift number.
- If any code changed during the session (axis mapping, pin polarity),
  re-run `dotnet test RayNeoHud.slnx` (must stay 135/135) and remind me what
  to review before I commit. **Do not commit anything yourself.**
