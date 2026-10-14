param(
    [string]$ProSimDirectory,
    [string]$ProSimHost = '127.0.0.1',
    [string]$SimulatorConfigDirectory = (Join-Path $env:APPDATA 'Microsoft Flight Simulator 2024'),
    [string]$AircraftProfileDirectory = (Join-Path $env:APPDATA 'Virtuali\Airplanes\prosim-b738-2026\800'),
    [string]$AircraftPackageDirectory,
    [switch]$SkipAnimationFix,
    [switch]$Uninstall
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InstallerFiles.ps1')
if($Uninstall) { & (Join-Path $PSScriptRoot 'Uninstall.ps1'); return }
$statePath=Join-Path $PSScriptRoot 'IntegrationState.json'
$state=$null
if(Test-Path -LiteralPath $statePath){$state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json}
if($AircraftProfileDirectory -match '(?i)[\\/]Community[\\/]|[\\/]SimObjects[\\/]'){throw 'GSX profile folder must be the GSX user-profile folder, not the aircraft package. Use %APPDATA%\Virtuali\Airplanes\prosim-b738-2026\800; it will be created if missing.'}
$exeXmlPath = Join-Path $SimulatorConfigDirectory 'EXE.xml'
if (!(Test-Path -LiteralPath $exeXmlPath)) { throw "EXE.xml not found at $exeXmlPath. Supply -SimulatorConfigDirectory for your installation." }
$exeXml = [xml][IO.File]::ReadAllText($exeXmlPath)
if ($exeXml.DocumentElement.Name -ne 'SimBase.Document') { throw 'Unexpected EXE.xml structure' }
$entryName = 'GSX ProSim Door Bridge'
$entries = @($exeXml.SelectNodes('/SimBase.Document/Launch.Addon') | Where-Object { $_.Name -eq $entryName })
$backupFolder = Join-Path $PSScriptRoot ('Backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
Copy-BridgeFileContent -Source $exeXmlPath -Destination (Join-Path $backupFolder 'EXE.xml')
if ($Uninstall) {
    foreach ($entry in $entries) { [void]$exeXml.DocumentElement.RemoveChild($entry) }
    $exeXml.Save($exeXmlPath)
    New-Item -ItemType File -Path (Join-Path $PSScriptRoot 'STOP') -Force | Out-Null
    Write-Output 'Autostart removed and bridge stop requested. GSX profile retained.'
    return
}
if (!$ProSimDirectory) {
    $proSimProcess = Get-Process -Name ProSimB738 -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proSimProcess -and $proSimProcess.Path) { $ProSimDirectory = Split-Path $proSimProcess.Path }
    elseif (Test-Path 'C:\ProSimB738\ProSimSDK.dll') { $ProSimDirectory = 'C:\ProSimB738' }
    else { throw 'Supply -ProSimDirectory pointing to your installed ProSim folder containing ProSimSDK.dll.' }
}
if (!(Test-Path (Join-Path $ProSimDirectory 'ProSimSDK.dll'))) { throw 'ProSimSDK.dll missing' }
$binaryPath = Join-Path $PSScriptRoot 'GSX-ProSim-Door-Bridge.exe'
if (!(Test-Path -LiteralPath $binaryPath)) { throw 'Bridge executable missing' }
$profilePath=Join-Path $AircraftProfileDirectory 'gsx.cfg'
if($state -and ($state.ProfilePath -ne $profilePath -or $state.ExePath -ne $exeXmlPath)) {throw 'Uninstall the recorded integration before changing its target folders.'}
if(!$state) {
 $exists=Test-Path -LiteralPath $profilePath
 $text=if($exists){[IO.File]::ReadAllText($profilePath)}else{[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Profiles\prosim-b738-2026-gsx.cfg'))}
 $edits=@()
 foreach($edit in @(@{Section='exit1';Code='(L:Tony_GSX_ProSim_L1_Open, bool)'},@{Section='service2';Code='(L:B738_door_3, number) 1*'})) {
  $m=[regex]::Matches($text,'(?ms)\['+$edit.Section+'\]\r?\n(?:(?!\[).)*?code\s*=\s*([^\r\n]+)')
  if($m.Count -ne 1){throw 'Unexpected GSX profile; no integration changes applied.'}
  $original=$m[0].Groups[1].Value
  # Adopt the known legacy L1 check so older packages can be safely removed.
  if($edit.Section -eq 'exit1' -and $original -eq $edit.Code){$original='(L:B738_door_0, number) 1*'}
  $edits+=@{Section=$edit.Section;Original=$original;Installed=$edit.Code}
 }
 $originalBytes=if($exists){[IO.File]::ReadAllBytes($profilePath)}else{@()}
 # For a legacy bridge profile, replace only its bridge-dependent L1 check.
 if($exists -and $text.Contains('(L:Tony_GSX_ProSim_L1_Open, bool)')) {$originalBytes=[Text.UTF8Encoding]::new($false).GetBytes($text.Replace('(L:Tony_GSX_ProSim_L1_Open, bool)','(L:B738_door_0, number) 1*'))}
 $state=[pscustomobject]@{ExePath=$exeXmlPath;ProfilePath=$profilePath;ProfileExisted=[bool]$exists;ProfileOriginal=$(if($exists){[Convert]::ToBase64String([byte[]]$originalBytes)}else{""});ProfileEdits=$edits;ProfileInstalledHash='';Animation=$false;AircraftPath=''}
}
if (!$SkipAnimationFix) {
    if (Get-Process -Name FlightSimulator2024 -ErrorAction SilentlyContinue) { throw 'Close MSFS before installing the aircraft animation fix.' }
    if (!$AircraftPackageDirectory) {
        $userCfgPath = Join-Path $SimulatorConfigDirectory 'UserCfg.opt'
        if (Test-Path -LiteralPath $userCfgPath) {
            $userCfg = [IO.File]::ReadAllText($userCfgPath)
            $packageMatch = [regex]::Match($userCfg,'(?m)^InstalledPackagesPath\s+"([^"]+)"')
            if ($packageMatch.Success) { $AircraftPackageDirectory = Join-Path $packageMatch.Groups[1].Value 'Community\prosim-b738' }
        }
        if (!$AircraftPackageDirectory) { $AircraftPackageDirectory = Join-Path $SimulatorConfigDirectory 'Packages\Community\prosim-b738' }
    }
    if($state.Animation -and $state.AircraftPath -ne $AircraftPackageDirectory){throw 'Uninstall the recorded animation integration before changing aircraft folders.'}
    $state.Animation=$true; $state.AircraftPath=$AircraftPackageDirectory
    $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding utf8
    & (Join-Path $PSScriptRoot 'Patch-L1Animation.ps1') -AircraftPackageDirectory $AircraftPackageDirectory -BackupDirectory (Join-Path $backupFolder 'Aircraft')
}
$settings = [xml]'<Bridge><ProSimDirectory/><ProSimHost/><SimulatorProcess>FlightSimulator2024</SimulatorProcess></Bridge>'
$settings.Bridge.ProSimDirectory = [string](Resolve-Path -LiteralPath $ProSimDirectory)
$settings.Bridge.ProSimHost = $ProSimHost
$settings.Save((Join-Path $PSScriptRoot 'BridgeSettings.xml'))
# Preserve existing aircraft customizations; change only the two known checks.
$profilePath = Join-Path $AircraftProfileDirectory 'gsx.cfg'
if (Test-Path -LiteralPath $profilePath) {
    Copy-BridgeFileContent -Source $profilePath -Destination (Join-Path $backupFolder 'gsx.cfg')
    $profileText = [IO.File]::ReadAllText($profilePath)
} else { $profileText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Profiles\prosim-b738-2026-gsx.cfg')) }
foreach ($edit in @(@{Section='exit1';Code='(L:Tony_GSX_ProSim_L1_Open, bool)'},@{Section='service2';Code='(L:B738_door_3, number) 1*'})) {
    $pattern = '(?ms)(\[' + $edit.Section + '\]\r?\n(?:(?!\[).)*?code\s*=\s*)[^\r\n]+'
    if ([regex]::Matches($profileText,$pattern).Count -ne 1) { throw "Expected one $($edit.Section) door check; profile requires manual review." }
    $profileText = [regex]::Replace($profileText,$pattern,('${1}' + $edit.Code))
}
New-Item -ItemType Directory -Path $AircraftProfileDirectory -Force | Out-Null
[IO.File]::WriteAllText($profilePath,$profileText,[Text.UTF8Encoding]::new($false))
$state.ProfileInstalledHash=(Get-FileHash -LiteralPath $profilePath).Hash
$state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding utf8
foreach ($entry in $entries) { [void]$exeXml.DocumentElement.RemoveChild($entry) }
$newEntry = $exeXml.CreateElement('Launch.Addon')
foreach ($pair in @(@('Name',$entryName),@('Disabled','False'),@('ManualLoad','False'),@('Path',$binaryPath),@('NewConsole','False'))) {
    $node = $exeXml.CreateElement($pair[0]); $node.InnerText = $pair[1]; [void]$newEntry.AppendChild($node)
}
[void]$exeXml.DocumentElement.AppendChild($newEntry)
$exeXml.Save($exeXmlPath)
$stopFile = Join-Path $PSScriptRoot 'STOP'
if (Test-Path -LiteralPath $stopFile) { Remove-Item -LiteralPath $stopFile }
Write-Output "Installed autostart, GSX checks and requested animation fix. Backups: $backupFolder"
Write-Output 'Keep this folder in its current location. Restart Couatl to reload the profile. Next MSFS launch starts the bridge automatically.'



