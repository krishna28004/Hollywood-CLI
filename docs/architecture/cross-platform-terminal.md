# Cross-platform terminal architecture

Status: **C1 audit complete; C2 shared contract in progress; C3 POSIX adapter implemented but not yet exercised on a POSIX host.** Windows remains the only platform tested in this environment.

## Design boundary

```text
Hollywood.Engine (modules, layout, canvas, deterministic simulation)
                         ^
                         | shared CLI orchestration and VT renderer
                         v
                 ITerminalSession
                   /          \
     WindowsTerminalSession   PosixTerminalSession
       Win32 console APIs     .NET Console + ANSI/VT
```

`ITerminalSession` owns viewport size, key polling, the output writer, and disposal. The factory chooses one adapter at startup. The engine does not reference OS APIs. Windows P/Invoke calls remain confined to `WindowsTerminalSession`; Linux and macOS share `PosixTerminalSession`.

The renderer remains single-threaded and is the only owner of terminal output during a session. Modules continue to update in-process on the central frame loop. The process uses one terminal canvas; it does not create OS panes, pseudo-terminal children, or a thread per visual module.

## POSIX terminal behavior

- Require non-redirected standard input and output for interactive mode. Redirected output is rejected before emitting escape sequences; `--snapshot` remains the plain-text path.
- Reject `TERM=dumb`. Other interactive TERM values use common xterm-compatible alternate-screen and cursor sequences, with color reduced to 16 colors when truecolor/256-color support is not advertised.
- Use `Console.WindowWidth`/`WindowHeight` for viewport polling and `Console.KeyAvailable`/`Console.ReadKey(intercept: true)` for non-echoing key input. Resize is handled by polling dimensions each frame; the renderer clears and redraws when the canvas dimensions change.
- Do not change termios explicitly. .NET owns its terminal-aware key-read behavior, so this application avoids duplicating kernel input-mode transitions and avoids assuming Linux and Darwin share a binary `termios` layout. This is a design choice to validate with PTYs on both systems, not evidence that all terminal hosts behave identically.
- `Console.CancelKeyPress` handles Ctrl+C/SIGINT. On .NET 10, SIGTERM needs explicit registration for graceful application shutdown; a POSIX-only `PosixSignalRegistration` cancels the default termination and signals the shared cancellation token. Both paths exit through the terminal-session disposal path.
- Do not call `stty`, `tput`, or a shell. If PTY tests find a host where the .NET Console APIs cannot provide required behavior, evaluate a small platform-specific native adapter with separately validated Linux and Darwin ABI declarations before adding it.
- On acquisition failure, return a clear error without entering the alternate screen. Disposal emits reset, cursor-show, and alternate-screen-leave sequences on controlled exits. Forced kill or power loss can still prevent cleanup.

## Capability and fallback behavior

| Condition | Interactive behavior |
|---|---|
| Windows interactive console | Existing Win32 mode handling, ConPTY-aware handle fallback, and console input records. |
| Linux/macOS PTY with `TERM=dumb` | Decline interactive mode; recommend `--snapshot`. |
| Redirected stdin or stdout | Decline interactive mode before terminal mutation; snapshots remain available. |
| `NO_COLOR` present | Use monochrome cell styling while retaining cursor/layout control sequences. |
| `COLORTERM=truecolor` or `24bit` | Use truecolor. |
| `TERM` advertises `256color` | Use the renderer's 256-color palette. |
| Other non-dumb POSIX TERM | Use 16-color styling. |
| Small terminal | Existing compact resize prompt and adaptive panel count. |
| Resize | Poll current dimensions and invalidate/repaint the new canvas. |

This is conservative environment-based detection, not terminfo negotiation. Windows Terminal, Terminal.app, iTerm2, GNOME Terminal, Konsole, xterm, SSH, tmux, and screen still require the real-host matrix before product compatibility is claimed.

## Configuration locations

- Windows: `%LOCALAPPDATA%/Hollywood/config.json`.
- Linux: `$XDG_CONFIG_HOME/hollywood/config.json` when XDG_CONFIG_HOME is absolute; otherwise `~/.config/hollywood/config.json`.
- macOS: `~/Library/Application Support/Hollywood/config.json`.

All platforms use the same schema version. Startup reads an existing file and never creates or overwrites it.

## Verified facts, assumptions, and validation

Verified from official documentation: .NET exposes redirected-stream checks and key-availability/read APIs; .NET's POSIX signal API can cancel SIGTERM on Unix; .NET 10 no longer provides the prior default SIGTERM handler; and self-contained/single-file outputs remain RID-specific. These facts do not prove the target terminals behave identically.

Current assumptions to validate:

1. `Console.KeyAvailable` plus `ReadKey` provides sufficiently responsive, non-echoing keys in Linux and macOS PTYs without application-owned termios changes.
2. The alternate-screen and cursor sequences used here work in the advertised terminal families; tmux/screen configuration may affect them.
3. .NET's `Console.WindowWidth` and `WindowHeight` observe SIGWINCH-driven PTY size changes on both Linux and macOS.
4. ConPTY/Windows behavior remains unchanged after introducing the factory and interface.

Experiments: run the Python stdlib PTY test on Ubuntu and macOS runners; run ConPTY on Windows x64 and ARM64; then inspect Windows Terminal, Console Host, GNOME Terminal, Konsole, SSH/tmux, Terminal.app, and iTerm2 manually. The harness verifies scene output, animation, ignored ordinary keys, resize, Ctrl+C, and cursor-show output. It cannot substitute for those visible terminal checks.

## Deployment notes

Self-contained single-file publishing is the initial package target; Native AOT remains a separate measured experiment. For Linux, artifacts built on Ubuntu 22.04 will be qualified against its glibc baseline and tested on older/newer representative distributions before broader compatibility claims. macOS archives are not currently signed or notarized; Gatekeeper behavior and frictionless distribution are release gates.

## References

- [.NET Console API](https://learn.microsoft.com/dotnet/api/system.console)
- [.NET Console.KeyAvailable](https://learn.microsoft.com/dotnet/api/system.console.keyavailable)
- [PosixSignalRegistration.Create](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.posixsignalregistration.create)
- [.NET 10 SIGTERM behavior change](https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/10.0/sigterm-signal-handler)
- [.NET single-file deployment](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview)
- [.NET Native AOT deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [GitHub-hosted runner operating systems and architectures](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
- [POSIX termios reference](https://man7.org/linux/man-pages/man3/termios.3.html) (consulted; explicit native termios mutation is intentionally deferred)
