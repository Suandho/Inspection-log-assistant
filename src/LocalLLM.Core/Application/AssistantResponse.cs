namespace LocalLLM.Core;

public sealed class AssistantResponse
{
    public required string AnswerText { get; init; }

    public required DiagnosisResult Diagnosis { get; init; }

    public UserQuestion Question => Diagnosis.Question;

    public IReadOnlyList<EventGroup> EventGroups => Diagnosis.EventGroups;

    public IReadOnlyList<string> RecommendedActions => Diagnosis.RecommendedActions;

    public IReadOnlyList<LogEntry> EvidenceLogs { get; init; } = [];
}
