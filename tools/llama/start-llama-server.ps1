param(
    [string]$LlamaServerPath = "",

    [string]$ModelPath = "",

    [string]$HostAddress = "127.0.0.1",
    [int]$Port = 8080,
    [int]$ContextSize = 2048,
    [int]$Threads = 4,
    [int]$GpuLayers = 0
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($LlamaServerPath)) {
    $defaultServer = Join-Path $PSScriptRoot "bin\llama-server.exe"
    if (Test-Path -LiteralPath $defaultServer) {
        $LlamaServerPath = $defaultServer
    }
}

if ([string]::IsNullOrWhiteSpace($ModelPath)) {
    $modelRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..\models")
    $model = Get-ChildItem -LiteralPath $modelRoot -Filter "*.gguf" -File |
        Sort-Object Length |
        Select-Object -First 1

    if ($null -ne $model) {
        $ModelPath = $model.FullName
    }
}

if ([string]::IsNullOrWhiteSpace($LlamaServerPath) -or -not (Test-Path -LiteralPath $LlamaServerPath)) {
    throw "llama-server.exe was not found. Put it under tools\llama\bin or pass -LlamaServerPath."
}

if ([string]::IsNullOrWhiteSpace($ModelPath) -or -not (Test-Path -LiteralPath $ModelPath)) {
    throw "GGUF model file was not found. Put a .gguf file under models or pass -ModelPath."
}

$resolvedServer = Resolve-Path -LiteralPath $LlamaServerPath
$resolvedModel = Resolve-Path -LiteralPath $ModelPath

Write-Host "Starting local LLM server..."
Write-Host "Server : $resolvedServer"
Write-Host "Model  : $resolvedModel"
Write-Host "URL    : http://$HostAddress`:$Port"

& $resolvedServer `
    --model $resolvedModel `
    --host $HostAddress `
    --port $Port `
    --ctx-size $ContextSize `
    --threads $Threads `
    --n-gpu-layers $GpuLayers
