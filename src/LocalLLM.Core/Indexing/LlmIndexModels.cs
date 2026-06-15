namespace LocalLLM.Core;

public sealed class LlmIndexSummaryFile
{
    public string Date { get; set; } = string.Empty;

    public string GeneratedAt { get; set; } = string.Empty;

    public LlmIndexSummaryItem[] Summaries { get; set; } = [];
}

public sealed class LlmIndexSummaryItem
{
    public string TimeRange { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int Count { get; set; }

    public string? Meaning { get; set; }

    public string[] ProbableCauses { get; set; } = [];

    public string[] Actions { get; set; } = [];

    public string[] Evidence { get; set; } = [];
}

public sealed class LlmIndexEventItem
{
    public string? Time { get; set; }

    public string Level { get; set; } = "Info";

    public string Type { get; set; } = "Unknown";

    public string? Station { get; set; }

    public string? Module { get; set; }

    public string? Inspection { get; set; }

    public string? Result { get; set; }

    public string? Message { get; set; }

    public double? MeasuredValue { get; set; }

    public double? ThresholdValue { get; set; }

    public double? ReferenceValue { get; set; }

    public string? SourceFile { get; set; }

    public int SourceLine { get; set; }

    public string? RawText { get; set; }
}
