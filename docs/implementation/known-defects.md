# Known defects and unverified behavior

## High priority

- Direct Windows Terminal/Console Host behavior remains unverified. ConPTY confirms scene output, pause/resume, ETX/Ctrl+C cancellation, Q/Escape, tiny/rapid resize, and cursor-show restoration.
- `TerminalLease.Dispose` swallows terminal restoration write failures; cleanup status is not surfaced yet.
- No performance benchmark exists.
- Stalled-output backpressure, forced writer failure, and direct terminal mode restoration still need tests.

## Feature gaps

- Live system telemetry is intentionally unavailable; metrics remain simulated.
- Unicode glyph mode and panel rotation are not supported.
- Config schema accepts only simulated telemetry. Optional live telemetry is not exposed.
- Installation manifests, portable ZIP, checksums, and release metadata are not complete.
- Windows 11, Windows Terminal, Console Host, PowerShell 5.1, and Arm64 have not been verified in this session.

Update this register only with reproducible findings. Do not describe the project as defect-free.
