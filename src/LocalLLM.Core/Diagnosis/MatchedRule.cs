namespace LocalLLM.Core;

public sealed record MatchedRule(
    string RuleType,
    string Key,
    string DisplayName,
    string Meaning,
    IReadOnlyList<string> ProbableCauses,
    IReadOnlyList<string> RecommendedActions);
