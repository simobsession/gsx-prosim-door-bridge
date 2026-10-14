param(
    [Parameter(Mandatory=$true)][string]$AircraftPackageDirectory,
    [string]$BackupDirectory = (Join-Path $PSScriptRoot ('Backups\animation-' + [guid]::NewGuid().ToString('N'))),
    [switch]$RemoveFix,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InstallerFiles.ps1')
$originalCode = '(L:B738_door_0, number) 1 *'
$fixedCode = '(L:B738_door_0, number) (A:INTERACTIVE POINT OPEN:0, percent) 1.99 * max'
$pattern = '(?s)(<Name>Pass_Door_Left_Front</Name>.*?<Code>)(.*?)(</Code>)'
$layoutPath = Join-Path $AircraftPackageDirectory 'layout.json'
$layout = [IO.File]::ReadAllText($layoutPath) | ConvertFrom-Json
$changes = @()
foreach ($variant in @('Blended','Scimitar')) {
    $relative = "SimObjects/Airplanes/prosim-b738-2026/model.$variant/prosim-B738-2026.xml"
    $path = Join-Path $AircraftPackageDirectory $relative
    $text = [IO.File]::ReadAllText($path)
    $matches = [regex]::Matches($text,$pattern)
    if ($matches.Count -ne 1) { throw "Expected one L1 animation in $relative" }
    $currentCode = $matches[0].Groups[2].Value.Trim()
    if ($currentCode -ne $originalCode -and $currentCode -ne $fixedCode) { throw "Unrecognized L1 animation in $relative; no files changed." }
    $entries = @($layout.content | Where-Object path -eq $relative)
    if ($entries.Count -ne 1) { throw "Missing or duplicate layout registration: $relative" }
    $targetCode = if ($RemoveFix) { $originalCode } else { $fixedCode }
    $newText = [regex]::Replace($text,$pattern,('${1}' + $targetCode + '${3}'))
    [xml]$validated = '<root>' + [regex]::Replace($newText,'<\?xml[^?]*\?>','') + '</root>'
    $changes += @{Path=$path; Relative=$relative; Text=$newText; Entry=$entries[0]; Changed=($currentCode -ne $targetCode)}
}
if ($ValidateOnly) { Write-Output 'Animation restoration preflight passed.'; return }
if (!@($changes | Where-Object Changed).Count) { Write-Output 'L1 animation already matches requested configuration.'; return }
New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
Copy-BridgeFileContent -Source $layoutPath -Destination (Join-Path $BackupDirectory 'layout.json')
foreach ($change in $changes) {
    $savedPath = Join-Path $BackupDirectory $change.Relative
    New-Item -ItemType Directory -Path (Split-Path $savedPath) -Force | Out-Null
    Copy-BridgeFileContent -Source $change.Path -Destination $savedPath
}
try {
    foreach ($change in $changes) {
        if ($change.Changed) { [IO.File]::WriteAllText($change.Path,$change.Text,[Text.UTF8Encoding]::new($false)) }
        $file = Get-Item -LiteralPath $change.Path
        $change.Entry.size = $file.Length
        $change.Entry.date = $file.LastWriteTimeUtc.ToFileTimeUtc()
    }
    [IO.File]::WriteAllText($layoutPath,($layout | ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
    $verifiedLayout = [IO.File]::ReadAllText($layoutPath) | ConvertFrom-Json
    foreach ($change in $changes) {
        $file = Get-Item -LiteralPath $change.Path
        $entry = $verifiedLayout.content | Where-Object path -eq $change.Relative
        if ($entry.size -ne $file.Length -or $entry.date -ne $file.LastWriteTimeUtc.ToFileTimeUtc()) { throw 'Layout verification failed' }
    }
} catch {
    foreach ($change in $changes) { Copy-BridgeFileContent -Source (Join-Path $BackupDirectory $change.Relative) -Destination $change.Path }
    Copy-BridgeFileContent -Source (Join-Path $BackupDirectory 'layout.json') -Destination $layoutPath
    throw
}
Write-Output "L1 animation updated in both variants; backup: $BackupDirectory"

