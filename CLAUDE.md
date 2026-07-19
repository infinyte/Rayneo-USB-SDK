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
  tests without the device.