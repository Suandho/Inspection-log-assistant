namespace LocalLLM.Core;

public sealed class StructuredQuestion
{
    public string OriginalText { get; set; } = string.Empty;

    public string Intent { get; set; } = "General";

    public DateOnly? Date { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? TargetInspection { get; set; }

    public string[] Keywords { get; set; } = [];

    public string ToAnalyzerQuestion()
    {
        var parts = new List<string>();

        if (Date is not null)
        {
            parts.Add(Date.Value.ToString("yyyyMMdd"));
        }

        if (StartTime is not null)
        {
            parts.Add($"{StartTime.Value:HH\\:mm}쯤");
        }

        if (!string.IsNullOrWhiteSpace(TargetInspection))
        {
            parts.Add(TargetInspection);
        }

        if (!string.IsNullOrWhiteSpace(Intent))
        {
            parts.Add(Intent);
        }

        parts.AddRange(Keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)));

        if (parts.Count == 0)
        {
            return OriginalText;
        }

        parts.Add(OriginalText);
        return string.Join(' ', parts.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
