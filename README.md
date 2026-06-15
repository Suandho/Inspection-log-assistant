# LocalLLM

Offline-first C# MVP for log-based customer support answers.

## Run

```powershell
dotnet run --project src\LocalLLM.App -- --log-root samples\logs --question "20261104에 왜 검사 Start가 안되었을까?"
```

```powershell
dotnet run --project src\LocalLLM.App -- --config config\assistant-settings.json --question "오후 2시쯤 기기 작동이 멈췄어"
```

The first stage does not call an LLM. It parses the question, finds date-matched logs, extracts related lines, and returns evidence-based diagnosis text.

## Free Offline Modes

`compact` mode does not use a model or internet. It formats the C# diagnosis result into a customer-facing answer.

```powershell
dotnet run --project src\LocalLLM.App -- --config config\assistant-settings.json --llm compact --question "오후 2시쯤 기기 작동이 멈췄어"
```

`local` or `llama` mode is for a free local model server such as llama.cpp. It sends HTTP requests to the configured local endpoint and does not call any paid external API unless you explicitly configure an external endpoint.

```powershell
dotnet run --project src\LocalLLM.App -- --config config\assistant-settings.json --llm llama --question "20260514에 OT 검사가 왜 안되었을까?"
```

Check whether the local model server is reachable:

```powershell
dotnet run --project src\LocalLLM.App -- --config config\assistant-settings.json --llm-health
```

Prepare local llama.cpp runtime:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\prepare-local-llm.ps1
```

Recommended small local models for this use case are 0.5B to 3B instruct GGUF models. The C# code already performs log search, rule matching, and evidence extraction, so the model only needs to rewrite the result in natural Korean.

More details:

- Local model setup: [docs/local-llm-setup.md](docs/local-llm-setup.md)
- Embedded application usage: [docs/embedded-integration.md](docs/embedded-integration.md)

## Embedded Sample

```powershell
dotnet run --project samples\EmbeddedIntegration
```

## Verify

```powershell
dotnet run --project tests\LocalLLM.Tests
```
