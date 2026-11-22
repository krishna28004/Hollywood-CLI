# Terminal restoration test status

**Status: PARTIALLY VERIFIED through ConPTY; direct host state inspection remains untested.** The implementation is in `src/Hollywood.Cli/Terminal/TerminalLease.cs`.

Current code inspection shows that it saves input/output modes, enters the alternate screen, hides the cursor, and attempts best-effort restoration on disposal. Acquisition rollback calls the same disposal path. This is code inspection only; no sentinel-screen or host restoration test has run.

| Case | Status | Evidence needed |
|---|---|---|
| Normal Q/Esc exit | Pass in ConPTY | Q and Escape each returned 0; cursor-show sequence captured. Direct shell usability and original mode values were not observed. |
| Ctrl+C | Not tested | Deliver an actual console control event; distinguish from a literal `0x03` byte. |
| Partial acquisition failure | Not tested | Inject failures after each console-mode/VT setup step and verify rollback. |
| Renderer write failure | Not tested | Inject a failing writer and verify original exception remains visible while restoration is attempted. |
| Repeated process launch/exit | Pass in ConPTY | Two independent pseudoconsole sessions completed Q and Escape shutdown. Repeated `Dispose()` on the same lease is still not directly tested. |
| Forced process termination | Not guaranteed | Document host-dependent recovery; no watchdog or persistent recovery process is intended. |

The harness does not assert alternate-screen escape pairs because ConPTY processes those modes internally. A direct visible host test with a recognizable preexisting screen sentinel is still required.

Known concern: restoration errors are currently best effort and swallowed by `Dispose`; recording cleanup failures without masking the initiating failure remains a hardening task.
