# Hollywood CLI

A native Windows character-cell terminal simulation inspired by Linux Hollywood. It renders fictional operations dashboards inside one terminal canvas. It performs no network traffic, security scanning, command execution, or file changes during normal operation.

## Requirements

- .NET 10 SDK to build from source
- Windows 10/11 for interactive mode
- Windows Terminal or Windows Console Host with an interactive console

The ConPTY smoke harness has passed on Windows 10 build 19045 x64. Direct Windows Terminal and Console Host behavior remains unverified. See [interactive validation](docs/implementation/interactive-validation.md), [compatibility](docs/implementation/compatibility.md), and [known defects](docs/implementation/known-defects.md).

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

The executable supports five themes (`classic`, `cyberpunk`, `soc`, `matrix`, `retro`), layouts (`auto`, `focus`, `dense`), panel counts (2, 3, 6, 10), speed (`slow`, `normal`, `fast`), frame caps (1–60), reduced motion, and deterministic seeds. Interactive controls are `Q` or `Esc` to quit, `Space` to pause/resume, `T` to cycle themes, and `L` to cycle panel counts. `--snapshot` writes plain text without terminal control sequences.

## Configuration

The optional JSON file is `%LOCALAPPDATA%\Hollywood\config.json`. Normal startup reads it if present and never creates or overwrites it. Command-line values override file values; `--no-config` bypasses it and `--config PATH` selects a file.

```powershell
hollywood config path
hollywood config show
hollywood config validate [PATH]
```

Schema version 1 supports theme, layout, panel count, speed, frame-rate limit, deterministic seed, color mode, ASCII glyph mode, reduced motion, simulated telemetry mode, and enabled modules. The parser rejects unknown fields, duplicate keys, unsupported versions, invalid values, files larger than 64 KiB, and excessive JSON nesting. A ready-to-copy example is in [examples/config.json](examples/config.json). Panel rotation and Unicode glyphs are not implemented.

## Distribution

The self-contained Windows x64 executable is produced with:

```powershell
dotnet publish src/Hollywood.Cli/Hollywood.Cli.csproj -c Release -r win-x64 --self-contained true --configfile NuGet.Config -o artifacts/win-x64
```

Local packaging is in progress. No package has been publicly published.

## Safety and current limitations

All ten visual modules generate synthetic content. Network addresses are documentation examples. System metrics remain simulated. Unicode glyph mode and live telemetry are not implemented. The runtime targets .NET 10; Windows PowerShell 5.1 is not a supported build/runtime target for the interactive binary. Host-specific input, resize, output backpressure, and terminal restoration still require ConPTY and direct-host validation before a release claim.

## Project status

This repository is a development preview, not a production release. Verification evidence and remaining gaps are tracked under [`docs/implementation`](docs/implementation/).
