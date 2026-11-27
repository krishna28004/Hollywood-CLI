# Public release progress

The first candidate version is **0.2.0-beta.1**. The existing application version is 0.2.0. Candidate packaging stamps the requested beta version into the executable.

| Milestone | Evidence |
| --- | --- |
| R1 · Audit | Public repository, no original release tags or assets, no project license. The historical Git scan reports matching blob IDs and rules without emitting candidate secret values. |
| R2 · Build fixes | Runtime notices come from the restored runtime pack. Archives carry the source SHA, package version, runtime version, file hashes and a CycloneDX runtime inventory. |
| R3 · Windows | One architecture-selecting Inno Setup installer. The lifecycle harness covers standard-user install, PowerShell/CMD/Git Bash lookup, repair, upgrade, failed upgrade, collision handling and uninstall when all lanes are enabled. |
| R4 · Linux/macOS | Both archive types contain a local installer and an uninstall command. The CI harness uses isolated homes, fresh shells, and a PATH with no SDK or Git. |
| R5 · Portable packaging | The package check verifies each executable’s architecture, extracts the archive, checks file hashes and launches the extracted executable with an empty PATH. |
| R6 · Automation | The release workflow records the source SHA, runs the shared six-lane matrix, and checks the resulting artifact hashes. Creating a private draft uses a separate protected job. |
| R7–R8 · Clean-user validation | Hosted test and actual desktop or reboot results are distinct. See the [release blockers](release-blockers.md). |
| R9 · Docs | The README labels its design concept, and the installation guide documents preview, portable and local installation routes. |
| R10–R11 · Release | License and publication decisions are pending. |

The [baseline automated run](https://github.com/krishna28004/Hollywood-CLI/actions/runs/45993687298) passed all six lanes on commit e056118. The release workflow rebuilds on the candidate commit and checks the exact candidate outputs.

## Defects found during qualification

- macOS represented the same home directory through different path aliases. The installer now checks resolved directory identity and has an alias regression case.
- macOS BSD utilities rejected GNU-style option separators. The installer now uses portable arguments with absolute filesystem paths.
- The Windows launcher check had prepended the installation directory. Child shell tests now reconstruct PATH from the machine and user registry values in Windows order.
- Windows uninstall after an upgrade could leave two generated launchers. Cleanup now checks the full launcher template against the version in the launcher, and the lifecycle verifies that both files are removed.
- Windows-generated archive checksums used CRLF, so GNU checksum verification treated the trailing carriage return as part of the archive name. Checksum files now use LF on every platform.

## Local Windows results

- Windows 10 x64 with a standard user token passed the default install location, a path containing spaces and Unicode, and the command collision case. Fresh PowerShell 5.1, PowerShell 7, CMD and Git Bash child processes resolved and ran the installed command from another directory using the persistent PATH values.
- Repeat installation, repair, a successful versioned upgrade, rejection of a deliberately broken upgrade, and uninstall passed. Uninstall preserved the unrelated command, user configuration and unrelated PATH values. The post-upgrade test now also verifies removal of both generated launchers.
- The packaged x64 app ran directly with an empty PATH. The local ConPTY harness observed animation, keyboard response, pause and resume, resize, Q, Escape, Ctrl+C, and cursor restoration.
- Actual Windows Terminal desktop interaction, reboot and sign-out remain unverified. The cleanup fixture and the normal compressed setup have separate build outputs.

## Candidate verification

The [0.2.0-beta.1 release rehearsal](https://github.com/krishna28004/Hollywood-CLI/actions/runs/46012320599) passed on source commit `5eda4eef570292f830226f60bb763a0272ea5cb1`. All six native jobs passed the build, regression harness, terminal integration checks, package extraction, architecture checks and standalone launch. The release job independently checked each asset's SHA-256, version and commit.

Linux x64 and ARM64 and macOS Intel and Apple Silicon also passed local installation into isolated homes, fresh-shell command lookup, a rejected replacement, interactive PTY checks and uninstall. Windows ARM64 passed the portable app checks and ConPTY. Windows x64 additionally passed the hosted installer lifecycle. That hosted pass is separate from the local standard-user desktop installation evidence.

These hashes identify the workflow's engineering artifacts. Rebuilding from different inputs, approving a license, or signing changes the hashes.

| Asset | SHA-256 |
| --- | --- |
| `hollywood-linux-arm64.tar.gz` | `070c9baa4e3183ae0ad88628bfd4b77b56ab96f2684faeb6a95a6cf37d543aca` |
| `hollywood-linux-x64.tar.gz` | `54f63dc1618734bf07aaf417c4b75268dde474e167f28c6143b51fb4b0d23773` |
| `hollywood-osx-arm64.tar.gz` | `6ed3ef04387b4c0c37b976595c940a74a70d936e13e2f88cf271d06bc78e372b` |
| `hollywood-osx-x64.tar.gz` | `d920d9d271f01a9dfc45708f6df83bcaf0fedd0dad2cbb771de65169f68d8db5` |
| `Hollywood-Setup.exe` | `df7e2449dbd638aecbfb4f2e5e9d966ad31f787e8116946f19c53e68f6a9415c` |
| `hollywood-win-arm64.zip` | `385c0c43ff2ded537d927bc16c3b1d4cacb7be32bf0876f7bd3a3c7ee8f16d46` |
| `hollywood-win-x64.zip` | `3ff06f525c9961dd4b46a14440dd0e9e284582ef69852dcc1f6d8cacececf2fc` |

**Public beta status: NOT READY.** A license, public release authorization, and the remaining platform qualification are tracked in the [blocker list](release-blockers.md). No GitHub Release draft or release was created.

## Running a candidate

The **Release candidate** workflow runs a rehearsal on changes to its workflow file on main. A maintainer can also run it on main from the Actions page with the full commit SHA and beta version. Leave **Create a release draft** unchecked during qualification.

For a draft, first record the owner’s license decision and commit the approved files. Configure the **release-draft** environment with required reviewers, no self approval, and deployments from main only. In that environment, set **APPROVED_RELEASE_SHA** to the reviewed commit, **APPROVED_LICENSE_SHA256** to the exact license file hash, and **QUALIFIED_PREVIEW_ASSETS** to the space-separated list of documented, qualified assets. Run the workflow with the draft switch and verify the draft’s assets and tested commit. Publish from GitHub after the owner explicitly authorizes that draft.

The Windows setup candidate is named Hollywood-Setup.exe in engineering outputs and Hollywood-Setup-x64-arm64.exe in a draft. Both names describe the same binary. To claim trusted Windows distribution, sign with the approved publisher identity, re-run qualification on the signed setup, and update its metadata and checksum. The optional macOS signing script in packaging/macos uses a Developer ID Application certificate and an existing notarytool keychain profile. It produces a stapled disk image for separate Gatekeeper and desktop installation qualification.
