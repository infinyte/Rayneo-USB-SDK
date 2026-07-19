# RayNeo HUD — Project Conventions
- Owner: Kurt Mitchell. All file headers and package metadata use this name.
- NEVER run git write operations (add/commit/push/branch/merge/tag/rebase).
  Kurt commits manually. Read-only git commands are permitted.
- C#/.NET. Follow Microsoft naming conventions and framework design guidelines.
- Code style: clean, readable, well-commented. XML doc comments on public APIs.
- RayNeoClient.cs is the protocol source of truth. Do not alter protocol
  constants, frame offsets, or the frame layout documented in its header.
- Hardware (RayNeo Air 4 Pro, VID 0x1BBB / PID 0xAF50) may not be plugged in
  during development. Everything except the live demo must build and pass

## Phase 2 — HUD Application
- Anthropic API key comes ONLY from the ANTHROPIC_API_KEY environment
  variable. Never hardcode, write to a file, echo, or log it.
- The golden-vector protocol tests are immutable. Any change that breaks
  them is wrong by definition; fix the change, never the test.
- The RayNeo glasses are a secondary Windows display. The HUD is a host-side
  window rendered onto that display — nothing executes on the glasses.
- Each milestone must build, pass all tests, and be runnable before the
  next begins. Kurt may stop after any milestone.

  
  ## Phase 3 — Voice
- Microphone is captured ONLY while push-to-talk is held. Never
  always-listening; no audio is ever written to disk.
- Speech-to-text and text-to-speech live behind interfaces
  (ISpeechToText / ITextToSpeech) so engines are swappable (Whisper
  later). Consumers depend on the interfaces only.
- The HUD window is click-through and unfocused by design. All voice
  controls must work system-wide (global hotkey), never via window focus.
- Every HUD state change must be visible on the glasses — Kurt cannot
  see the console while wearing them.
  tests without the device.