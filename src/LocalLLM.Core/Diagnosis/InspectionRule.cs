namespace LocalLLM.Core;

public sealed class InspectionRule
{
    public string DisplayName { get; set; } = string.Empty;

    public string FailureMeaning { get; set; } = string.Empty;

    public string[] ProbableCauses { get; set; } = [];

    public string[] RecommendedActions { get; set; } = [];
}
