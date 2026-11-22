# Contributing

## Local checks

Install the .NET 10 SDK, then run:

```powershell
dotnet build Hollywood.sln -c Release
dotnet run --project tests/Hollywood.UnitTests -c Release
dotnet run --project src/Hollywood.Cli -c Release -- --help
dotnet run --project src/Hollywood.Cli -c Release -- config validate examples/config.json
```

Changes to interactive behavior should include a regression check and state the terminal host used. Snapshot output is not proof of interactive host compatibility. Keep simulated modules offline and read-only; do not add real network activity or access private user data.

## Pull requests

Describe the change, exact verification commands and results, platform/host tested, and any remaining gaps. Never include local configuration, credentials, build output, or private research files.
