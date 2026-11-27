# Hollywood CLI

Hollywood CLI turns an interactive terminal into a fictional, animated operations dashboard inspired by the multi-pane Linux `hollywood` experience. It is built with .NET and runs its panels in one terminal canvas.

**Safety:** Hollywood displays synthetic activity. It does not scan networks, send packets, access credentials, execute displayed code, or monitor other applications. System readings shown in the dashboard are simulated.

![Hollywood CLI operations dashboard design concept](docs/design/hollywood-master-120x30.png)

*Design concept.* See a [plain-text snapshot from the application](docs/design/actual-120x30-snapshot.txt).

## Quick install

Hollywood CLI is in development preview. The [GitHub Releases page](https://github.com/krishna28004/Hollywood-CLI/releases) is where official downloads will appear. There are currently no published installers, portable releases, or package-manager listings. For package testing and uninstall instructions, see the [installation guide](docs/installation.md).

## Launch

After installing an official package, open a new terminal and type `hollywood`. If the installer reported a command conflict, type `hollywood-cli` instead. Press `Q`, `Esc`, or `Ctrl+C` to exit.

## Controls

| Key | Action |
| --- | --- |
| `Q` or `Esc` | Exit |
| `Ctrl+C` | Cancel and restore the terminal |
| `Space` | Pause or resume |
| `T` | Cycle themes |
| `L` | Cycle layouts |
| Other keys | Increase simulated activity while pressed; key contents are not stored |

When typing stops, animation gradually returns to a quieter idle state. Use `--keyboard-reactive off` to disable this behavior.

## Themes and options

Hollywood includes Classic, Cyberpunk, SOC, Matrix, and Retro themes. Common options:

```text
--theme classic|cyberpunk|soc|matrix|retro
--layout auto|focus|dense
--panels 2|3|6|7|10
--speed slow|normal|fast
--fps 1..60
--keyboard-reactive on|off
--reduced-motion
--seed NUMBER
--snapshot
```

Examples:

```powershell
hollywood --theme matrix --speed fast
hollywood --layout dense --keyboard-reactive off
hollywood --snapshot --theme retro --seed 12345
```

`--snapshot` writes plain text and does not start the interactive display. For the complete option list, run:

```powershell
hollywood --help
```

## Configuration

Configuration is optional and is never created or overwritten automatically.

- **Windows:** `%LOCALAPPDATA%\Hollywood\config.json`
- **Linux:** `$XDG_CONFIG_HOME/hollywood/config.json`, or `~/.config/hollywood/config.json`
- **macOS:** `~/Library/Application Support/Hollywood/config.json`

Inspect or validate configuration with:

```text
hollywood config path
hollywood config show
hollywood config validate
```

See [`examples/config.json`](examples/config.json) for themes, layout, speed, enabled modules, reduced motion, and keyboard-reactivity settings.

## Platform testing

| Platform | Evidence |
| --- | --- |
| Windows x64 | Windows 10 x64 installer lifecycle and shell lookup exercised locally. Native CI covers ConPTY and installer checks. |
| Windows ARM64 | Native CI covers build, packaged execution, and ConPTY. Installer and graphical terminal qualification remain pending. |
| Linux x64 and ARM64 | Native Ubuntu 22.04 CI covers the app in interactive pseudo-terminals. Other distributions and terminal emulators need qualification. |
| macOS Intel and Apple Silicon | Native macOS CI covers the app in interactive pseudo-terminals. Trusted download and installation checks remain pending. |

See the [release readiness record](docs/release/public-release-progress.md) for exact runs and remaining checks.

## Build and run from source

Install Git and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to try the development preview from a local checkout.

### Windows PowerShell or Windows Terminal

Open PowerShell and run:

```powershell
git clone https://github.com/krishna28004/Hollywood-CLI.git
Set-Location Hollywood-CLI
dotnet run --project src/Hollywood.Cli
```

In a new PowerShell window, repeat the final command from the cloned folder to start Hollywood again.

### Windows Git Bash

Open Git Bash and run:

```bash
git clone https://github.com/krishna28004/Hollywood-CLI.git
cd Hollywood-CLI
winpty dotnet run --project src/Hollywood.Cli
```

Use `winpty` when running interactively in Git Bash’s mintty terminal.

### Linux or macOS

In an interactive terminal, run:

```bash
git clone https://github.com/krishna28004/Hollywood-CLI.git
cd Hollywood-CLI
dotnet run --project src/Hollywood.Cli
```


### Run the development checks

```powershell
dotnet build Hollywood.sln -c Release
dotnet tests/Hollywood.UnitTests/bin/Release/net10.0/Hollywood.UnitTests.dll
```

The Windows ConPTY interaction harness can be run against a built Windows executable:

```powershell
dotnet tests/Hollywood.ConPty/bin/Release/net10.0/Hollywood.ConPty.dll path\to\hollywood.exe
```

This harness is Windows-only. Unit or ConPTY success does not establish compatibility with every terminal emulator. See [`docs/implementation`](docs/implementation/) for current evidence and pending checks.

## Troubleshooting

- **Command not found after setup** — open a fresh terminal from the Start menu on Windows. If setup reported a collision, use `hollywood-cli`.
- **Shell says this is a redirected session** — open an interactive terminal. For output to a file, add `--snapshot`.
- **Terminal is too small** — enlarge the window or launch with `--panels 2`.
- **Archiving or installation questions** — see the [installation guide](docs/installation.md).

## License and credits

The project owner has not declared a source license. Hollywood CLI is inspired by [Dustin Kirkland’s Hollywood](https://github.com/dustinkirkland/hollywood). Candidate archives include license and attribution files from the .NET runtime pack they bundle.
