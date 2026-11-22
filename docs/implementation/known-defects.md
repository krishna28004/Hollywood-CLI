# Known defects and unverified behavior

## High priority

- Direct Windows Terminal/Console Host behavior remains unverified. Updated ConPTY confirms the seven-panel scene, animation after ignored ordinary keys, 12×5 resize fallback, ETX/Ctrl+C cancellation, and cursor-show restoration.
- Linux/macOS adapter, PTY harness, and native runner matrix are implemented but unexecuted on those operating systems; do not advertise cross-platform compatibility until native evidence passes.
- `TerminalLease.Dispose` swallows terminal restoration write failures; cleanup status is not surfaced yet.
- No performance benchmark exists.
- Stalled-output backpressure, forced writer failure, and direct terminal mode restoration still need tests.

## Feature gaps

- Live system telemetry is intentionally unavailable; metrics remain simulated.
- Unicode glyph mode and panel rotation are not supported.
- Config schema accepts only simulated telemetry. Optional live telemetry is not exposed.
- Cross-platform installation manifests and signed/notarized macOS release artifacts are not complete. Windows x64 portable packaging exists locally; no package is published.
- Windows 11, Windows Terminal, Console Host, PowerShell 5.1, and Arm64 have not been verified in this session.

Update this register only with reproducible findings. Do not describe the project as defect-free.
