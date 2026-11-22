# Installation

## Current availability

There is no public release or package-manager listing yet. The Windows setup and archives built from this checkout are local validation artifacts, not signed public downloads. The repository has no `LICENSE` file; do not redistribute artifacts until the project owner chooses and adds a license.

## Windows 10/11 setup (local build)

The developer build script creates a self-contained per-user installer and portable archives for x64 and ARM64:

```powershell
./packaging/windows/build-installer.ps1
```

The setup output is `artifacts/installer/releases/windows/Hollywood-Setup.exe`. To install, extract the downloaded release archive, double-click `Hollywood-Setup.exe`, accept the per-user destination, then open a new PowerShell or Command Prompt and run `hollywood`. If that command is already provided by another application, setup selects `hollywood-cli` and explains the choice. Setup does not require administrator privileges or change execution policy. The uninstaller removes the command-path entry it owns and preserves `%LOCALAPPDATA%\Hollywood` configuration.

The local installer is unsigned and may trigger Windows reputation warnings. Do not bypass security warnings for artifacts from an unverified source. Release signing credentials and a declared project license are required before public distribution.

The portable archives are:

- `hollywood-win-x64.zip`
- `hollywood-win-arm64.zip`

Extract a matching archive into a user-owned folder and run `hollywood.exe`. Portable archives do not edit PATH or registry state.

## Linux

No `.deb` or `.rpm` package is currently produced. The cross-platform CI builds portable glibc-targeted archives for x64 and ARM64. Extract the matching verified archive into a user-owned directory and run `./hollywood`. To add a command, place a symlink named `hollywood-cli` in a user-owned PATH directory such as `~/.local/bin`; the original Linux Hollywood project may own `/usr/bin/hollywood`, which must not be overwritten. Linux distribution and glibc compatibility still require qualification on each claimed target.

## macOS

No `.pkg`, Homebrew formula, signed, or notarized macOS release is currently available. CI targets Intel and Apple Silicon portable archives. A downloaded unsigned build is a development artifact and may be blocked or warned on by Gatekeeper. Do not disable Gatekeeper to run it. A frictionless release requires Developer ID signing, notarization, staple verification, and clean-machine testing.

## Configuration

Configuration uses one versioned JSON schema. Windows stores it under `%LOCALAPPDATA%\Hollywood`; Linux uses `$XDG_CONFIG_HOME/hollywood` or `~/.config/hollywood`; macOS uses the documented Application Support location. Uninstallers must preserve user configuration.

## Package managers

No WinGet package ID, Homebrew formula, or Linux package repository has been published. Names and install commands will be added only after the corresponding package is actually published and verified.
