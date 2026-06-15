param(
    [Parameter(Mandatory = $true)]
    [string]$ModelUrl,

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$modelRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..\models")

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $fileName = [IO.Path]::GetFileName(([Uri]$ModelUrl).AbsolutePath)
    if ([string]::IsNullOrWhiteSpace($fileName) -or -not $fileName.EndsWith(".gguf", [StringComparison]::OrdinalIgnoreCase)) {
        throw "ModelUrl must point to a .gguf file or pass -OutputPath."
    }

    $OutputPath = Join-Path $modelRoot $fileName
}

Write-Host "Downloading model..."
Write-Host "URL   : $ModelUrl"
Write-Host "Output: $OutputPath"

Invoke-WebRequest -Uri $ModelUrl -OutFile $OutputPath

Write-Host "Downloaded: $OutputPath"
