# Embedded Integration

기존 검사 프로그램 안에서는 콘솔 앱을 실행하지 말고 `LocalLLM.Core`와 `LocalLLM.Llm`을 직접 참조하는 방식이 가장 단순합니다.

## Minimal Usage

```csharp
using LocalLLM.Core;
using LocalLLM.Llm;

var options = AssistantOptionsLoader.Load("config\\assistant-settings.json");
var assistant = new AssistantService(options);

var response = assistant.Analyze("오후 2시쯤 기기 작동이 멈췄어");
var answer = new CompactAnswerService().GenerateAnswer(response);
```

`answer`를 WPF 화면, 알림창, 고객문의 화면, 또는 로그 분석 패널에 그대로 표시하면 됩니다.

## Recommended UI Flow

```text
사용자 질문 입력
  -> AssistantService.Analyze(question)
  -> CompactAnswerService.GenerateAnswer(response)
  -> 답변, 근거 로그, 권장 확인 표시
```

LLM 서버가 있는 환경에서는 다음처럼 선택적으로 로컬 모델 답변을 사용할 수 있습니다.

```csharp
using LocalLLM.Core;
using LocalLLM.Llm;

var response = assistant.Analyze(question);
var llmOptions = LocalLlmOptions.Load("config\\assistant-settings.json");

using var llmClient = new LocalHttpLlmClient(llmOptions);
var answer = await new LlmAnswerService(llmClient).GenerateAnswerAsync(response);
```

## WPF Binding Targets

화면에서는 최소한 다음 정보를 분리해서 보여주는 편이 좋습니다.

- 최종 답변: `CompactAnswerService.GenerateAnswer(response)`
- 요약: `response.Diagnosis.Summary`
- 이벤트: `response.EventGroups`
- 권장 확인: `response.RecommendedActions`
- 근거 로그: `response.EvidenceLogs`

처음 제품 버전은 `compact` 답변만 붙이고, 이후 고객사 장비 사양이 확인되면 로컬 모델 답변을 옵션으로 켜는 구조를 권장합니다.
