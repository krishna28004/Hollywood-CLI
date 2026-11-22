# ADR 0001: Isolate native terminal behavior behind adapters

- Status: Accepted for implementation; POSIX runtime validation pending.
- Date: 2026-11-23.

## Context

The original CLI directly called a Win32-only `TerminalLease` from its rendering loop. The visual engine, module model, layout solver, deterministic simulation, and VT diff renderer are platform-neutral. Linux and macOS also need native terminal sizing, interactive input, cancellation, and restoration without WSL, Docker, shell helpers, or duplicated engine code.

## Decision

Keep the .NET 10 engine and renderer. Define an `ITerminalSession` contract and a factory selected by runtime OS. Retain Win32 console APIs in `WindowsTerminalSession`. Implement Linux and macOS through one `PosixTerminalSession` using .NET Console key/dimension APIs, ANSI/VT output, `Console.CancelKeyPress`, and `PosixSignalRegistration` for SIGTERM. Use platform-appropriate configuration paths under one JSON schema.

Do not change termios through direct P/Invoke in the initial adapter. Linux and Darwin have distinct terminal ABI layouts; .NET already implements terminal key input and process signal behavior. The PTY harness is the experiment that will determine whether this abstraction is sufficient. Do not run `stty` or terminal shell commands.

## Consequences

- Shared rendering and modules remain unchanged and operate on one canvas.
- Native input, viewport, signal, and cleanup behavior has a clear ownership boundary.
- The POSIX code stays simpler and avoids ABI/structure drift, but its per-key polling and resize behavior must be proven on Linux and macOS PTYs.
- The adapter intentionally uses broadly supported xterm-compatible sequences rather than claiming full terminfo negotiation.
- A POSIX host test is required before treating Linux/macOS as supported releases.

## Alternatives considered

1. Rewrite around a cross-platform third-party TUI library: rejected for this phase because it adds a dependency and requires reworking the existing buffer renderer before the platform boundary is proven.
2. Share Win32 `TerminalLease` behavior with Unix conditional branches: rejected because native APIs, resource ownership, and cleanup semantics would become entangled.
3. Direct `termios` P/Invoke in shared code: rejected because Linux/macOS native structures and constants must be declared separately; .NET provides terminal-aware key APIs.
4. Launch shell utilities (`stty`, `tput`, `script`) in the application: rejected due subprocess/persistence/quoting complexity and the requirement to avoid shell-dependent runtime behavior.

## Validation required

- Build and run the PTY integration harness on Ubuntu x64/ARM64 and macOS Intel/Apple Silicon runners.
- Verify ordinary keys are ignored, resize works, Ctrl+C/SIGINT and SIGTERM shut down, and terminal state is restored.
- Verify Windows x64 ConPTY regression after every shared CLI change; retain Windows ARM64 CI coverage.
- Follow with direct real-terminal testing; PTY success is not a substitute for GNOME Terminal, Konsole, SSH/tmux, Terminal.app, or iTerm2 validation.

## References

- [.NET Console APIs](https://learn.microsoft.com/dotnet/api/system.console)
- [POSIX signal registration](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.posixsignalregistration.create)
- [SIGTERM default behavior change in .NET 10](https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/10.0/sigterm-signal-handler)
- [POSIX termios reference](https://man7.org/linux/man-pages/man3/termios.3.html)
