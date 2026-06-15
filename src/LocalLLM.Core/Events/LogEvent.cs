namespace LocalLLM.Core;

public sealed record LogEvent(
    LogEventType Type,
    string Message,
    string? Station,
    string? Module,
    string? Inspection,
    string? SerialNumber,
    int? GrabIndex,
    bool? Result,
    LogEntry Source)
{
    public DateTime? Timestamp => Source.Timestamp;

    public string GroupKey
    {
        get
        {
            if (Type == LogEventType.InspectionComplete && Inspection is not null && Result is not null)
            {
                return $"{Type}:{Inspection}:{Result}";
            }

            if (Type == LogEventType.IoError && Module is not null)
            {
                return $"{Type}:{Module}:{NormalizeMessage(Message)}";
            }

            return $"{Type}:{NormalizeMessage(Message)}";
        }
    }

    private static string NormalizeMessage(string message)
    {
        var trimmed = message.Trim();
        return trimmed.Length <= 120 ? trimmed : trimmed[..120];
    }
}
