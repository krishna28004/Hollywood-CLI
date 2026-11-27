# Release readiness audit

**Audit date:** 2026-11-27

**Decision:** Development preview; not ready for a general-user release.

## What another user can do today

The repository is public and can be cloned. A technically comfortable user with Git and the .NET 10 SDK can build and run the current source using the commands in the root README. This is a developer preview; it does not install a persistent `hollywood` command.

There are no GitHub Releases or supported downloadable packages. The Windows installer and ZIPs in the developer checkout are local validation artifacts; they are unsigned and must not be presented as downloads to the public. CI artifacts are temporary build evidence, not supported releases.

## Evidence checked

- Public repository: `https://github.com/krishna28004/Hollywood-CLI`.
- GitHub Releases API returned an empty release list.
- Latest public CI run checked: run 19, commit `f9c9662847270a7b90a005d316024ea26d3f1a4a`, dated 2026-11-25.
- Windows x64 and Windows ARM64 workflow jobs passed, including their configured Windows integration steps.
- Linux x64, Linux ARM64, macOS Intel, and macOS Apple Silicon builds, shared regression harnesses, snapshots, and self-contained publish steps passed. Their PTY step failed because `tests/posix-pty.py` expected the obsolete mixed-case strings `Ubuntu 24.04` and `Hollywood demo`; the application emits `UBUNTU 24.04 // HOLLYWOOD SIM`. The test expectation has now been corrected, but the corrected workflow has not yet run.
- Local Windows evidence is recorded in [installation progress](installation-progress.md), [compatibility](compatibility.md), and [interactive validation](interactive-validation.md). It covers Windows 10 x64; it does not establish all terminal-host behavior.
- No root `LICENSE` file exists. Choose and add a license before inviting public use, reuse, or redistribution. Do not guess the owner’s license choice.

## Readiness by audience

| Audience/use | Status | What is missing |
| --- | --- | --- |
| Developer evaluating source on Windows | Conditional preview | Git, .NET 10 SDK, build/run steps; direct Windows Terminal and classic Console Host checks remain. |
| Ordinary Windows user | Not ready | No published installer, signing, or official download; currently tested installer is unsigned and local only. |
| Linux user | Not ready | Corrected PTY CI rerun, terminal-emulator validation, supported distro/libc declaration, and release archive. |
| macOS user | Not ready | Corrected PTY CI rerun, terminal validation, signed/notarized distribution, and release archive. |
| Third-party reuse/redistribution | Not authorized by repository license | Add an owner-approved license and review dependency notices. |

## Release gates

1. Owner selects and adds a project license; dependency and installer notices are reviewed.
2. Corrected CI run passes all six build/test/package lanes, and artifacts are inspected.
3. Windows x64 setup and portable ZIP are built reproducibly; installer signing identity is established before describing a download as trusted.
4. Windows installation is validated for a standard user, command lookup in new PowerShell/CMD/Git Bash sessions, upgrade/repair/uninstall, and reboot persistence. A reboot result must be observed, not inferred from registry state.
5. Linux and macOS PTY checks pass, then representative real terminal sessions and supported OS/architecture combinations are recorded.
6. Release notes, install/uninstall steps, SHA-256 checksums, provenance, and known limitations are prepared. Publish only after the release owner authorizes it.

## Next action

Push the corrected PTY expectation and documentation, wait for the full GitHub Actions matrix, then fix any real failures before preparing an explicitly versioned release. Do not label the application generally available until the gates above are satisfied.
