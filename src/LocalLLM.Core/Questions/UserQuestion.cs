namespace LocalLLM.Core;

public sealed record UserQuestion(
    string OriginalText,
    DateOnly? Date,
    string EventName,
    string? TargetInspection,
    TimeWindow? TimeWindow);
