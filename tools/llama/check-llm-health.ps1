param(
    [string]$ConfigPath = "config\assistant-settings.json",
    [int]$TimeoutSeconds = 3
)

$ErrorActionPreference = "Stop"

dotnet run --project src\LocalLLM.App -- `
    --config $ConfigPath `
    --llm-health `
    --llm-timeout-seconds $TimeoutSeconds
