namespace LocalLLM.Core;

public sealed class RuleMatcher
{
    public MatchedRule? Match(EventGroup? primaryGroup, IReadOnlyList<LogEvent> events, AssistantOptions options)
    {
        if (primaryGroup is null)
        {
            return null;
        }

        if (primaryGroup.Type == LogEventType.InspectionComplete)
        {
            var inspection = events
                .FirstOrDefault(logEvent => logEvent.Type == LogEventType.InspectionComplete &&
                                            logEvent.Result == false &&
                                            logEvent.Source == primaryGroup.FirstEntry)
                ?.Inspection;

            if (inspection is not null && options.InspectionRules.TryGetValue(inspection, out var rule))
            {
                return new MatchedRule(
                    "Inspection",
                    inspection,
                    string.IsNullOrWhiteSpace(rule.DisplayName) ? inspection : rule.DisplayName,
                    rule.FailureMeaning,
                    rule.ProbableCauses,
                    rule.RecommendedActions);
            }
        }

        if (primaryGroup.Type is LogEventType.IoError or LogEventType.Error or LogEventType.Timeout or LogEventType.Alarm)
        {
            foreach (var (pattern, rule) in options.ErrorRules)
            {
                if (primaryGroup.Description.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    primaryGroup.FirstEntry.RawText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return new MatchedRule(
                        "Error",
                        pattern,
                        string.IsNullOrWhiteSpace(rule.DisplayName) ? pattern : rule.DisplayName,
                        rule.Impact,
                        rule.ProbableCauses,
                        rule.RecommendedActions);
                }
            }
        }

        return null;
    }
}
