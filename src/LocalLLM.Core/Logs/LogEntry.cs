namespace LocalLLM.Core;

public sealed record LogEntry(
    string FilePath,
    int LineNumber,
    string RawText,
    DateTime? Timestamp,
    string Severity);
