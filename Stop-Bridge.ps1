New-Item -ItemType File -Path (Join-Path $PSScriptRoot 'STOP') -Force | Out-Null
Write-Output 'Stop requested. Door positions are preserved.'
