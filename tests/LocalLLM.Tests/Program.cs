using LocalLLM.Core;
using LocalLLM.Llm;

var tests = new (string Name, Action Run)[]
{
    ("Question parser maps OT to WheelOuterDiameter", QuestionParserMapsOt),
    ("Question parser uses configured aliases", QuestionParserUsesConfiguredAliases),
    ("Question parser handles Korean time window", QuestionParserHandlesKoreanTimeWindow),
    ("Question parser detects startup failure", QuestionParserDetectsStartupFailure),
    ("Config loader reads sample config", ConfigLoaderReadsSampleConfig),
    ("Config loader reads customer rules", ConfigLoaderReadsCustomerRules),
    ("Config loader reads event patterns", ConfigLoaderReadsEventPatterns),
    ("Log event extractor detects FnIO error", LogEventExtractorDetectsFnIoError),
    ("Log event extractor uses custom event pattern", LogEventExtractorUsesCustomEventPattern),
    ("Log event extractor detects inspection false", LogEventExtractorDetectsInspectionFalse),
    ("Analyze returns structured assistant response", AnalyzeReturnsStructuredAssistantResponse),
    ("Analyze can use LLM index first", AnalyzeCanUseLlmIndexFirst),
    ("Analyze LLM index ignores normal workspace logs", AnalyzeLlmIndexIgnoresNormalWorkspaceLogs),
    ("LLM prompt builder uses structured response", LlmPromptBuilderUsesStructuredResponse),
    ("Dummy LLM answer service returns prompt", DummyLlmAnswerServiceReturnsPrompt),
    ("Compact answer service generates offline answer", CompactAnswerServiceGeneratesOfflineAnswer),
    ("LLM question understanding returns structured query", LlmQuestionUnderstandingReturnsStructuredQuery),
    ("LLM question understanding falls back for startup failure", LlmQuestionUnderstandingFallsBackForStartupFailure),
    ("Local HTTP LLM client parses chat response", LocalHttpLlmClientParsesChatResponse),
    ("Local HTTP LLM client checks health endpoint", LocalHttpLlmClientChecksHealthEndpoint),
    ("E2E diagnoses OT inspection false", E2eDiagnosesOtInspectionFalse),
    ("E2E diagnoses dawn machine stop", E2eDiagnosesDawnMachineStop),
    ("E2E diagnoses afternoon IO stop without date", E2eDiagnosesAfternoonIoStopWithoutDate)
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine($"     {exception.Message}");
    }
}

if (failed > 0)
{
    Console.WriteLine($"{failed} test(s) failed.");
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"{tests.Length} test(s) passed.");

static void QuestionParserMapsOt()
{
    var parser = new QuestionParser();
    var question = parser.Parse("20260514에 OT 검사가 왜 안되었을까?");

    AssertEqual(new DateOnly(2026, 5, 14), question.Date, "Date");
    AssertEqual("InspectionArea", question.EventName, "EventName");
    AssertEqual("WheelOuterDiameter", question.TargetInspection, "TargetInspection");
}

static void QuestionParserUsesConfiguredAliases()
{
    var parser = new QuestionParser(new AssistantOptions
    {
        InspectionAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["WheelOuterDiameter"] = ["WOD-Custom"]
        }
    });

    var question = parser.Parse("20260514에 WOD-Custom 검사가 왜 안되었을까?");

    AssertEqual("WheelOuterDiameter", question.TargetInspection, "TargetInspection");
}

static void QuestionParserHandlesKoreanTimeWindow()
{
    var parser = new QuestionParser();
    var question = parser.Parse("5월14일 새벽3시쯤 갑자기 기계가 멈췄어");

    AssertEqual(new DateOnly(DateTime.Today.Year, 5, 14), question.Date, "Date");
    AssertEqual("TimeWindow", question.EventName, "EventName");
    AssertNotNull(question.TimeWindow, "TimeWindow");
    AssertEqual(new TimeOnly(2, 30), question.TimeWindow!.Start, "TimeWindow.Start");
    AssertEqual(new TimeOnly(3, 30), question.TimeWindow.End, "TimeWindow.End");
}

static void QuestionParserDetectsStartupFailure()
{
    var parser = new QuestionParser();
    var question = parser.Parse("StartupFailure Load Error Camera PLC IO 왜 갑자기 실행이 안되는거지?");

    AssertEqual("StartupFailure", question.EventName, "EventName");
}

static void ConfigLoaderReadsSampleConfig()
{
    var options = AssistantOptionsLoader.Load(Path.Combine(FindRepositoryRoot(), "config", "assistant-settings.json"));

    AssertEqual("LLMTestLog", options.LogRootPath, "LogRootPath");
    AssertEqual(true, options.InspectionAliases["WheelOuterDiameter"].Contains("OT"), "InspectionAliases");
    AssertEqual(true, options.RecommendedActions["IoError"].Length > 0, "RecommendedActions");
}

static void ConfigLoaderReadsCustomerRules()
{
    var options = AssistantOptionsLoader.Load(Path.Combine(FindRepositoryRoot(), "config", "assistant-settings.json"));

    AssertEqual("외경 검사", options.InspectionRules["WheelOuterDiameter"].DisplayName, "InspectionRule.DisplayName");
    AssertEqual("IO 출력 비트 읽기 실패", options.ErrorRules["FnIO read output bit fail"].DisplayName, "ErrorRule.DisplayName");
}

static void ConfigLoaderReadsEventPatterns()
{
    var options = AssistantOptionsLoader.Load(Path.Combine(FindRepositoryRoot(), "config", "assistant-settings.json"));

    AssertEqual(true, options.EventPatterns.Any(pattern => pattern.Type == "InspectionComplete"), "InspectionComplete pattern");
    AssertEqual(true, options.EventPatterns.Any(pattern => pattern.Type == "IoError"), "IoError pattern");
}

static void LogEventExtractorDetectsFnIoError()
{
    var entry = new LogEntry(
        "20260514.log",
        82244,
        "26-05-14_14:01:34:778 [ERR] <> FnIO Error!: FnIO read output bit fail!",
        new DateTime(2026, 5, 14, 14, 1, 34),
        "Error");

    var events = new LogEventExtractor().Extract([entry]);

    AssertEqual(1, events.Count, "Event count");
    AssertEqual(LogEventType.IoError, events[0].Type, "Event type");
    AssertEqual("FnIO", events[0].Module, "Module");
    AssertContains(events[0].Message, "read output bit fail", "Message");
}

static void LogEventExtractorUsesCustomEventPattern()
{
    var options = new AssistantOptions
    {
        EventPatterns =
        [
            new EventPatternRule
            {
                Type = "IoError",
                Module = "CustomIO",
                Contains = ["IO_ERR", "READ_OUTPUT_BIT_FAILED"],
                Message = "READ_OUTPUT_BIT_FAILED"
            }
        ]
    };
    var entry = new LogEntry(
        "customer.log",
        10,
        "2026-05-14 14:00:00 [ERR] IO_ERR READ_OUTPUT_BIT_FAILED",
        new DateTime(2026, 5, 14, 14, 0, 0),
        "Error");

    var events = new LogEventExtractor(options).Extract([entry]);

    AssertEqual(1, events.Count, "Event count");
    AssertEqual(LogEventType.IoError, events[0].Type, "Event type");
    AssertEqual("CustomIO", events[0].Module, "Module");
    AssertEqual("READ_OUTPUT_BIT_FAILED", events[0].Message, "Message");
}

static void LogEventExtractorDetectsInspectionFalse()
{
    var entry = new LogEntry(
        "20260514.log",
        27,
        "26-05-14_00:01:38:738 [INF] <InspectionProcessThread> On Station1 InspectionComplete - WheelOuterDiameter, False",
        new DateTime(2026, 5, 14, 0, 1, 38),
        "Error");

    var events = new LogEventExtractor().Extract([entry]);

    AssertEqual(1, events.Count, "Event count");
    AssertEqual(LogEventType.InspectionComplete, events[0].Type, "Event type");
    AssertEqual("Station1", events[0].Station, "Station");
    AssertEqual("WheelOuterDiameter", events[0].Inspection, "Inspection");
    AssertEqual(false, events[0].Result, "Result");
}

static void LlmPromptBuilderUsesStructuredResponse()
{
    var response = Analyze("오후 2시쯤 기기 작동이 멈췄어");
    var prompt = new LlmPromptBuilder().Build(response);

    AssertContains(prompt, "장비 로그 분석 결과", "Prompt");
    AssertContains(prompt, "항목 제목, 번호 목록, 지시문", "Prompt");
    AssertContains(prompt, "FnIO read output bit fail", "Prompt");
    AssertContains(prompt, "IO 출력 비트 읽기 실패", "Prompt");
    AssertContains(prompt, "첫 근거 로그", "Prompt");
}

static void DummyLlmAnswerServiceReturnsPrompt()
{
    var response = Analyze("20260514에 OT 검사가 왜 안되었을까?");
    var service = new LlmAnswerService(new DummyLlmClient());
    var answer = service.GenerateAnswerAsync(response).GetAwaiter().GetResult();

    AssertContains(answer, "외경 검사", "Answer");
    AssertContains(answer, "WheelOuterDiameter 검사 결과 False", "Answer");
}

static void CompactAnswerServiceGeneratesOfflineAnswer()
{
    var response = Analyze("오후 2시쯤 기기 작동이 멈췄어");
    var answer = new CompactAnswerService().GenerateAnswer(response);

    AssertContains(answer, "FnIO read output bit fail", "Answer");
    AssertContains(answer, "적용된 고객사 규칙", "Answer");
    AssertContains(answer, "확인 조치", "Answer");
    AssertContains(answer, "첫 근거 로그", "Answer");
}

static void LlmQuestionUnderstandingReturnsStructuredQuery()
{
    var llmJson = """
        {
          "intent": "StartupFailure",
          "date": null,
          "startTime": null,
          "endTime": null,
          "targetInspection": null,
          "keywords": ["Load Error", "Fail", "Camera", "PLC", "IO"]
        }
        """;
    var service = new LlmQuestionUnderstandingService(new StaticLlmClient(llmJson));
    var structured = service.UnderstandAsync("왜 갑자기 실행이 안되는거지?").GetAwaiter().GetResult();

    AssertNotNull(structured, "StructuredQuestion");
    AssertEqual("StartupFailure", structured!.Intent, "Intent");
    AssertEqual(true, structured.Keywords.Contains("Load Error"), "Keywords");
    AssertContains(structured.ToAnalyzerQuestion(), "Load Error", "AnalyzerQuestion");
    AssertContains(structured.ToAnalyzerQuestion(), "왜 갑자기 실행이 안되는거지?", "AnalyzerQuestion");
}

static void LlmQuestionUnderstandingFallsBackForStartupFailure()
{
    var service = new LlmQuestionUnderstandingService(new StaticLlmClient("not json"));
    var structured = service.UnderstandAsync("왜 갑자기 실행이 안되는거지?").GetAwaiter().GetResult();

    AssertNotNull(structured, "StructuredQuestion");
    AssertEqual("StartupFailure", structured!.Intent, "Intent");
    AssertContains(structured.ToAnalyzerQuestion(), "Load Error", "AnalyzerQuestion");
    AssertContains(structured.ToAnalyzerQuestion(), "Load", "AnalyzerQuestion");
}

static void LocalHttpLlmClientParsesChatResponse()
{
    var handler = new StubHttpMessageHandler(
        """{"choices":[{"message":{"content":"외경 검사 NG가 반복되었습니다."}}]}""");
    using var httpClient = new HttpClient(handler);
    var client = new LocalHttpLlmClient(
        new LocalLlmOptions
        {
            Endpoint = "http://localhost:8080/v1/chat/completions",
            Model = "test-model"
        },
        httpClient);

    var answer = client.GenerateAsync("prompt").GetAwaiter().GetResult();

    AssertEqual("외경 검사 NG가 반복되었습니다.", answer, "Answer");
    AssertEqual(true, handler.RequestBody.Contains("test-model", StringComparison.Ordinal), "Request model");
    AssertEqual(true, handler.RequestBody.Contains("prompt", StringComparison.Ordinal), "Request prompt");
}

static void LocalHttpLlmClientChecksHealthEndpoint()
{
    var handler = new StubHttpMessageHandler("""{"data":[{"id":"test-model"}]}""");
    using var httpClient = new HttpClient(handler);
    var client = new LocalHttpLlmClient(
        new LocalLlmOptions
        {
            Endpoint = "http://localhost:8080/v1/chat/completions",
            Model = "test-model"
        },
        httpClient);

    var result = client.CheckHealthAsync().GetAwaiter().GetResult();

    AssertEqual(true, result.IsHealthy, "Health");
    AssertEqual("http://localhost:8080/v1/models", result.Endpoint, "Health endpoint");
    AssertEqual("GET", handler.Method, "Health method");
}

static void E2eDiagnosesOtInspectionFalse()
{
    var answer = Ask("20260514에 OT 검사가 왜 안되었을까?");

    AssertContains(answer, "WheelOuterDiameter 검사 결과 False", "Answer");
    AssertContains(answer, "고객사 규칙: 외경 검사", "Answer");
    AssertContains(answer, "10건", "Answer");
    AssertContains(answer, "권장 확인", "Answer");
}

static void AnalyzeReturnsStructuredAssistantResponse()
{
    var response = Analyze("오후 2시쯤 기기 작동이 멈췄어");

    AssertContains(response.AnswerText, "FnIO read output bit fail", "AnswerText");
    AssertEqual("TimeWindow", response.Question.EventName, "Question.EventName");
    AssertEqual("IO 출력 비트 읽기 실패", response.Diagnosis.MatchedRule?.DisplayName, "MatchedRule.DisplayName");
    AssertEqual(true, response.EventGroups.Count > 0, "EventGroups");
    AssertEqual("FnIO read output bit fail!", response.EventGroups[0].Description, "Primary Event Description");
    AssertEqual(199, response.EventGroups[0].Count, "Primary Event Count");
    AssertEqual(true, response.RecommendedActions.Count > 0, "RecommendedActions");
    AssertEqual(true, response.EvidenceLogs.Count > 0, "EvidenceLogs");
}

static void AnalyzeCanUseLlmIndexFirst()
{
    var root = FindRepositoryRoot();
    var options = AssistantOptionsLoader.Load(Path.Combine(root, "config", "assistant-settings.json"));
    options.IndexRootPath = Path.Combine(root, "LLMIndex");
    options.UseIndexFirst = true;

    var response = new AssistantService(options).Analyze("측정값이 임계값보다 큰 NG는 뭐야?");

    AssertContains(response.AnswerText, "인덱스 기준", "AnswerText");
    AssertContains(response.AnswerText, "측정값", "AnswerText");
    AssertContains(response.AnswerText, "LLMIndex", "AnswerText");
}

static void AnalyzeLlmIndexIgnoresNormalWorkspaceLogs()
{
    var root = FindRepositoryRoot();
    var options = AssistantOptionsLoader.Load(Path.Combine(root, "config", "assistant-settings.json"));
    options.IndexRootPath = Path.Combine(root, "LLMIndex");
    options.UseIndexFirst = true;

    var response = new AssistantService(options).Analyze("20260514에 OT 검사가 왜 안되었을까?");

    AssertContains(response.AnswerText, "외경 검사", "AnswerText");
    AssertContains(response.AnswerText, "WheelOuterDiameter", "AnswerText");
    AssertContains(response.AnswerText, "NG", "AnswerText");
    AssertEqual(false, response.AnswerText.Contains("WorkspaceReady", StringComparison.OrdinalIgnoreCase), "WorkspaceReady excluded");
    AssertEqual(false, response.AnswerText.Contains("IO 출력 비트 읽기 실패", StringComparison.OrdinalIgnoreCase), "Unrelated IO excluded");
}

static void E2eDiagnosesDawnMachineStop()
{
    var answer = Ask("5월14일 새벽3시쯤 갑자기 기계가 멈췄어");

    AssertContains(answer, "02:30~03:30", "Answer");
    AssertContains(answer, "WheelOuterDiameter 검사 결과 False", "Answer");
    AssertContains(answer, "205건", "Answer");
}

static void E2eDiagnosesAfternoonIoStopWithoutDate()
{
    var answer = Ask("오후 2시쯤 기기 작동이 멈췄어");

    AssertContains(answer, "13:30~14:30", "Answer");
    AssertContains(answer, "로그에서 추정", "Answer");
    AssertContains(answer, "FnIO read output bit fail", "Answer");
    AssertContains(answer, "고객사 규칙: IO 출력 비트 읽기 실패", "Answer");
    AssertContains(answer, "199건", "Answer");
}

static string Ask(string question)
{
    return CreateAssistant().Ask(question);
}

static AssistantResponse Analyze(string question)
{
    return CreateAssistant().Analyze(question);
}

static AssistantService CreateAssistant()
{
    var options = AssistantOptionsLoader.Load(Path.Combine(FindRepositoryRoot(), "config", "assistant-settings.json"));
    options.UseIndexFirst = false;
    return new AssistantService(options);
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "LocalLLM.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root could not be found.");
}

static void AssertEqual<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertContains(string text, string expected, string name)
{
    if (!text.Contains(expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"{name}: expected to contain '{expected}'. Actual: {text}");
    }
}

static void AssertNotNull(object? value, string name)
{
    if (value is null)
    {
        throw new InvalidOperationException($"{name}: expected non-null value.");
    }
}

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseBody;

    public StubHttpMessageHandler(string responseBody)
    {
        _responseBody = responseBody;
    }

    public string RequestBody { get; private set; } = string.Empty;

    public string Method { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Method = request.Method.Method;
        RequestBody = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json")
        };
    }
}
