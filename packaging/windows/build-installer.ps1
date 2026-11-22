[CmdletBinding()]
param(
    [string] $Version = '0.2.0',
    [string] $CompilerPath,
    [string] $RestoreSource
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $repoRoot

if (-not $CompilerPath) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 7/ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7/ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7/ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe')
    )
    $compiler = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
} else {
    $compiler = (Resolve-Path -LiteralPath $CompilerPath).Path
}
if (-not $compiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $compiler = $command.Source }
}
if (-not $compiler) {
    throw 'Inno Setup 6.3+ is required. Install the signed compiler from https://jrsoftware.org/inno-setup/downloads or pass -CompilerPath.'
}

$versionParts = $Version.Split('.')
if ($versionParts.Count -lt 2 -or $Version -notmatch '^\d+\.\d+\.\d+([-.][A-Za-z0-9.-]+)?$') {
    throw "Version '$Version' must use a numeric major.minor.patch form."
}

$outputRootPath = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/installer'))
$publishRoot = Join-Path $outputRootPath 'publish'
$releaseRoot = Join-Path $outputRootPath 'releases/windows'
$stageRoot = Join-Path $outputRootPath 'stage'
$noticePath = Join-Path $publishRoot 'THIRD-PARTY-NOTICES.txt'
$innoLicensePath = Join-Path $publishRoot 'INNO-SETUP-LICENSE.txt'
$compilerLicense = Join-Path (Split-Path -Parent $compiler) 'LICENSE.TXT'
$project = Join-Path $repoRoot 'src/Hollywood.Cli/Hollywood.Cli.csproj'
$noticesCandidates = @(
    (Join-Path $env:ProgramFiles 'dotnet/ThirdPartyNotices.txt'),
    (Join-Path $env:ProgramFiles 'dotnet/shared/Microsoft.NETCore.App/10.0.15/ThirdPartyNotices.txt')
)
$noticesSource = $noticesCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $noticesSource) {
    throw 'The .NET runtime ThirdPartyNotices.txt file could not be located. Refusing to build a package without runtime notices.'
}
if (-not (Test-Path -LiteralPath $compilerLicense)) {
    throw "The Inno Setup license file was not found next to the compiler: $compilerLicense"
}

function Assert-WithinOutputRoot([string] $Path) {
    $absolute = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetFullPath($outputRootPath).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $absolute.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to mutate a path outside the ignored output directory: $absolute"
    }
}

New-Item -ItemType Directory -Force -Path $publishRoot, $releaseRoot | Out-Null
foreach ($rid in @('win-x64', 'win-arm64')) {
    $ridOutput = Join-Path $publishRoot $rid
    New-Item -ItemType Directory -Force -Path $ridOutput | Out-Null
    $restoreArguments = @('restore', $project, '-r', $rid)
    if ($RestoreSource) {
        $restoreArguments += @('--source', $RestoreSource, '-p:NuGetAudit=false')
    }
    & dotnet @restoreArguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed for $rid (exit $LASTEXITCODE)." }
    & dotnet publish $project -c Release -r $rid --self-contained true -p:Version=$Version --no-restore `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -p:DebugSymbols=false -o $ridOutput
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid (exit $LASTEXITCODE)." }
    $executable = Join-Path $ridOutput 'hollywood.exe'
    if (-not (Test-Path -LiteralPath $executable) -or (Get-Item -LiteralPath $executable).Length -lt 1MB) {
        throw "The self-contained executable for $rid is missing or unexpectedly small."
    }
}

Copy-Item -LiteralPath $noticesSource -Destination $noticePath -Force
Copy-Item -LiteralPath $compilerLicense -Destination $innoLicensePath -Force
$licenseInfo = @'
Hollywood CLI source license status

The repository currently has no LICENSE file declaring a source license. This
notice does not grant permission to redistribute the Hollywood CLI source or
binary. A project license must be approved and added before a public release.

The application is published self-contained and includes the .NET runtime.
See THIRD-PARTY-NOTICES.txt for the runtime's bundled third-party notices.
'@
$licenseInfoPath = Join-Path $publishRoot 'LICENSE-INFORMATION.txt'
[IO.File]::WriteAllText($licenseInfoPath, $licenseInfo, [Text.UTF8Encoding]::new($false))

$versionOutput = & (Join-Path $publishRoot 'win-x64/hollywood.exe') --version
if ($LASTEXITCODE -ne 0 -or $versionOutput.Trim() -ne "Hollywood CLI $Version") {
    throw "The x64 packaged executable version check failed: '$versionOutput'."
}

foreach ($rid in @('win-x64', 'win-arm64')) {
    $zipPath = Join-Path $releaseRoot "hollywood-$rid.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $stage = Join-Path $stageRoot $rid
    Assert-WithinOutputRoot $stage
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    Copy-Item -Path (Join-Path $publishRoot "$rid/*") -Destination $stage -Recurse -Force
    Copy-Item -LiteralPath $noticePath, $licenseInfoPath, $innoLicensePath -Destination $stage -Force
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$zipPath.sha256", "$hash  $(Split-Path $zipPath -Leaf)`n", [Text.Encoding]::ASCII)
}

$compilerOutput = & $compiler "/DAppVersion=$Version" (Join-Path $PSScriptRoot 'hollywood.iss') 2>&1
$compilerExitCode = $LASTEXITCODE
$compilerOutput | ForEach-Object { Write-Host $_ }
if ($compilerExitCode -ne 0) { throw "Inno Setup compilation failed (exit $compilerExitCode)." }

$setupPath = Join-Path $releaseRoot 'Hollywood-Setup.exe'
if (-not (Test-Path -LiteralPath $setupPath)) { throw 'Inno Setup did not create Hollywood-Setup.exe.' }
$signature = Get-AuthenticodeSignature -FilePath $setupPath
$setupHash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
$compilerVersionLine = $compilerOutput | Where-Object { $_ -match '^Compiler engine version:' } | Select-Object -First 1
$compilerVersion = if ($compilerVersionLine) { ($compilerVersionLine -replace '^Compiler engine version:\s*', '').Trim() } else { 'Unknown (version not reported by compiler)' }
$metadata = [ordered]@{
    product = 'Hollywood CLI'
    version = $Version
    installerFramework = 'Inno Setup'
    installerFrameworkVersion = $compilerVersion
    architectures = @('win-x64', 'win-arm64')
    appLicenseDeclared = (Test-Path -LiteralPath (Join-Path $repoRoot 'LICENSE'))
    installerSignatureStatus = [string]$signature.Status
    sha256 = $setupHash
}
$metadataPath = Join-Path $releaseRoot 'artifact-metadata.json'
$metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $metadataPath -Encoding utf8
[IO.File]::WriteAllText("$setupPath.sha256", "$setupHash  Hollywood-Setup.exe`n", [Text.Encoding]::ASCII)

Write-Host "Created $setupPath"
Write-Host "SHA-256 $setupHash"
Write-Host "Installer Authenticode status: $($signature.Status)"
if ($signature.Status -ne 'Valid') {
    Write-Warning 'The product installer is unsigned. This build is a local/development artifact; Windows SmartScreen may show a warning.'
}
if (-not $metadata.appLicenseDeclared) {
    Write-Warning 'No source LICENSE file exists. Do not publish this installer until the project license is approved and added.'
}
