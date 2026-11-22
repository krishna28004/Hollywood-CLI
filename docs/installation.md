# Installation status and portable workflows

**No public packages or installer exist yet.** The commands below describe the intended release workflow; they are not package-manager install commands that work today. Do not download release archives from unofficial mirrors.

## Release archive names

| Platform | Archive |
|---|---|
| Windows x64 | `hollywood-win-x64.zip` |
| Windows ARM64 | `hollywood-win-arm64.zip` |
| Linux x64 | `hollywood-linux-x64.tar.gz` |
| Linux ARM64 | `hollywood-linux-arm64.tar.gz` |
| macOS Intel | `hollywood-osx-x64.tar.gz` |
| macOS Apple Silicon | `hollywood-osx-arm64.tar.gz` |

CI is being prepared to generate these per-RID self-contained archives with a SHA-256 sidecar and artifact metadata. A passing build artifact is not yet a published, signed release.

## Intended portable use

### Windows 10/11

1. Download the matching ZIP from a future verified release and extract it to a folder you own, for example `%LOCALAPPDATA%\Programs\Hollywood`.
2. Open PowerShell in that folder and run `.hollywood.exe`.
3. To use `hollywood` from any folder, add that exact per-user install folder to your user PATH through Windows Settings. The archive will not edit PATH or registry state.
4. Remove the extracted folder and the optional user PATH entry to uninstall.

### Linux (Ubuntu/Debian and other distributions)

1. Download the matching x64 or ARM64 archive from a future verified release.
2. Extract it into a user-owned directory, for example `~/.local/opt/hollywood`.
3. Run `./hollywood` from that directory.
4. For command lookup, copy or symlink the binary into a user-owned directory already on PATH, such as `~/.local/bin`; do not overwrite a package-owned `/usr/bin/hollywood`.
5. Remove the user-owned extracted files/symlink to uninstall.

Linux artifacts will target glibc, not every libc. The planned Ubuntu 22.04 build baseline must be confirmed in CI; Ubuntu 20.04 or other older glibc systems may be incompatible, and Alpine/musl is outside the initial target unless separately built and tested.

### macOS Intel and Apple Silicon

1. Download the archive matching the Mac's CPU architecture from a future verified release.
2. Extract it into a user-owned directory and run `./hollywood` from Terminal or iTerm2.
3. Optionally add a user-owned symlink under `~/.local/bin` if that directory is already on PATH.
4. Remove only the extracted directory/symlink to uninstall.

The current release workflow does not sign or notarize macOS binaries. Gatekeeper may warn or block an unsigned archive. A release is not ready to claim frictionless macOS installation until Developer ID signing, notarization, staple verification, and a clean-user download test are in place. Do not disable Gatekeeper or security protections to run an unverified binary.

## Command-name collision

The Linux Hollywood package may already install a `hollywood` executable. Before adding this application to PATH, check `command -v hollywood` (Linux/macOS) or `Get-Command hollywood` (PowerShell). Do not overwrite an existing package-owned binary. Use an isolated user-owned folder and resolve PATH precedence deliberately; a future installer must detect an existing command and provide a distinct alias such as `hollywood-cli` rather than replacing it silently.

## Package-manager goals

These are not currently available:

- Windows: a verified WinGet manifest after stable release and checksum validation.
- macOS: a Homebrew formula after signing/notarization and release automation are established.
- Linux: distribution packages or maintained repositories only after libc and distribution compatibility are qualified.

No package ID, formula, or Linux repository name is published yet.
