# Installation

## Availability

Hollywood CLI has no public release or package-manager listing yet. This guide covers release candidate testing. Use an artifact from a run you trust and compare its SHA-256 with the corresponding manifest before extracting or running it. Local and CI Windows builds are unsigned. macOS CI artifacts are development builds pending signing and notarization.

## Windows setup

1. Open the verified `Hollywood-Setup.exe` candidate. The same setup contains x64 and ARM64 payloads and selects the appropriate one.
2. Keep the default per-user location or choose a directory you own. Setup registers the command and keeps settings on upgrade.
3. Open a new PowerShell, CMD, or Git Bash window and type `hollywood`. Setup displays `hollywood-cli` if it detects a collision.

To repair or upgrade, run the new setup. To remove it, use **Settings → Apps → Installed apps**. Hollywood settings are preserved.

For portable use, extract the matching `hollywood-win-x64.zip` or `hollywood-win-arm64.zip` and run `./hollywood.exe` in PowerShell from that folder. For development setup builds, run `./packaging/windows/build-installer.ps1` from the repository. Its output is in `artifacts/installer/releases/windows/`.

## Linux and macOS archives

1. Download the candidate for your OS and CPU and verify its checksum against the manifest. On macOS, use the development archive only in a test environment authorized to evaluate it.
2. Extract the archive. Open a terminal in the extracted directory and run `./install.sh`.
3. Open a new terminal and type `hollywood`, or `hollywood-cli` if the installer reported a collision.

The installer needs `~/.local/bin` or `~/bin` already on your shell’s PATH. If neither is present, you can run `./hollywood` from the extracted directory immediately. For a persistent command, enable `~/.local/bin` using your shell or distribution’s user PATH setup, open a new login shell, and retry. The installer does not change shell startup files. Rerun the installer from a newer archive to upgrade. To uninstall, run `~/.local/share/hollywood-cli/uninstall.sh --uninstall`.

The Linux runtime targets glibc. CI exercises Ubuntu 22.04 x64 and ARM64 and does not qualify older glibc versions, Alpine, or other distributions. macOS candidates target Intel and Apple Silicon separately. Signed and notarized macOS downloads and real Gatekeeper tests are still pending.

## Configuration

Uninstallers preserve settings. Run `hollywood config path` for the active path. The defaults are `%LOCALAPPDATA%\Hollywood\config.json` (Windows), `$XDG_CONFIG_HOME/hollywood/config.json` or `~/.config/hollywood/config.json` (Linux), and `~/Library/Application Support/Hollywood/config.json` (macOS).
