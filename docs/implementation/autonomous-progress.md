# Engineering progress

## Current milestone

Phase 3 hardening. Status: **CONDITIONAL**; direct-host behavior and performance have not been proven.

## Completed in this work cycle

- Audited the current CLI, terminal lease, renderer, layout solver, module interface, tests, and repository boundary.
- Added schema-versioned JSON configuration with bounded file size and nesting, unknown/duplicate property rejection, value validation, and CLI overrides.
- Added `config path`, `config show`, `config validate`, `--config`, and `--no-config`.
- Added Retro Amber as the fifth theme, 16-color/truecolor/mono renderer modes, animation speed, frame cap, reduced motion, and enabled-module selection.
- Updated user documentation and added a sample configuration.
- Built a local portable Windows x64 ZIP and SHA-256 manifest. These are ignored artifacts, not source history.
- Initialized a separate project-local Git repository. Its ignore rules exclude the local research folder, build outputs, and artifacts.

## Verification evidence

- Release build and current console test harness passed after the changes, with zero compiler warnings/errors.
- The harness covers 96,000 layout cases, renderer behavior, deterministic random streams, config round-tripping, invalid schema/value/unknown/duplicate properties, and theme palette output.
- CLI help, a Retro Amber dense snapshot, sample config validation, and rejection of a missing explicitly selected config were exercised.
- Added and ran a Windows ConPTY harness against the self-contained x64 executable. It captured a live scene and 1,644 additional bytes during a 700 ms animation interval; resized to 42×12, 12×5, and 120×30; observed the tiny fallback; sent Q and Escape to separate launches; and verified cursor-show output and exit code 0.
- Windows UI automation initialization timed out. No direct interactive Windows Terminal/Console Host claim is made.

## Bugs found and addressed

- An invalid array initializer introduced during module selection failed the first build; corrected and rebuilt.
- The saved configuration layout was not used to select the default panel count; now `focus` and `dense` select their panel presets unless CLI panel/layout arguments override them.
- An explicit missing config path previously fell back to defaults; it now fails before terminal acquisition.
- Panel rendering is capped to the number of enabled modules.
- Initial ConPTY testing found console standard streams can be non-console handles even though `CONOUT$`/`CONIN$` exist. Terminal acquisition now falls back to those console device handles and reads keyboard events from the Win32 console input queue.
- Resize testing found .NET window-size properties did not report the pseudoconsole viewport. Size polling now reads `GetConsoleScreenBufferInfo`, and a dimension change clears the previous viewport before drawing.

## Remaining risks and next tasks

1. Extend ConPTY tests for pause/resume, Ctrl+C control events, output stalls, and injected acquisition/write failures.
2. Validate terminal mode restoration and shell usability in a visible Windows Terminal and Console Host.
3. Fix verified lifecycle/renderer defects and add regression coverage.
4. Record real performance measurements; no benchmark has been run.
5. Verify the local ZIP checksum and clean-user launch; prepare dependency/license notices.
6. Create the requested private GitHub remote and push only reviewed project files. No GitHub remote exists yet; GitHub CLI and its connector are unavailable in this environment.

## Continuation

Start with `dotnet build Hollywood.sln -c Release --no-restore -m:1 -v:minimal`, then run `dotnet run --project tests/Hollywood.UnitTests -c Release --no-build` and `dotnet run --project tests/Hollywood.ConPty -c Release --no-build -- artifacts/win-x64/hollywood.exe`. Keep local-only research materials outside the repository history. Do not claim direct-host compatibility until Windows Terminal/Console Host evidence exists.
