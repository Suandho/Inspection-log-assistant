namespace LocalLLM.Llm;

public sealed class QuestionUnderstandingPromptBuilder
{
    public string Build(string question)
    {
        return """
        사용자 질문을 로그 분석 프로그램이 이해하기 쉬운 JSON으로 변환하세요.
        JSON만 출력하세요. 설명 문장은 쓰지 마세요.

        intent 후보:
        - StartupFailure: 프로그램 실행, 시작, 로딩, 장치 초기화 실패
        - MachineStop: 기계 멈춤, 동작 정지
        - InspectionFailure: 검사 실패, NG, False
        - General: 그 외

        keywords에는 로그 검색에 필요한 영어 키워드를 넣으세요.
        실행/시작 실패 질문이면 Load Error, Fail, Camera, PLC, IO를 넣으세요.

        출력 JSON 형식:
        {
          "intent": "StartupFailure",
          "date": null,
          "startTime": null,
          "endTime": null,
          "targetInspection": null,
          "keywords": ["Load Error", "Fail", "Camera", "PLC", "IO"]
        }

        사용자 질문:
        """ + Environment.NewLine + question;
    }
}
