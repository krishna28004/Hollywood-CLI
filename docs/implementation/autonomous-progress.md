# Engineering progress

## Current milestone

Cross-platform milestone C2/C3. Status: **IN PROGRESS**; the POSIX implementation, tests, and six-RID CI are authored. Current Linux/macOS CI validation and direct terminal-host checks remain release gates. Updated Windows x64 ConPTY verification has passed; performance benchmarks remain outstanding.

## Completed in this work cycle

- Audited the current CLI, terminal lease, renderer, layout solver, module interface, tests, and repository boundary.
- Added schema-versioned JSON configuration with bounded file size and nesting, unknown/duplicate property rejection, value validation, and CLI overrides.
- Added `config path`, `config show`, `config validate`, `--config`, and `--no-config`.
- Added Retro Amber as the fifth theme, 16-color/truecolor/mono renderer modes, animation speed, frame cap, reduced motion, and enabled-module selection.
- Updated user documentation and added a sample configuration.
- Refreshed the flagship visual experience to a seven-panel, Ubuntu-style operations layout, with syntax-colored code, compact network graphs/feeds, system process output, event logs, data, and topology. Only Ctrl+C exits; ordinary keys are drained and ignored.
- Built a local portable Windows x64 ZIP and SHA-256 manifest. These are ignored artifacts, not source history.
- Initialized a separate project-local Git repository. Its ignore rules exclude the local research folder, build outputs, and artifacts.
- Completed the cross-platform audit and introduced `ITerminalSession`, retaining the Windows Win32 adapter while adding a shared POSIX adapter, POSIX SIGTERM cancellation, conservative color fallback, and platform-specific config paths.
- Added a Python standard-library PTY end-to-end harness for Linux/macOS and a native-runner CI matrix for Windows/Linux/macOS x64/ARM64. These tests/workflows have not yet run on a POSIX runner.

## Verification evidence

- Release build and current console test harness passed after the changes, with zero compiler warnings/errors.
- The harness covers 96,000 layout cases, renderer behavior, deterministic random streams, config round-tripping, invalid schema/value/unknown/duplicate properties, and theme palette output.
- CLI help, a Retro Amber dense snapshot, sample config validation, and rejection of a missing explicitly selected config were exercised.
- Rebuilt and ran the updated Windows ConPTY harness against a self-contained single-file x64 executable. The latest run captured a live scene and 7,590 additional VT bytes over 700 ms; confirmed letters, Space, and Escape did not stop animation; resized to 42×12, 12×5, and 120×30; and confirmed the tiny fallback, Ctrl+C exit code 130, and cursor-show restoration.
- Windows UI automation initialization timed out. No direct interactive Windows Terminal/Console Host claim is made.

## Bugs found and addressed

- An invalid array initializer introduced during module selection failed the first build; corrected and rebuilt.
- `--layout auto` selected six panels even though the new default scene uses seven; the auto preset now resolves to seven.
- An explicit missing config path previously fell back to defaults; it now fails before terminal acquisition.
- Panel rendering is capped to the number of enabled modules.
- Initial ConPTY testing found console standard streams can be non-console handles even though `CONOUT$`/`CONIN$` exist. Terminal acquisition now falls back to those console device handles and reads keyboard events from the Win32 console input queue.
- Resize testing found .NET window-size properties did not report the pseudoconsole viewport. Size polling now reads `GetConsoleScreenBufferInfo`, and a dimension change clears the previous viewport before drawing.
- Cross-platform audit found terminal acquisition was directly wired into CLI orchestration; platform selection now goes through a shared terminal-session contract.

## Remaining risks and next tasks

1. Build and run the POSIX PTY harness and six-RID CI on actual Linux/macOS/Windows runners; fix platform-specific issues from evidence.
2. Validate visible Windows Terminal, Console Host, GNOME Terminal/Konsole, SSH/tmux, Terminal.app, and iTerm2 sessions.
3. Add output-stall and injected acquisition/write-failure tests; record terminal restoration behavior.
4. Record platform-specific performance measurements; no benchmark has been run.
5. Verify clean-user portable launches, archive checksums, libc baseline, and macOS signing/notarization requirements.
6. Create the requested private GitHub remote and push only reviewed project files after authenticated GitHub access is available.

## Continuation

Start with `dotnet build Hollywood.sln -c Release --no-restore -m:1 -v:minimal`, run `dotnet run --project tests/Hollywood.UnitTests -c Release --no-build`, publish a native platform RID, then run `tests/Hollywood.ConPty` on Windows or `python3 tests/posix-pty.py <native-publish-path>` on Linux/macOS. Keep local-only research materials outside repository history. Do not claim a platform or terminal host is validated until its native evidence is recorded.
