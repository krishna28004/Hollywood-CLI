# Hollywood CLI

A native .NET character-cell terminal simulation inspired by Linux Hollywood. It renders fictional operations dashboards inside one terminal canvas on Windows, Linux, and macOS. It performs no network traffic, security scanning, command execution, or file changes during normal operation.

## Requirements

- .NET 10 SDK to build from source
- Windows 10/11, Linux, or macOS for interactive mode
- An interactive terminal with VT/ANSI support; redirected output is supported through `--snapshot`
- .NET 10 SDK only when building from source; planned release archives are self-contained

**Compatibility status:** Windows 10 build 19045 x64 ConPTY has passed the current interactive smoke test. The POSIX terminal adapter and CI matrix are in development; Linux and macOS runtime tests have not yet run, and direct Windows Terminal/Console Host checks remain pending. See [cross-platform terminal architecture](docs/architecture/cross-platform-terminal.md), [interactive validation](docs/implementation/interactive-validation.md), [compatibility](docs/implementation/compatibility.md), and [known defects](docs/implementation/known-defects.md).

## Build and run

```powershell
dotnet build Hollywood.sln -c Release
dotnet run --project src/Hollywood.Cli
dotnet run --project src/Hollywood.Cli -- --help
dotnet run --project src/Hollywood.Cli -- --version
dotnet run --project src/Hollywood.Cli -- --snapshot --theme retro --layout focus --seed 12345
dotnet run --project tests/Hollywood.UnitTests -c Release
dotnet run --project tests/Hollywood.ConPty -c Release -- artifacts/win-x64/hollywood.exe
```

On Linux and macOS, use `dotnet run --project src/Hollywood.Cli` from an interactive terminal while developing. Native self-contained archives are not yet published; CI is being expanded to build and test the six target operating-system/architecture combinations.

The executable supports five themes (`classic`, `cyberpunk`, `soc`, `matrix`, `retro`), layouts (`auto`, `focus`, `dense`), panel counts (2, 3, 6, 10), speed (`slow`, `normal`, `fast`), frame caps (1–60), reduced motion, and deterministic seeds. Interactive controls are `Q` or `Esc` to quit, `Space` to pause/resume, `T` to cycle themes, and `L` to cycle panel counts. `--snapshot` writes plain text without terminal control sequences.

## Configuration

The optional JSON file is `%LOCALAPPDATA%\Hollywood\config.json` on Windows, `$XDG_CONFIG_HOME/hollywood/config.json` (or `~/.config/hollywood/config.json`) on Linux, and `~/Library/Application Support/Hollywood/config.json` on macOS. Normal startup reads it if present and never creates or overwrites it. Command-line values override file values; `--no-config` bypasses it and `--config PATH` selects a file.

```powershell
hollywood config path
hollywood config show
hollywood config validate [PATH]
```

Schema version 1 supports theme, layout, panel count, speed, frame-rate limit, deterministic seed, color mode, ASCII glyph mode, reduced motion, simulated telemetry mode, and enabled modules. The parser rejects unknown fields, duplicate keys, unsupported versions, invalid values, files larger than 64 KiB, and excessive JSON nesting. A ready-to-copy example is in [examples/config.json](examples/config.json). Panel rotation and Unicode glyphs are not implemented.

## Distribution

Self-contained, single-file artifacts are produced per runtime identifier. For example, build the Windows x64 artifact with:

```powershell
dotnet publish src/Hollywood.Cli/Hollywood.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true --configfile NuGet.Config -o artifacts/win-x64
```

Planned release targets are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`. Portable ZIP/tar.gz packages and native-runner CI evidence are being prepared. No package manager package or public release is available yet. Linux compatibility is constrained by the build artifact's glibc baseline; macOS archives are not signed or notarized yet. See [installation status and portable workflows](docs/installation.md) for the planned user-owned installation process and command-collision guidance.

## Safety and current limitations

All ten visual modules generate synthetic content. Network addresses are documentation examples. System metrics remain simulated. Unicode glyph mode and live telemetry are not implemented. The runtime targets .NET 10. Windows PowerShell 5.1 can launch the published native executable but is not a build/runtime target. Linux/macOS PTY validation and real terminal host checks remain release gates.

## Project status

This repository is a development preview, not a production release. Verification evidence and remaining gaps are tracked under [`docs/implementation`](docs/implementation/).
