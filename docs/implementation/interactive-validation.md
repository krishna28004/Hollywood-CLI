# Interactive validation

## Evidence status

**CONDITIONAL.** The updated Windows ConPTY test passes against the self-contained `win-x64` executable on Windows 10 build 19045 x64. It verifies the seven-panel scene, continued animation after ignored ordinary keys, resize fallback, Ctrl+C exit code 130, and cursor restoration. Direct Windows Terminal/Console Host validation remains unavailable: the command session has redirected standard handles and the Windows UI automation connection timed out before exposing windows.

## Executed checks

| Scenario | Result | Evidence |
|---|---|---|
| CLI help | Pass | `dotnet run --project src/Hollywood.Cli -c Release --no-build -- --help` |
| Deterministic plain snapshot | Pass historically; current themed snapshot executed during this cycle | No VT control output in snapshot mode by design; snapshot is not interactive evidence. |
| Five-theme option parsing | Pass for Retro Amber snapshot | `--snapshot --theme retro --layout dense --seed 42` |
| Sample JSON validation | Pass | `config validate examples/config.json` |
| Explicit missing configuration | Rejected as expected | `--config .\does-not-exist.json --snapshot` exited nonzero with a configuration error. |
| ConPTY launch and first scene | Pass | `dotnet run --project tests/Hollywood.ConPty -c Release --no-build -- artifacts/win-x64/hollywood.exe`; frame header captured within 5 seconds. |
| Animation progression | Pass | 7,590 additional VT output bytes captured over a 700 ms interval in the latest successful run. This is an activity check, not a performance benchmark. |
| Resize 80×24 → 42×12 → 12×5 → 120×30 | Pass | Tiny viewport displayed the compact `RESIZE` fallback; returning to 120×30 redrew the scene. |
| Ordinary keys and Escape ignored | Pass | Letters, Space, and Escape were sent; the process stayed active and output continued changing. |
| Ctrl+C | Pass in ConPTY | ETX (`0x03`) input caused graceful shutdown with exit code 130. Visible-host Ctrl+C behavior remains to be checked. |
| Cursor restoration output | Pass in ConPTY | Cursor-show sequence was captured after Ctrl+C. ConPTY consumes alternate-screen mode changes internally, so the raw stream does not expose the entry/leave sequences as a pair. |
| Forced write failure, slow-reader backpressure | Not tested | Injected writer failure and constrained-output tests remain necessary. |

## Required next validation

The current harness drains output continuously, bounds capture at 1 MB, applies timeouts, records exit codes, and terminates only its child process during failure cleanup. It covers ordinary-key handling, resize, and ETX-triggered Ctrl+C shutdown. Follow with direct Windows Terminal and Console Host checks when a visible host is available. Do not treat snapshots or fake terminal adapters as proof of direct-host compatibility.

The harness uses Windows `CreatePseudoConsole`/`ResizePseudoConsole` APIs. Microsoft's API documentation lists Windows 10 version 1809 as the minimum desktop client and requires synchronous pipe handles; the implementation keeps the streams synchronous while draining output on a separate task ([CreatePseudoConsole](https://learn.microsoft.com/en-us/windows/console/createpseudoconsole), [ConPTY session guidance](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session)).
