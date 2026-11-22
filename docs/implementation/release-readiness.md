# Release readiness

**Decision: DEVELOPMENT.** This build is not a release candidate.

Completed: native .NET CLI and engine, synthetic modules, five themes, snapshots, versioned JSON config, sample config, source documentation, and a self-contained Windows x64 publish command.

Local packaging has produced `artifacts/hollywood-cli-0.2.0-win-x64.zip` (36,995,068 bytes) and `artifacts/SHA256SUMS.txt` (SHA-256: `BF72FE4E7B645F2AA8AE85B29CBA29724D4C5840B25526A6A53A731BE3CB0A87`). The archive contains 195 entries, including the executable, runtime, README, install notes, and sample config; a full ZIP stream read verified archive integrity and no symbols or local research files. Both remain ignored local artifacts and are not included in Git commits.

Release gates still open:

- Direct Windows Terminal/Console Host validation
- Ctrl+C control-event handling, terminal mode restoration, output backpressure, and soak evidence
- Performance measurements against approved budgets
- Broader config/CLI precedence and failure-path integration coverage
- Clean standard-user launch and dependency/license inventory for the portable ZIP
- Review of license choice and third-party notices before distribution
- Private GitHub remote creation and scoped push requested by the owner

No public package has been published. Build artifacts are ignored and will not be committed.
