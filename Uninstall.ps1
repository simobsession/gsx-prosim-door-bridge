param([switch]$PlanOnly)
$ErrorActionPreference='Stop'
$statePath=Join-Path $PSScriptRoot 'IntegrationState.json'
if(!(Test-Path -LiteralPath $statePath)) { throw 'No restoration record exists in this folder. For a pre-0.2.6 installation, apply configuration with 0.2.6 first to record the existing integration before uninstalling.' }
$state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if(Get-Process -Name FlightSimulator2024 -ErrorAction SilentlyContinue) { throw 'Close MSFS before uninstalling.' }
$profileBytes=$null
$profileDelete=$false
if(Test-Path -LiteralPath $state.ProfilePath) {
 $current=[IO.File]::ReadAllBytes($state.ProfilePath)
 $text=[Text.Encoding]::UTF8.GetString($current)
 if(!$state.ProfileExisted) {
  if((Get-FileHash -LiteralPath $state.ProfilePath).Hash -ne $state.ProfileInstalledHash) {throw 'The GSX profile created by this utility was subsequently modified. Save or remove that profile before uninstalling; it will not be deleted automatically.'}
  $profileDelete=$true
 } elseif((Get-FileHash -LiteralPath $state.ProfilePath).Hash -eq $state.ProfileInstalledHash) {
  $profileBytes=[Convert]::FromBase64String($state.ProfileOriginal)
 } else {
  foreach($edit in $state.ProfileEdits) {
   $pattern='(?ms)(\['+$edit.Section+'\]\r?\n(?:(?!\[).)*?code\s*=\s*)([^\r\n]+)'
   $matches=[regex]::Matches($text,$pattern)
   if($matches.Count -ne 1) {throw "GSX section $($edit.Section) changed; restoration requires review."}
   if($matches[0].Groups[2].Value -eq $edit.Installed) {$text=[regex]::Replace($text,$pattern,('${1}'+$edit.Original))}
   elseif($matches[0].Groups[2].Value -ne $edit.Original) {throw "GSX check $($edit.Section) changed; restoration requires review."}
  }
  $profileBytes=[Text.UTF8Encoding]::new($false).GetBytes($text)
 }
}
$xml=$null
if(Test-Path -LiteralPath $state.ExePath) {
 $xml=[xml][IO.File]::ReadAllText($state.ExePath)
 foreach($entry in @($xml.SelectNodes('/SimBase.Document/Launch.Addon') | Where-Object {$_.Name -eq 'GSX ProSim Door Bridge'})) {[void]$xml.DocumentElement.RemoveChild($entry)}
}
if($state.Animation) {
 & (Join-Path $PSScriptRoot 'Patch-L1Animation.ps1') -AircraftPackageDirectory $state.AircraftPath -RemoveFix -ValidateOnly
}
if($PlanOnly) {Write-Output 'Restoration preflight passed.';return}
New-Item -ItemType File -Path (Join-Path $PSScriptRoot 'STOP') -Force | Out-Null
$deadline=(Get-Date).AddSeconds(15)
do {
 $running=@(Get-Process -Name GSX-ProSim-Door-Bridge -ErrorAction SilentlyContinue | Where-Object { $_.Path -and (Split-Path $_.Path) -eq $PSScriptRoot })
 if(!$running.Count){break}
 Start-Sleep -Milliseconds 250
} while((Get-Date) -lt $deadline)
if($running.Count){throw 'The bridge has not exited. Exit it from the tray and retry; restoration records are retained.'}
if($state.Animation) {& (Join-Path $PSScriptRoot 'Patch-L1Animation.ps1') -AircraftPackageDirectory $state.AircraftPath -RemoveFix}
if($profileDelete){Remove-Item -LiteralPath $state.ProfilePath}
elseif($null -ne $profileBytes){[IO.File]::WriteAllBytes($state.ProfilePath,$profileBytes)}
if($xml){$xml.Save($state.ExePath)}
foreach($name in @('IntegrationState.json','BridgeSettings.xml','STOP','bridge-exe.log')) {
 $path=Join-Path $PSScriptRoot $name
 if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}
}
Write-Output 'Integration removed: GSX checks restored, requested animation fix reversed, autostart removed and runtime settings/log cleared. You may now delete the portable folder, including its local backups. No Windows app registration or service was installed.'
