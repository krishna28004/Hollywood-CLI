# Release readiness

**Decision: DEVELOPMENT.** This build is not a release candidate.

Completed: native .NET CLI and engine, synthetic modules, five themes, snapshots, versioned JSON config, sample config, Windows and POSIX terminal adapters, POSIX PTY harness, native-runner build matrix, and a local self-contained Windows x64 package.

Local packaging produced `artifacts/hollywood-win-x64.zip` (33,058,117 bytes) and `artifacts/SHA256SUMS.txt` (SHA-256: `71F4AE058CCAE9CF1AEF42A8E6D691CE034C34A68CD6AAA686917450B0936AF7`). The five-entry archive passed CRC verification and contains the self-contained executable, README, install guide, sample config, and artifact metadata; it contains no PDB symbols or local research materials. Both remain ignored local artifacts and are not included in Git commits.

Release gates still open:

- Direct Windows Terminal/Console Host validation
- Linux/macOS native build and PTY execution on all planned architectures; the local environment cannot reach NuGet or run POSIX binaries
- Terminal mode restoration inspection in visible hosts, output backpressure, and soak evidence
- Performance measurements against approved budgets
- Broader config/CLI precedence and failure-path integration coverage
- Clean standard-user launch and dependency/license inventory for the portable ZIP
- Review of license choice and third-party notices before distribution
- Private GitHub remote creation and scoped push requested by the owner

Native AOT has not been selected; the initial release workflow uses self-contained single-file .NET publishing, while AOT requires native toolchains and separate compatibility/size/startup evidence. No public package has been published. Build artifacts are ignored and will not be committed.
