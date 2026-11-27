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

## Running a candidate

The **Release candidate** workflow runs a rehearsal on changes to its workflow file on main. A maintainer can also run it on main from the Actions page with the full commit SHA and beta version. Leave **Create a release draft** unchecked during qualification.

For a draft, first record the owner’s license decision and commit the approved files. Configure the **release-draft** environment with required reviewers, no self approval, and deployments from main only. In that environment, set **APPROVED_RELEASE_SHA** to the reviewed commit, **APPROVED_LICENSE_SHA256** to the exact license file hash, and **QUALIFIED_PREVIEW_ASSETS** to the space-separated list of documented, qualified assets. Run the workflow with the draft switch and verify the draft’s assets and tested commit. Publish from GitHub after the owner explicitly authorizes that draft.

The Windows setup candidate is named Hollywood-Setup.exe in engineering outputs and Hollywood-Setup-x64-arm64.exe in a draft. Both names describe the same binary. To claim trusted Windows distribution, sign with the approved publisher identity, re-run qualification on the signed setup, and update its metadata and checksum. The optional macOS signing script in packaging/macos uses a Developer ID Application certificate and an existing notarytool keychain profile. It produces a stapled disk image for separate Gatekeeper and desktop installation qualification.
