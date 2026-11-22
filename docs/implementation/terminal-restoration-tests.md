# Terminal restoration test status

**Status: Windows partially verified through ConPTY; POSIX PTY and direct host state inspection remain untested.** Platform adapters are in `src/Hollywood.Cli/Terminal/`.

Current code inspection shows that it saves input/output modes, enters the alternate screen, hides the cursor, and attempts best-effort restoration on disposal. Acquisition rollback calls the same disposal path. This is code inspection only; no sentinel-screen or host restoration test has run.

| Case | Status | Evidence needed |
|---|---|---|
| Ordinary keys / Escape | Pass in ConPTY | Letters, Space, and Escape were sent; the scene remained active and animation output continued. |
| Ctrl+C | Pass in ConPTY | Sending ETX (`0x03`) caused the app's cancel path to exit with code 130 and restore cursor visibility. Direct Windows Terminal/Console Host input remains unverified. |
| Partial acquisition failure | Not tested | Inject failures after each console-mode/VT setup step and verify rollback. |
| Renderer write failure | Not tested | Inject a failing writer and verify original exception remains visible while restoration is attempted. |
| Repeated process launch/exit | Pass in ConPTY | Independent pseudoconsole sessions completed after Ctrl+C. Repeated `Dispose()` on the same lease is still not directly tested. |
| Forced process termination | Not guaranteed | Document host-dependent recovery; no watchdog or persistent recovery process is intended. |
| POSIX ignored keys/Ctrl+C/SIGTERM and restore | Harness implemented; pending native runners | Run `tests/posix-pty.py` on Linux and macOS; capture exit status and cursor restoration. |

The harness does not assert alternate-screen escape pairs because ConPTY processes those modes internally. A direct visible host test with a recognizable preexisting screen sentinel is still required.

Known concern: restoration errors are currently best effort and swallowed by `Dispose`; recording cleanup failures without masking the initiating failure remains a hardening task.
