# Installation implementation progress

Updated: 2026-11-23

## Windows installer status

- Added an Inno Setup project for a standard per-user installation at `%LOCALAPPDATA%\Programs\Hollywood`.
- Added a build script that publishes self-contained Windows x64 and ARM64 executable payloads, builds portable ZIPs with SHA-256 sidecars, embeds runtime and installer notices, compiles setup, and emits artifact metadata.
- Added command-owner detection across process, user, and machine PATH values. If `hollywood` is already owned, setup selects `hollywood-cli`; if that name is occupied too, setup stops without replacing either command.
- Added a generated `.cmd` launcher, persistent HKCU PATH registration, environment-change notification, installed executable version check, optional launch action, Start Menu shortcut, and uninstall cleanup that preserves unrelated PATH entries and user configuration.
- Added an installation lifecycle harness for fresh/repeated installs, spaced and non-ASCII paths, command lookup from CMD and PowerShell, repair, optional upgrade/failure fixtures, collision behavior, and uninstall.
- Added Windows setup instructions and a Windows x64 CI step for building and exercising setup.

## Build and test evidence

- Local host: Windows 10 x64, PowerShell; Inno Setup 6.7.3.
- `dotnet publish` completed for `win-x64` and `win-arm64` as self-contained single-file executables.
- Inno Setup compiled `Hollywood-Setup.exe` successfully with both architecture payloads embedded. Output is unsigned.
- The non-elevated lifecycle passed at a path containing spaces and `Ω`: fresh installation, actual command launch from an unrelated directory using CMD, Windows PowerShell 5.1 and PowerShell 7 (both were present), repeated installation, missing-executable repair, PATH de-duplication, configuration preservation, and uninstall. Repeat-install and repair checks were rerun against the final setup build.
- A second lifecycle passed with a pre-existing `hollywood.cmd` earlier on process PATH: setup selected `hollywood-cli`, command invocation worked, and the original command file remained unchanged after uninstall.
- The same lifecycle passed at the default `%LOCALAPPDATA%\Programs\Hollywood` location.
- The harness restored the original HKCU PATH after each run. Its tests ran with a non-elevated Windows token. The first test attempt exposed and corrected a test assertion type error; subsequent complete runs passed.
- Upgrade fixture 0.2.1 installed over 0.2.0 and the command continued to launch. A deliberately invalid 0.2.2 package returned custom error code 4, logged payload validation failure, left no 0.2.2 payload, and preserved the working 0.2.1 launcher. Uninstall then completed and restored PATH.
- Final local output: `artifacts/installer/releases/windows/Hollywood-Setup.exe` (unsigned, 47 MB). SHA-256: `e1f660f589566303cb7672a829f20b83d40b4aee6f5ca6de1e57cffe2afc6719` (see generated `artifact-metadata.json` and sidecar).
- Portable archive SHA-256 values: x64 `a1c1c72631608cbd88544097432514aa1fe2e339f30c7beaa41aa370e774a1a7`; ARM64 `fb8f40adc6eee882e838eab1edcaf62725ba28c0543bc357d5100055808b6eb7`.
- Not yet verified: reboot/sign-out persistence, Windows ARM64 runtime/installer behavior, interactive Windows Terminal and Git Bash sessions, or signing.

## Release blockers and limitations

- No repository `LICENSE` exists. Do not publish or redistribute until the owner adds an approved license and runtime/installer notices are reviewed.
- The local Inno compiler identifies its use mode as non-commercial. Confirm applicable Inno Setup licensing before any commercial use.
- The setup is unsigned. No trusted-publisher or SmartScreen status is claimed.
- Linux and macOS package-manager installers are not produced. Existing cross-platform CI builds portable archives; macOS signing/notarization and distro package qualification remain future release work.
- The original Linux `hollywood` command can conflict; the recommended distinct command for Linux archives is `hollywood-cli` until a package can safely resolve ownership.

## Exact next tasks

1. Validate the CI installer job on a hosted Windows runner.
2. Perform a real reboot/sign-out persistence test in a disposable Windows VM.
3. Validate setup and command lookup on Windows ARM64 hardware or a qualified VM.
4. Keep installer unsigned and local until signing credentials, publisher identity, and project license are authorized.
5. Prepare Linux `.deb`/`.rpm` and macOS `.pkg` only after target distribution and signing qualification plans are available.
