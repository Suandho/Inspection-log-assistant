param(
    [string]$OutputDirectory = "",
    [string]$AssetPattern = "win.*cpu.*x64.*\.zip$"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot "bin"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$apiUrl = "https://api.github.com/repos/ggml-org/llama.cpp/releases/latest"
$headers = @{
    "User-Agent" = "LocalLLM-Setup"
    "Accept" = "application/vnd.github+json"
}

Write-Host "Querying latest llama.cpp release..."
$release = Invoke-RestMethod -Uri $apiUrl -Headers $headers

$asset = $release.assets |
    Where-Object {
        $_.name -match $AssetPattern -and
        $_.name -notmatch "cudart" -and
        $_.name -notmatch "sycl"
    } |
    Select-Object -First 1

if ($null -eq $asset) {
    throw "No matching llama.cpp release asset was found. Pattern: $AssetPattern"
}

$zipPath = Join-Path $env:TEMP $asset.name
$extractPath = Join-Path $env:TEMP ([IO.Path]::GetFileNameWithoutExtension($asset.name))

Write-Host "Release: $($release.tag_name)"
Write-Host "Asset  : $($asset.name)"
Write-Host "URL    : $($asset.browser_download_url)"
Write-Host "Downloading..."

Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath

if (Test-Path -LiteralPath $extractPath) {
    Remove-Item -LiteralPath $extractPath -Recurse -Force
}

Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force

$server = Get-ChildItem -LiteralPath $extractPath -Recurse -File -Filter "llama-server.exe" |
    Select-Object -First 1

if ($null -eq $server) {
    throw "llama-server.exe was not found in the downloaded archive."
}

$runtimeDirectory = $server.Directory.FullName
Copy-Item -Path (Join-Path $runtimeDirectory "*") -Destination $OutputDirectory -Recurse -Force

$target = Join-Path $OutputDirectory "llama-server.exe"

Write-Host "Installed: $target"
