[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $SetupPath,
    [string] $UpgradeSetupPath,
    [string] $FailingSetupPath,
    [switch] $SkipDefaultDirectory,
    [switch] $SkipCollision,
    [switch] $DefaultDirectoryOnly,
    [switch] $SkipRepeatAndRepair,
    [switch] $AllowElevatedTestHost
)

$ErrorActionPreference = 'Stop'
$setup = (Resolve-Path -LiteralPath $SetupPath).Path
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C6B24AA2-1A28-4F6C-9C4E-4C2DB93D18D5}_is1'
$stateKey = 'HKCU:\Software\Hollywood CLI\Installer'
$environmentKey = 'HKCU:\Environment'
$originalPathProperty = Get-ItemProperty -LiteralPath $environmentKey -Name Path -ErrorAction SilentlyContinue
$originalPathExists = $null -ne $originalPathProperty
$originalPath = if ($originalPathExists) { [string]$originalPathProperty.Path } else { '' }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
$isElevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$configPath = Join-Path $env:LOCALAPPDATA 'Hollywood/config.json'
$configBefore = if (Test-Path -LiteralPath $configPath) { (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash } else { $null }
$machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
$unrelatedSentinel = Join-Path $env:TEMP 'Hollywood CLI install test sibling.txt'
[IO.File]::WriteAllText($unrelatedSentinel, 'preserve')

function Assert([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Normalize-Entry([string] $Value) {
    $item = [Environment]::ExpandEnvironmentVariables($Value.Trim().Trim('"'))
    return $item.TrimEnd('\').ToLowerInvariant()
}

function Get-EffectivePath {
    $userPathProperty = Get-ItemProperty -LiteralPath $environmentKey -Name Path -ErrorAction SilentlyContinue
    $userPath = if ($userPathProperty) { [Environment]::ExpandEnvironmentVariables([string]$userPathProperty.Path) } else { '' }
    return (@($userPath, $machinePath) | Where-Object { $_ }) -join ';'
}

function Invoke-CapturedProcess([string] $FileName, [string] $Arguments, [string] $WorkingDirectory, [string] $PathValue) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $FileName
    $info.Arguments = $Arguments
    $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.EnvironmentVariables['PATH'] = $PathValue
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $stdout; Error = $stderr }
}

function Start-Setup([string] $Path, [string] $TargetDir, [string] $LogPath, [string] $ProcessPath) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/DIR=`"$TargetDir`"", "/LOG=`"$LogPath`"")
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Path
    $info.Arguments = $arguments -join ' '
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    if ($ProcessPath) { $info.EnvironmentVariables['PATH'] = $ProcessPath }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    [void]$process.Start()
    $process.WaitForExit()
    return $process.ExitCode
}

function Get-InstalledCommandName {
    $state = Get-ItemProperty -LiteralPath $stateKey
    return [string]$state.HollywoodCommand
}

function Assert-CommandWorks([string] $CommandName, [string] $TargetDir, [string] $ExpectedVersion = '0.2.0') {
    $pathValue = Get-EffectivePath
    $expected = "Hollywood CLI $ExpectedVersion"
    $cmd = Invoke-CapturedProcess $env:ComSpec "/d /c $CommandName --version" $env:WINDIR $pathValue
    Assert ($cmd.ExitCode -eq 0 -and $cmd.Output.Contains($expected)) "CMD could not launch $CommandName from $env:WINDIR. $($cmd.Error)"

    foreach ($shell in @(@('powershell.exe', '5.1'), @('pwsh.exe', '7+'))) {
        $exe = Get-Command $shell[0] -ErrorAction SilentlyContinue
        if (-not $exe) { continue }
        $script = "& {0} --version" -f $CommandName
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
        $result = Invoke-CapturedProcess $exe.Source "-NoLogo -NoProfile -NonInteractive -EncodedCommand $encoded" $env:WINDIR $pathValue
        Assert ($result.ExitCode -eq 0 -and $result.Output.Contains($expected)) "PowerShell $($shell[1]) could not launch $CommandName. $($result.Error)"
    }
}

function Get-PathCount([string] $PathValue, [string] $Entry) {
    $expected = Normalize-Entry $Entry
    return @($PathValue.Split(';') | Where-Object { (Normalize-Entry $_) -eq $expected }).Count
}

function Run-InstallLifecycle([string] $TargetDir, [string] $ProcessPath, [string] $UpgradeInstaller, [string] $FailureInstaller, [string] $ExpectedCommand = '', [bool] $SkipRepeatAndRepairChecks = $false) {
    $target = [IO.Path]::GetFullPath($TargetDir)
    Assert (-not (Test-Path -LiteralPath $target)) "Refusing to use existing test install directory: $target"
    $log = Join-Path $env:TEMP ('Hollywood-install-' + [Guid]::NewGuid().ToString('N') + '.log')
    try {
        $exit = Start-Setup $setup $target $log $ProcessPath
        Assert ($exit -eq 0) "Fresh install failed with exit code $exit. See $log"
        Assert ((-not $isElevated) -or $AllowElevatedTestHost) 'Run this lifecycle test as a standard user to prove installation does not require administrator privileges.'
        Assert (Test-Path -LiteralPath (Join-Path $target 'unins000.exe')) 'Per-user uninstaller was not created.'
        Assert (Test-Path -LiteralPath $uninstallKey) 'Installation was not registered under HKCU.'

        $pathProperty = Get-ItemProperty -LiteralPath $environmentKey -Name Path -ErrorAction SilentlyContinue
        $pathNow = if ($pathProperty) { [string]$pathProperty.Path } else { '' }
        Assert ((Get-PathCount $pathNow $target) -eq 1) 'Install path was not registered exactly once in user PATH.'
        $command = Get-InstalledCommandName
        Assert ($command -in @('hollywood', 'hollywood-cli')) "Unexpected command name '$command'."
        if ($ExpectedCommand) { Assert ($command -eq $ExpectedCommand) "Expected '$ExpectedCommand' after collision detection, got '$command'." }
        Assert-CommandWorks $command $target

        if (-not $SkipRepeatAndRepairChecks) {
            $exit = Start-Setup $setup $target $log $ProcessPath
            Assert ($exit -eq 0) "Repeated install/repair failed with exit code $exit. See $log"
            $pathProperty = Get-ItemProperty -LiteralPath $environmentKey -Name Path -ErrorAction SilentlyContinue
            $pathNow = if ($pathProperty) { [string]$pathProperty.Path } else { '' }
            Assert ((Get-PathCount $pathNow $target) -eq 1) 'Repeated install duplicated the user PATH entry.'

            $installedExe = Get-ChildItem -LiteralPath (Join-Path $target 'versions') -Filter hollywood.exe -File -Recurse | Select-Object -First 1
            Assert ($null -ne $installedExe) 'Installed self-contained executable was not found.'
            $savedExe = $installedExe.FullName + '.test-backup'
            Move-Item -LiteralPath $installedExe.FullName -Destination $savedExe
            try {
                $exit = Start-Setup $setup $target $log $ProcessPath
                Assert ($exit -eq 0 -and (Test-Path -LiteralPath $installedExe.FullName)) 'Repair did not restore a missing executable.'
            } finally {
                if (Test-Path -LiteralPath $savedExe) { Remove-Item -LiteralPath $savedExe -Force }
            }
            Assert-CommandWorks $command $target
        }

        if ($UpgradeInstaller) {
            $upgrade = (Resolve-Path -LiteralPath $UpgradeInstaller).Path
            $exit = Start-Setup $upgrade $target $log $ProcessPath
            Assert ($exit -eq 0) "Upgrade failed with exit code $exit. See $log"
            Assert-CommandWorks $command $target '0.2.0'
            Assert (Test-Path -LiteralPath (Join-Path $target 'versions/0.2.1/win-x64/hollywood.exe')) 'Upgrade did not install the new versioned payload.'
        }

        if ($FailureInstaller) {
            $failure = (Resolve-Path -LiteralPath $FailureInstaller).Path
            $failureExit = Start-Setup $failure $target $log $ProcessPath
            Assert ($failureExit -eq 4) "Failed setup did not return the installer error code 4 (received $failureExit)."
            Assert (-not (Test-Path -LiteralPath (Join-Path $target 'versions/0.2.2/win-x64/hollywood.exe'))) 'The deliberately invalid payload was left installed.'
            Assert ((Get-Content -LiteralPath $log -Raw) -match 'Hollywood CLI installation failed: The installed executable failed validation') 'The failed setup did not report the payload verification error.'
            Assert-CommandWorks $command $target '0.2.0'
            if ($UpgradeInstaller) {
                $launcher = Get-Content -LiteralPath (Join-Path $target "$command.cmd") -Raw
                Assert ($launcher.Contains('versions\0.2.1\')) 'Failed upgrade did not preserve the prior working launcher.'
            }
            Assert (-not (Test-Path -LiteralPath (Join-Path $target 'versions/0.2.2'))) 'Failed upgrade left the invalid payload directory behind.'
        }

        $uninstaller = Join-Path $target 'unins000.exe'
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru
        Assert ($uninstall.ExitCode -eq 0) "Uninstall failed with exit code $($uninstall.ExitCode)."
        Assert (-not (Test-Path -LiteralPath $uninstallKey)) 'Uninstall registry entry remains.'
        $pathProperty = Get-ItemProperty -LiteralPath $environmentKey -Name Path -ErrorAction SilentlyContinue
        $pathAfterUninstall = if ($pathProperty) { [string]$pathProperty.Path } else { '' }
        Assert ((Get-PathCount $pathAfterUninstall $target) -eq 0) 'Uninstall left its PATH entry behind.'
        Assert ($pathAfterUninstall -ceq $originalPath) 'Uninstall did not restore the exact pre-install user PATH value.'
        Assert (Test-Path -LiteralPath $unrelatedSentinel) 'Uninstall removed an unrelated neighboring file.'
        if ($configBefore) {
            Assert ((Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash -eq $configBefore) 'Uninstall changed the existing Hollywood configuration.'
        } else {
            Assert (-not (Test-Path -LiteralPath $configPath)) 'Installer created an unexpected user configuration file.'
        }
        Assert (-not (Test-Path -LiteralPath (Join-Path $target 'versions'))) 'Uninstall left the installed application payload behind.'
        $coverage = @('install', 'command lookup from another directory')
        if (-not $SkipRepeatAndRepairChecks) { $coverage += @('repeated install', 'repair') }
        if ($UpgradeInstaller) { $coverage += 'upgrade' }
        if ($FailureInstaller) { $coverage += 'failed-upgrade rollback' }
        $coverage += 'uninstall'
        Write-Host "PASS: $($coverage -join ', ') at $target"
    } finally {
        if (Test-Path -LiteralPath $target) {
            $leftoverUninstaller = Join-Path $target 'unins000.exe'
            if (Test-Path -LiteralPath $leftoverUninstaller) {
                Start-Process -FilePath $leftoverUninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait | Out-Null
            }
            if (Test-Path -LiteralPath $target) {
                $normalizedTarget = [IO.Path]::GetFullPath($target)
                $allowedRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'Hollywood CLI Install Tests')).TrimEnd('\') + '\'
                if ($normalizedTarget.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
                    Remove-Item -LiteralPath $normalizedTarget -Recurse -Force
                }
            }
        }
        Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue
    }
}

try {
    $defaultDir = Join-Path $env:LOCALAPPDATA 'Programs/Hollywood'
    if ($DefaultDirectoryOnly) {
        if (Test-Path -LiteralPath $defaultDir) {
            throw "Refusing to change an existing default install directory: $defaultDir"
        }
        Run-InstallLifecycle $defaultDir '' $UpgradeSetupPath $FailingSetupPath '' $SkipRepeatAndRepair
    } elseif (-not $SkipDefaultDirectory) {
        if (-not (Test-Path -LiteralPath $defaultDir)) {
            Run-InstallLifecycle $defaultDir '' $UpgradeSetupPath $FailingSetupPath '' $SkipRepeatAndRepair
        } else {
            Write-Warning "Default install directory already exists; left untouched: $defaultDir"
        }
    }
    if ($DefaultDirectoryOnly) { return }
    $testBase = Join-Path $env:TEMP 'Hollywood CLI Install Tests'
    New-Item -ItemType Directory -Force -Path $testBase | Out-Null
    $spacedTarget = Join-Path $testBase 'Path with spaces Ω'
    Run-InstallLifecycle $spacedTarget '' $UpgradeSetupPath $FailingSetupPath '' $SkipRepeatAndRepair

    if (-not $SkipCollision) {
        $collisionDir = Join-Path $testBase 'Existing Command Owner'
        New-Item -ItemType Directory -Force -Path $collisionDir | Out-Null
        $collisionCommand = Join-Path $collisionDir 'hollywood.cmd'
        [IO.File]::WriteAllText($collisionCommand, '@echo off`r`necho existing-owner`r`n', [Text.Encoding]::ASCII)
        $collisionTarget = Join-Path $testBase 'Collision Install'
        Run-InstallLifecycle $collisionTarget "$collisionDir;$env:PATH" $UpgradeSetupPath $FailingSetupPath 'hollywood-cli' $SkipRepeatAndRepair
        Assert ((Get-Content -LiteralPath $collisionCommand -Raw) -like '*existing-owner*') 'Collision handling changed the pre-existing hollywood command.'
    }
} finally {
    if ($originalPathExists) {
        if (-not (Test-Path -LiteralPath $environmentKey)) { New-Item -Path $environmentKey -Force | Out-Null }
        Set-ItemProperty -LiteralPath $environmentKey -Name Path -Value $originalPath
    } else {
        Remove-ItemProperty -LiteralPath $environmentKey -Name Path -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $unrelatedSentinel) { Remove-Item -LiteralPath $unrelatedSentinel -Force }
}
