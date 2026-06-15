namespace LocalLLM.Core;

public sealed class EventPatternRule
{
    public string Type { get; set; } = "Unknown";

    public string? Module { get; set; }

    public string[] Contains { get; set; } = [];

    public string? Regex { get; set; }

    public string? Message { get; set; }

    public string? MessageAfter { get; set; }
}
