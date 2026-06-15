$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")
$modelDir = Join-Path $repoRoot "models"
$binDir = Join-Path $PSScriptRoot "bin"
$serverPath = Join-Path $binDir "llama-server.exe"

New-Item -ItemType Directory -Force -Path $modelDir | Out-Null
New-Item -ItemType Directory -Force -Path $binDir | Out-Null

$models = Get-ChildItem -LiteralPath $modelDir -Filter "*.gguf" -File -ErrorAction SilentlyContinue

Write-Host "Local LLM runtime check"
Write-Host "Repository : $repoRoot"
Write-Host "Server path: $serverPath"
Write-Host "Model dir  : $modelDir"
Write-Host ""

if (Test-Path -LiteralPath $serverPath) {
    Write-Host "[OK] llama-server.exe found"
}
else {
    Write-Host "[NEED] Put llama-server.exe at tools\llama\bin\llama-server.exe"
}

if ($models.Count -gt 0) {
    Write-Host "[OK] GGUF model files found"
    foreach ($model in $models) {
        $sizeMb = [Math]::Round($model.Length / 1MB, 1)
        Write-Host "     $($model.Name) ($sizeMb MB)"
    }
}
else {
    Write-Host "[NEED] Put one small .gguf model file under models"
}

Write-Host ""
Write-Host "Recommended first model size: 0.5B~1.5B Q4 GGUF"
Write-Host "After both files are ready, run:"
Write-Host "powershell -ExecutionPolicy Bypass -File .\tools\llama\start-llama-server.ps1"
