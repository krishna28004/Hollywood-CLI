# Release blockers

| Gate | Status | Evidence needed |
| --- | --- | --- |
| Source license | BLOCKED | Owner approves the license and confirms they can license the project files. Commit the approved LICENSE and NOTICE after checking asset provenance and copyright holders. |
| Security | CONDITIONAL | Pattern audit is a first pass. Review asset provenance, embedded .NET and Inno notices, and selected runtime patch before distribution. |
| Windows trusted installer | BLOCKED | Approved publisher signing identity and qualification of the signed binary. Local unsigned setup is a preview candidate. |
| Windows normal terminal and reboot | CONDITIONAL | Validate the candidate in Windows Terminal on a clean standard-user account, then perform real sign-out and reboot. ARM64 needs the equivalent native setup run. |
| Linux desktop installation | CONDITIONAL | Native CI PTY and extracted archive tests cover the Ubuntu runner. Check a clean supported desktop login shell, persistent command lookup, and at least one supported emulator. |
| macOS trusted install | BLOCKED | Run the signing and notarization steps with approved credentials. Verify Gatekeeper on a clean downloaded disk image and test Terminal.app on both architectures. |
| Public assets | BLOCKED | Finish license, platform qualification and explicit publication approval for the reviewed draft. |

## License recommendation

Apache-2.0 is a permissive option with explicit patent terms and notice obligations. MIT is shorter and has a simpler attribution model. A reciprocal license would change the terms for downstream modifications and redistribution. The recommended starting point is Apache-2.0 for original project material the owner has rights to license.

The application has no third-party NuGet package references in the current project files. It bundles a Microsoft .NET runtime and the Windows installer uses Inno Setup, which have their own terms and notices. The original Linux Hollywood project is credited as inspiration. A review of source and design image provenance must rule out copied material requiring additional terms before an owner approves the proposal.

Use the [Apache Software Foundation text](https://www.apache.org/licenses/LICENSE-2.0.txt) for the root LICENSE if approved. The proposed NOTICE identifies the owner’s copyright line, original Hollywood inspiration, the bundled .NET terms, and the Inno Setup engine. Confirm the copyright line and notice scope before adoption.
