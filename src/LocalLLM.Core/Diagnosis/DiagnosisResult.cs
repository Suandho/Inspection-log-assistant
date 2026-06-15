namespace LocalLLM.Core;

public sealed class DiagnosisResult
{
    public required UserQuestion Question { get; init; }

    public required string LogRootPath { get; init; }

    public IReadOnlyList<string> SearchedFiles { get; init; } = [];

    public IReadOnlyList<LogEntry> RelatedEntries { get; init; } = [];

    public IReadOnlyList<LogEntry> FailureEntries { get; init; } = [];

    public IReadOnlyList<LogEvent> Events { get; init; } = [];

    public IReadOnlyList<EventGroup> EventGroups { get; init; } = [];

    public MatchedRule? MatchedRule { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string SuspectedCause { get; init; } = string.Empty;

    public IReadOnlyList<string> RecommendedActions { get; init; } = [];

    public bool HasEnoughEvidence => FailureEntries.Count > 0 || RelatedEntries.Count > 0;
}
