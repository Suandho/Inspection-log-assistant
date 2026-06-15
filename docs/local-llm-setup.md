# Local LLM Setup

This project is designed for free local models first. It does not require a paid external API.

## Recommended First Mode

Use `compact` mode for the first product version.

```powershell
dotnet run --project src\LocalLLM.App -- --config config\assistant-settings.json --llm compact --question "오후 2시쯤 기기 작동이 멈췄어"
```

This mode does not require a model file, GPU, internet access, or a local LLM server. The C# analyzer creates the diagnosis and formats the customer answer.

## Local Model Mode

Use `llama` mode when a small local model server is available.

Recommended first model size:

- 0.5B: very small, lower answer quality
- 1B to 1.5B: good starting point for log-answer rewriting
- 3B: better quality, higher memory and latency

Runtime structure:

```text
Inspection Program
  -> LocalLLM.Core: log search, rule matching, evidence extraction
  -> LocalLLM.Llm compact: no-model customer answer
  -> LocalLLM.Llm llama: optional small local model answer
```

## Prepare Runtime Folders

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\prepare-local-llm.ps1
```

Default file locations:

```text
tools\llama\bin\llama-server.exe
models\*.gguf
```

Put `llama-server.exe` under `tools\llama\bin`.

Put one small `.gguf` model under `models`.

## Optional Download Helpers

Download the latest Windows CPU llama.cpp release from the official GitHub repository:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\download-llama-cpp.ps1
```

Download a GGUF model when you have a direct model URL:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\download-model.ps1 -ModelUrl "https://example.com/model.gguf"
```

Only use model files whose license allows your deployment scenario.

## Start llama.cpp Server

If files are in the default locations:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\start-llama-server.ps1
```

Or pass explicit paths:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\start-llama-server.ps1 `
  -LlamaServerPath "C:\llama.cpp\llama-server.exe" `
  -ModelPath "C:\models\qwen2.5-1.5b-instruct-q4.gguf" `
  -Port 8080 `
  -ContextSize 2048 `
  -Threads 4 `
  -GpuLayers 0
```

Use `GpuLayers 0` for CPU-only machines.

## Health Check

After the server starts:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\check-llm-health.ps1
```

Expected success text:

```text
로컬 LLM 서버 연결 성공
```

## Stop Server

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\stop-llama-server.ps1
```

## Ask Through Local Model

```powershell
dotnet run --project src\LocalLLM.App -- --config config\assistant-settings.json --llm llama --question "20260514에 OT 검사가 왜 안되었을까?"
```

Or:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\ask-local-llm.ps1 -Question "오후 2시쯤 기기 작동이 멈췄어"
```

## Configuration

`config\assistant-settings.json` controls the local server endpoint.

```json
{
  "llm": {
    "endpoint": "http://127.0.0.1:8080/v1/chat/completions",
    "model": "local-model",
    "temperature": 0.1,
    "maxTokens": 512,
    "timeoutSeconds": 120
  }
}
```

To avoid paid external APIs, keep the endpoint on `127.0.0.1` or a private network address.
