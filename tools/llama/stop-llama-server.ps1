$ErrorActionPreference = "Stop"

$processes = Get-Process llama-server -ErrorAction SilentlyContinue

if ($null -eq $processes) {
    Write-Host "llama-server is not running."
    return
}

foreach ($process in $processes) {
    Write-Host "Stopping llama-server pid=$($process.Id)"
    Stop-Process -Id $process.Id -Force
}

Write-Host "llama-server stopped."
