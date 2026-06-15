param(
    [string]$Question = "20260514에 OT 검사가 왜 안되었을까?",
    [string]$ConfigPath = "config\assistant-settings.json"
)

$ErrorActionPreference = "Stop"

dotnet run --project src\LocalLLM.App -- `
    --config $ConfigPath `
    --llm llama `
    --question $Question
