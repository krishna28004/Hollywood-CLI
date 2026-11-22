# Compatibility evidence

| OS | Host | Shell | Architecture | Status |
|---|---|---|---|---|
| Windows 10 build 19045 | PowerShell command session; standard handles redirected | PowerShell 7.6.9 | x64 | Environment metadata observed. Release build, unit harness, help, themed snapshot, and sample config validation executed. Interactive mode untested. |
| Windows 10 build 19045 | Win32 ConPTY pseudo-console | .NET 10 harness; self-contained single-file Hollywood process | x64 | Seven-panel scene and animation passed; letters, Space, and Escape were ignored while output continued; 12×5 resize fallback appeared; Ctrl+C exited with 130 and cursor visibility was restored. This does not prove direct Windows Terminal/Console Host integration. |
| Windows 10 build 19045 | Windows UI automation surface | — | x64 | Initialization timed out; no app window was controlled. |
| Windows 11 | Windows Terminal and Console Host | PowerShell 7 and Windows PowerShell 5.1 | x64 | Not tested. |
| Windows 10 LTSC 2021 / 22H2 edition-specific lanes | Console Host; Windows Terminal only if available | PowerShell 7 and 5.1 | x64 | Not tested. |
| Windows 11 | Windows Terminal | PowerShell 7 | Arm64 | Not tested. |
| Linux Ubuntu 22.04 x64 | Planned GitHub Actions runner and stdlib PTY harness | .NET 10 | x64 | Adapter/build/test path added; has not run in this environment. |
| Linux Ubuntu 22.04 ARM64 | Planned GitHub Actions runner and stdlib PTY harness | .NET 10 | Arm64 | Adapter/build/test path added; has not run in this environment. |
| macOS 15 Intel | Planned GitHub Actions runner and stdlib PTY harness | .NET 10 | x64 | Adapter/build/test path added; has not run in this environment. |
| macOS 15 Apple Silicon | Planned GitHub Actions runner and stdlib PTY harness | .NET 10 | Arm64 | Adapter/build/test path added; has not run in this environment. |

Do not interpret an OS string from this execution environment as evidence that its terminal session is interactive. No compatibility claim is made until an actual host test is recorded. See [interactive validation](interactive-validation.md).
