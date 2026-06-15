namespace LocalLLM.Core;

public sealed class ErrorRule
{
    public string DisplayName { get; set; } = string.Empty;

    public string Impact { get; set; } = string.Empty;

    public string[] ProbableCauses { get; set; } = [];

    public string[] RecommendedActions { get; set; } = [];
}
