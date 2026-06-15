namespace LocalLLM.Core;

public sealed record EventGroup(
    string Key,
    LogEventType Type,
    string Description,
    int Count,
    DateTime? FirstTimestamp,
    DateTime? LastTimestamp,
    LogEntry FirstEntry);
