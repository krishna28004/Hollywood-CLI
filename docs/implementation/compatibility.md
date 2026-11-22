# Compatibility evidence

| OS | Host | Shell | Architecture | Status |
|---|---|---|---|---|
| Windows 10 build 19045 | PowerShell command session; standard handles redirected | PowerShell 7.6.9 | x64 | Environment metadata observed. Release build, unit harness, help, themed snapshot, and sample config validation executed. Interactive mode untested. |
| Windows 10 build 19045 | Win32 ConPTY pseudo-console | .NET 10 harness; self-contained Hollywood process | x64 | Automated scene, animation output, Q/Escape exit, viewport resize including 12×5, and cursor-show output passed. This does not prove direct Windows Terminal/Console Host integration. |
| Windows 10 build 19045 | Windows UI automation surface | — | x64 | Initialization timed out; no app window was controlled. |
| Windows 11 | Windows Terminal and Console Host | PowerShell 7 and Windows PowerShell 5.1 | x64 | Not tested. |
| Windows 10 LTSC 2021 / 22H2 edition-specific lanes | Console Host; Windows Terminal only if available | PowerShell 7 and 5.1 | x64 | Not tested. |
| Windows 11 | Windows Terminal | PowerShell 7 | Arm64 | Not tested. |

Do not interpret an OS string from this execution environment as evidence that its terminal session is interactive. No compatibility claim is made until an actual host test is recorded. See [interactive validation](interactive-validation.md).
