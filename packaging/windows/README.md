# Windows setup packaging

`build-installer.ps1` publishes self-contained `win-x64` and `win-arm64` application payloads, builds portable ZIPs and SHA-256 sidecars, then compiles `hollywood.iss` into `Hollywood-Setup.exe`. It uses the official Inno Setup compiler installed per user or machine; pass `-CompilerPath` when it is outside standard locations.

```powershell
./packaging/windows/build-installer.ps1 -Version 0.2.0
```

Outputs are written under the ignored `artifacts/installer/` directory. Setup is per-user (`%LOCALAPPDATA%\Programs\Hollywood`), tracks only its own PATH registration, and has no service or startup entry. It detects an existing `hollywood` command and selects `hollywood-cli` if available; if both names conflict it aborts without replacing the existing command. Configuration lives outside the install directory and is preserved on uninstall.

Validate a built installer with:

```powershell
./tests/installation/windows-install.ps1 -SetupPath ./artifacts/installer/releases/windows/Hollywood-Setup.exe
```

The test uses temporary installs and restores the pre-test HKCU PATH value. It skips an existing default install directory. To test version upgrade and failed-upgrade recovery, first build the test-only fixtures:

```powershell
./tests/installation/build-fixtures.ps1
./tests/installation/windows-install.ps1 `
  -SetupPath ./artifacts/installer/releases/windows/Hollywood-Setup.exe `
  -UpgradeSetupPath ./artifacts/installer/test-fixtures/Hollywood-Upgrade-Test.exe `
  -FailingSetupPath ./artifacts/installer/test-fixtures/Hollywood-Failure-Test.exe `
  -SkipDefaultDirectory -SkipCollision
```

The failure fixture contains a deliberately invalid executable and must never be distributed as a product build. Do not point the test at a production installation directory.

The lifecycle harness requires a standard-user process by default. CI can pass `-AllowElevatedTestHost` when its hosted Windows runner uses an administrator token; the separate local standard-user run is still required to verify the no-admin acceptance criterion.

## Release caveats

- The repository needs an owner-approved `LICENSE` before redistribution.
- Setup is unsigned until project signing credentials are configured.
- The local Inno Setup compiler reports non-commercial use only. Re-evaluate the Inno Setup license and acquire any necessary commercial license before commercial use.
- No Windows ARM64 machine is currently available for interactive installer validation; the ARM64 payload is cross-published and embedded, not runtime-tested here.
- The setup is built with the Inno Setup compiler; see [official compiler command-line documentation](https://jrsoftware.org/ishelp/topic_compilercmdline.htm), [per-user privilege mode](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm), and [architecture identifiers](https://jrsoftware.org/ishelp/topic_archidentifiers.htm).
