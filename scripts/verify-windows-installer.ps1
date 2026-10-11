[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SetupDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Compiler,
    [string]$GamePackage,
    [string]$LauncherPackage,
    [switch]$StandardUser
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-Program([string]$Executable, [string[]]$Arguments, [string]$Name) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -PassThru
    if (-not $process.WaitForExit(120000)) {
        Stop-Process -Id $process.Id -Force
        throw "$Name did not finish within two minutes."
    }
    Assert-Condition ($process.ExitCode -eq 0) "$Name failed with exit code $($process.ExitCode)."
}

function Assert-Manifest([string]$Directory) {
    foreach ($line in [IO.File]::ReadAllLines((Join-Path $Directory 'manifest.sha256'))) {
        if ($line.StartsWith('#') -or $line.Length -eq 0) { continue }
        Assert-Condition ($line -match '^([a-fA-F0-9]{64}) [* ]\./(.+)$') 'Invalid package manifest.'
        $hash = $Matches[1]
        $path = Join-Path $Directory $Matches[2]
        Assert-Condition ((Get-FileHash -LiteralPath $path).Hash -eq $hash) "Installed file differs: $path"
    }
}

function Snapshot-Files([string]$Directory) {
    $hashes = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Directory -File -Recurse -Force) {
        $hashes[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash
    }
    return $hashes
}

function Assert-Preserved($Hashes) {
    foreach ($path in $Hashes.Keys) {
        Assert-Condition (Test-Path -LiteralPath $path) "Setup removed player data: $path"
        Assert-Condition ((Get-FileHash -LiteralPath $path).Hash -eq $Hashes[$path]) "Setup changed player data: $path"
    }
}

function Assert-Shortcut([string]$Path, [string]$Target) {
    Assert-Condition (Test-Path -LiteralPath $Path) "Shortcut missing: $Path"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    Assert-Condition ($shortcut.TargetPath -eq $Target) "Shortcut points elsewhere: $Path"
}

if ($StandardUser) {
    # This branch runs under the newly created, non-administrator account.
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    Assert-Condition (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) 'Install check must run as a standard user.'
    $profileDirectory = [Environment]::GetFolderPath('UserProfile')
    $userName = $identity.Name.Split('\')[-1]
    Assert-Condition ((Split-Path $profileDirectory -Leaf) -eq $userName) 'The standard-user profile was not loaded.'
    $localData = [Environment]::GetFolderPath('LocalApplicationData')
    Write-Output "INSTALLER_NATIVE_USER identity=$($identity.Name) profile=$profileDirectory localData=$localData"
    Assert-Condition ($localData.StartsWith($profileDirectory + '\', [StringComparison]::OrdinalIgnoreCase)) 'LocalAppData belongs to another profile.'
    $dataRoot = Join-Path $localData 'ClankerWorld'
    $programRoot = Join-Path $localData 'Programs\ClankerWorld'
    Assert-Condition (-not (Test-Path $dataRoot) -and -not (Test-Path $programRoot)) 'Native check refuses an existing player installation.'
    $build = Get-Content (Join-Path $SetupDirectory 'build.json') -Raw | ConvertFrom-Json
    $setup = Join-Path $SetupDirectory "ClankerWorld-Setup-$($build.gameVersion)-windows-x64.exe"
    $oldSetup = Join-Path $SetupDirectory 'older-setup-fixture.exe'
    $game = Join-Path $dataRoot "versions\$($build.gameVersion)"
    $launcher = Join-Path $programRoot 'ClankerWorldLauncher.exe'
    $registration = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClankerWorld.Launcher_is1'
    $machineRegistration = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClankerWorld.Launcher_is1'
    $menuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'ClankerWorld\ClankerWorld.lnk'
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'ClankerWorld.lnk'
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    foreach ($relative in @('saves\preserved-world', 'client\preserved-key', 'usage\preserved-count', 'launcher\preserved-settings', 'versions\0.0.1-alpha.1\preserved-version')) {
        $path = Join-Path $dataRoot $relative
        New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
        [IO.File]::WriteAllText($path, 'Synthetic installer preservation marker: ' + $relative)
    }
    $original = Snapshot-Files $dataRoot
    Invoke-Program $oldSetup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=""', "/LOG=`"$(Join-Path $OutputDirectory 'first-install.log')`"") 'Initial setup'
    Assert-Condition ((Get-ItemProperty $registration).DisplayVersion -eq '0.0.0-ci') 'Older setup fixture was not registered.'
    Assert-Condition (-not (Test-Path $machineRegistration)) 'Setup registered a machine-wide installation.'
    Assert-Shortcut $menuShortcut $launcher
    Assert-Condition (-not (Test-Path $desktopShortcut)) 'Unchecked desktop shortcut was created.'
    Assert-Manifest $programRoot
    Assert-Manifest $game
    Assert-Preserved $original
    # The fixture's valid, different README/manifest catches overwriting an
    # existing same-version folder instead of preserving it as a whole.
    $installed = Snapshot-Files $dataRoot
    Invoke-Program $setup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=desktopicon', "/LOG=`"$(Join-Path $OutputDirectory 'upgrade.log')`"") 'Newer setup'
    Assert-Condition ((Get-ItemProperty $registration).DisplayVersion -eq $build.launcherVersion) 'The newer setup did not replace its registration.'
    Assert-Shortcut $menuShortcut $launcher
    Assert-Shortcut $desktopShortcut $launcher
    Assert-Manifest $programRoot
    Assert-Preserved $installed
    Invoke-Program $setup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=desktopicon', "/LOG=`"$(Join-Path $OutputDirectory 'reinstall.log')`"") 'Reinstall'
    Assert-Preserved $installed
    $launcherProcess = Start-Process -FilePath $launcher -ArgumentList @('--headless', '--', '--launcher-smoke-test') -PassThru -RedirectStandardOutput (Join-Path $OutputDirectory 'launcher.log') -RedirectStandardError (Join-Path $OutputDirectory 'launcher-errors.log')
    if (-not $launcherProcess.WaitForExit(60000)) { Stop-Process -Id $launcherProcess.Id -Force; throw 'Installed launcher timed out.' }
    Assert-Condition ($launcherProcess.ExitCode -eq 0) 'Installed launcher did not run.'
    Assert-Condition ((Get-Content (Join-Path $OutputDirectory 'launcher.log') -Raw).Contains('Launcher checks passed:')) 'Installed launcher checks did not complete.'
    $uninstallCommand = (Get-ItemProperty $registration).UninstallString
    Assert-Condition ($uninstallCommand -match '^"([^"]+\.exe)"') 'Registered uninstall command is missing.'
    Invoke-Program $Matches[1] @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$(Join-Path $OutputDirectory 'uninstall.log')`"") 'Uninstall'
    # Inno removes its own running executable through a short-lived helper.
    $removalDeadline = [DateTime]::UtcNow.AddSeconds(30)
    while ((Test-Path $programRoot) -and [DateTime]::UtcNow -lt $removalDeadline) { Start-Sleep -Milliseconds 100 }
    Assert-Condition (-not (Test-Path $programRoot)) 'Uninstall kept launcher program files.'
    Assert-Condition (-not (Test-Path $registration)) 'Uninstall kept its Windows registration.'
    Assert-Condition (-not (Test-Path $menuShortcut) -and -not (Test-Path $desktopShortcut)) 'Uninstall kept shortcuts.'
    Assert-Preserved $installed
    Invoke-Program $setup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=""', "/LOG=`"$(Join-Path $OutputDirectory 'install-again.log')`"") 'Install after uninstall'
    Assert-Shortcut $menuShortcut $launcher
    Assert-Condition (-not (Test-Path $desktopShortcut)) 'Install after uninstall ignored the desktop choice.'
    Assert-Manifest $programRoot
    Assert-Preserved $installed
    Assert-Condition (@(Get-ChildItem (Join-Path $dataRoot 'versions') -Directory -Force | Where-Object Name -Like '.setup-*').Count -eq 0) 'An incomplete setup staging folder remains.'
    [ordered]@{ standardUser = $identity.Name; administrator = $false; sourceRevision = $build.sourceRevision; gameVersion = $build.gameVersion; fixtureVersion = '0.0.0-ci'; launcherVersion = $build.launcherVersion; checks = @('first install', 'optional shortcuts', 'higher-version setup', 'reinstall', 'installed native launcher', 'uninstall', 'install after uninstall', 'player data and version hashes') } | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    Write-Output 'INSTALLER_NATIVE standardUserInstallUpgradeReinstallUninstallDataAndVersions=passed'
    exit 0
}

Assert-Condition ($env:OS -eq 'Windows_NT' -and $env:CI -eq 'true') 'Run this controller only in native Windows CI.'
Assert-Condition ((New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) 'CI needs permission to create an ephemeral standard user.'
$build = Get-Content (Join-Path $SetupDirectory 'build.json') -Raw | ConvertFrom-Json
$setup = Get-Item (Join-Path $SetupDirectory "ClankerWorld-Setup-$($build.gameVersion)-windows-x64.exe")
$checksum = Get-Content ($setup.FullName + '.sha256') -Raw
Assert-Condition ($checksum.Trim() -match ('^([a-fA-F0-9]{64}) [* ]' + [regex]::Escape($setup.Name) + '$')) 'Setup checksum names another file.'
Assert-Condition ((Get-FileHash $setup.FullName).Hash -eq $Matches[1]) 'Setup checksum differs.'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$fixture = Join-Path $OutputDirectory 'fixture'
Expand-Archive -LiteralPath $GamePackage -DestinationPath (Join-Path $fixture 'game')
Expand-Archive -LiteralPath $LauncherPackage -DestinationPath (Join-Path $fixture 'launcher')
$gameFiles = @(Get-ChildItem (Join-Path $fixture 'game') -Directory)[0].FullName
$launcherFiles = @(Get-ChildItem (Join-Path $fixture 'launcher') -Directory)[0].FullName
# A synthetic older setup exercises actual Inno upgrade behavior without
# publishing a false old launcher release. Its payload differs in README only.
foreach ($directory in @($gameFiles, $launcherFiles)) {
    $readme = Join-Path $directory 'README.txt'
    [IO.File]::AppendAllText($readme, "`r`nOlder installer fixture preservation marker.`r`n")
    $hash = (Get-FileHash $readme).Hash.ToLowerInvariant()
    $manifest = Join-Path $directory 'manifest.sha256'
    $lines = [IO.File]::ReadAllLines($manifest) | ForEach-Object { if ($_ -match '^[a-f0-9]{64} \*\./README\.txt$') { "$hash *./README.txt" } else { $_ } }
    [IO.File]::WriteAllLines($manifest, [string[]]$lines, (New-Object Text.UTF8Encoding($false)))
    Assert-Manifest $directory
}
$fixtureSetupDirectory = Join-Path $fixture 'setup'
New-Item -ItemType Directory -Path $fixtureSetupDirectory -Force | Out-Null
& $Compiler "/O$fixtureSetupDirectory" '/Folder-setup-fixture' '/DLauncherVersion=0.0.0-ci' '/DLauncherFileVersion=0.0.0.0' "/DGameVersion=$($build.gameVersion)" "/DSourceRevision=$($build.sourceRevision)" "/DLauncherFiles=$launcherFiles" "/DGameFiles=$gameFiles" (Join-Path $PSScriptRoot 'windows-installer.iss')
Assert-Condition ($LASTEXITCODE -eq 0) 'Older setup fixture compilation failed.'
$nativeUserName = 'cwinst-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$password = ConvertTo-SecureString ('Cw!' + [Guid]::NewGuid().ToString('N') + 'a7') -AsPlainText -Force
$shared = Join-Path ([Environment]::GetFolderPath('CommonDocuments')) ('ClankerWorldInstallerCI-' + [Guid]::NewGuid().ToString('N'))
try {
    New-LocalUser -Name $nativeUserName -Password $password -Description 'Ephemeral ClankerWorld installer CI' | Out-Null
    Add-LocalGroupMember -Group (Get-LocalGroup -SID 'S-1-5-32-545').Name -Member $nativeUserName
    New-Item -ItemType Directory -Path $shared | Out-Null
    Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $shared 'verify.ps1')
    Copy-Item -LiteralPath $SetupDirectory -Destination (Join-Path $shared 'setup') -Recurse
    Copy-Item (Join-Path $fixtureSetupDirectory 'older-setup-fixture.exe') (Join-Path $shared 'setup')
    & icacls $shared /grant "${nativeUserName}:(OI)(CI)M" /T | Out-Null
    Assert-Condition ($LASTEXITCODE -eq 0) 'Could not grant the standard user access to its fixture.'
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + (Join-Path $shared 'verify.ps1') + '"'), '-StandardUser', '-SetupDirectory', ('"' + (Join-Path $shared 'setup') + '"'), '-OutputDirectory', ('"' + (Join-Path $shared 'evidence') + '"'))
    $credential = New-Object Management.Automation.PSCredential("$env:COMPUTERNAME\$nativeUserName", $password)
    $process = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList $arguments -Credential $credential -LoadUserProfile -UseNewEnvironment -PassThru -RedirectStandardOutput (Join-Path $OutputDirectory 'standard-user.log') -RedirectStandardError (Join-Path $OutputDirectory 'standard-user-errors.log')
    if (-not $process.WaitForExit(600000)) { Stop-Process -Id $process.Id -Force; throw 'Standard-user installer checks timed out.' }
    if (Test-Path (Join-Path $shared 'evidence')) { Copy-Item (Join-Path $shared 'evidence') $OutputDirectory -Recurse }
    Get-Content (Join-Path $OutputDirectory 'standard-user.log')
    Get-Content (Join-Path $OutputDirectory 'standard-user-errors.log')
    Assert-Condition ($process.ExitCode -eq 0) 'Standard-user installer checks failed.'
    Assert-Condition (Test-Path (Join-Path $OutputDirectory 'evidence\result.json')) 'Native installer results are missing.'
} finally {
    if (Get-LocalUser -Name $nativeUserName -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $nativeUserName }
    # Account teardown is separate from game uninstall; retain its data until
    # the ephemeral runner is discarded, so the preservation proof stays honest.
}
