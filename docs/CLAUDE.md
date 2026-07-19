# Working in `docs/`

Guidance for maintaining this documentation set. The repository-root
`CLAUDE.md` remains authoritative for code conventions and ownership; this file
only covers the docs.

## Files

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — components, data flow, protocol
  summary, filter design, threading, and testing strategy.
- [`todo.md`](todo.md) — completed vs. pending work. "Done" items must be backed
  by implemented code (ideally by a passing test); "Pending" items must be
  flagged in the source, not invented.

## Rules

- Document only what is implemented and verified. Do not add placeholder or
  speculative content.
- The wire protocol's source of truth is the header comment of
  `src/RayNeo.Device/RayNeoClient.cs`. Do not restate protocol constants, frame
  offsets, or the frame layout in a way that could drift from it — summarize and
  link instead.
- Keep `README.md` (user-facing quick start) and `ARCHITECTURE.md` (internal
  design) consistent when behavior changes.
- Owner is Kurt Mitchell; keep author attribution consistent with the code.
- Verify a claim against the current code before writing it — recalled details
  may be stale.
