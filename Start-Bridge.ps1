$ErrorActionPreference = 'Stop'
$stopFile = Join-Path $PSScriptRoot 'STOP'
if (Test-Path -LiteralPath $stopFile) { Remove-Item -LiteralPath $stopFile }
Start-Process -FilePath (Join-Path $PSScriptRoot 'GSX-ProSim-Door-Bridge.exe') -WindowStyle Hidden
Write-Output 'Compiled bridge launched; it exits when MSFS 2024 closes.'
