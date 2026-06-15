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

## 로컬 모델 파일 준비

이 저장소에는 `.gguf` 모델 파일을 포함하지 않습니다. 모델 파일은 용량이 크고 라이선스가 모델마다 다르기 때문에, 실행 환경에서 별도로 다운로드해야 합니다.

`local` 또는 `llama` 모드를 사용하려면 다음 파일을 준비하세요.

```text
models\*.gguf
tools\llama\bin\llama-server.exe
```

권장 모델은 0.5B~3B 크기의 instruct GGUF 모델입니다. 예를 들어 Qwen, Llama, Phi 계열의 GGUF 모델 중 배포 조건에 맞는 파일을 선택할 수 있습니다.

모델 다운로드 URL이 있는 경우 다음 스크립트를 사용할 수 있습니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\llama\download-model.ps1 -ModelUrl "https://example.com/model.gguf"
```

다운로드한 `.gguf` 파일은 `models` 폴더에 두고, `config\assistant-settings.json`의 `llm.endpoint`가 로컬 llama.cpp 서버 주소를 가리키는지 확인하세요.

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
