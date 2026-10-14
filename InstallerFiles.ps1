function Copy-BridgeFileContent {
 param([string]$Source,[string]$Destination)
 $inputStream=$null; $outputStream=$null
 try {
  $inputStream=[IO.File]::OpenRead($Source)
  $outputStream=[IO.File]::Open($Destination,[IO.FileMode]::Create,[IO.FileAccess]::Write)
  $inputStream.CopyTo($outputStream)
 } catch {throw "Could not copy file contents. Source: $Source. Destination: $Destination. $($_.Exception.Message)"}
 finally {if($outputStream){$outputStream.Dispose()};if($inputStream){$inputStream.Dispose()}}
 if((Get-FileHash -LiteralPath $Source).Hash -ne (Get-FileHash -LiteralPath $Destination).Hash){throw "Backup verification failed: $Source -> $Destination"}
}
