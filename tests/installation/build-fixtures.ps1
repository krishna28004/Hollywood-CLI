[CmdletBinding()]
param([string] $CompilerPath, [switch] $OnlyFailure)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $repoRoot
$payload = Join-Path $repoRoot 'artifacts/installer/publish'
$fixtureRoot = Join-Path $repoRoot 'artifacts/installer/test-fixtures'
$failurePayload = Join-Path $fixtureRoot 'invalid-payload'
if (-not $CompilerPath) {
    $CompilerPath = @(
        (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 7/ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7/ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7/ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}
if (-not $CompilerPath) { throw 'Inno Setup 6.3+ is required to compile installer lifecycle fixtures.' }
$CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
foreach ($rid in @('win-x64', 'win-arm64')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload "$rid/hollywood.exe"))) {
        throw "Run packaging/windows/build-installer.ps1 first; missing $rid application payload."
    }
}
New-Item -ItemType Directory -Force -Path $fixtureRoot, $failurePayload | Out-Null
Copy-Item -LiteralPath (Join-Path $payload 'THIRD-PARTY-NOTICES.txt'),
    (Join-Path $payload 'DOTNET-LICENSE.txt'),
    (Join-Path $payload 'LICENSE-INFORMATION.txt'),
    (Join-Path $payload 'INNO-SETUP-LICENSE.txt') -Destination $failurePayload -Force
New-Item -ItemType Directory -Force -Path (Join-Path $failurePayload 'win-x64'),
    (Join-Path $failurePayload 'win-arm64') | Out-Null
[IO.File]::WriteAllText((Join-Path $failurePayload 'win-x64/hollywood.exe'), 'deliberately invalid test payload', [Text.Encoding]::ASCII)
Copy-Item -LiteralPath (Join-Path $payload 'win-arm64/hollywood.exe') -Destination (Join-Path $failurePayload 'win-arm64/hollywood.exe') -Force

$scriptPath = Join-Path $PSScriptRoot '../../packaging/windows/hollywood.iss'
if (-not $OnlyFailure) {
    & $CompilerPath '/DAppVersion=0.2.1' "/O$fixtureRoot" '/FHollywood-Upgrade-Test' $scriptPath
    if ($LASTEXITCODE -ne 0) { throw 'Failed to compile the valid version 0.2.1 upgrade fixture.' }
}
& $CompilerPath '/DAppVersion=0.2.2' '/DPayloadRoot=..\..\artifacts\installer\test-fixtures\invalid-payload' "/O$fixtureRoot" '/FHollywood-Failure-Test' $scriptPath
if ($LASTEXITCODE -ne 0) { throw 'Failed to compile the deliberately invalid rollback fixture.' }
Write-Host "Created test-only fixtures in $fixtureRoot"
