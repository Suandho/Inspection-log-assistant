namespace LocalLLM.Core;

public sealed class AssistantService
{
    private readonly AssistantOptions _options;
    private readonly QuestionParser _questionParser;
    private readonly LogFileLocator _logFileLocator = new();
    private readonly LogAnalyzer _logAnalyzer = new();
    private readonly LogEventExtractor _logEventExtractor;
    private readonly DiagnosisEngine _diagnosisEngine = new();
    private readonly AnswerFormatter _answerFormatter = new();
    private readonly LlmIndexSearchService _indexSearchService = new();

    public AssistantService(AssistantOptions options)
    {
        _options = options;
        _questionParser = new QuestionParser(options);
        _logEventExtractor = new LogEventExtractor(options);
    }

    public string Ask(string question)
    {
        return Analyze(question).AnswerText;
    }

    public AssistantResponse Analyze(string question)
    {
        var parsedQuestion = _questionParser.Parse(question);
        var indexDiagnosis = _indexSearchService.TryAnalyze(parsedQuestion, _options);
        if (indexDiagnosis is not null)
        {
            return new AssistantResponse
            {
                AnswerText = _answerFormatter.Format(indexDiagnosis),
                Diagnosis = indexDiagnosis,
                EvidenceLogs = indexDiagnosis.FailureEntries.Count > 0
                    ? indexDiagnosis.FailureEntries
                    : indexDiagnosis.RelatedEntries.Take(10).ToArray()
            };
        }

        var files = _logFileLocator.FindFiles(_options.LogRootPath, parsedQuestion.Date);
        var relatedEntries = _logAnalyzer.ExtractRelatedEntries(files, parsedQuestion, _options);
        var events = _logEventExtractor.Extract(relatedEntries);
        var eventGroups = _logEventExtractor.Group(events);
        var diagnosis = _diagnosisEngine.Diagnose(parsedQuestion, _options.LogRootPath, files, relatedEntries, events, eventGroups, _options);
        var answerText = _answerFormatter.Format(diagnosis);

        return new AssistantResponse
        {
            AnswerText = answerText,
            Diagnosis = diagnosis,
            EvidenceLogs = diagnosis.FailureEntries.Count > 0
                ? diagnosis.FailureEntries
                : diagnosis.RelatedEntries.Take(10).ToArray()
        };
    }
}
