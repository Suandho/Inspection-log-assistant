using System.Text.RegularExpressions;

namespace LocalLLM.Core;

public sealed class DiagnosisEngine
{
    private readonly RuleMatcher _ruleMatcher = new();
    private readonly DiagnosisMessageBuilder _messageBuilder = new();

    public DiagnosisResult Diagnose(
        UserQuestion question,
        string logRootPath,
        IReadOnlyList<string> searchedFiles,
        IReadOnlyList<LogEntry> relatedEntries,
        IReadOnlyList<LogEvent> events,
        IReadOnlyList<EventGroup> eventGroups,
        AssistantOptions options)
    {
        var diagnosisEntries = string.IsNullOrWhiteSpace(question.TargetInspection)
            ? relatedEntries
            : relatedEntries.Where(entry => entry.RawText.Contains(question.TargetInspection, StringComparison.OrdinalIgnoreCase)).ToArray();

        var diagnosisEvents = string.IsNullOrWhiteSpace(question.TargetInspection)
            ? events
            : events.Where(logEvent => logEvent.Inspection?.Equals(question.TargetInspection, StringComparison.OrdinalIgnoreCase) == true).ToArray();

        var diagnosisGroups = string.IsNullOrWhiteSpace(question.TargetInspection)
            ? eventGroups
            : GroupProblemEvents(diagnosisEvents);

        var allFailureEntries = diagnosisEntries
            .Where(entry => entry.Severity is "Error" or "Warning" ||
                            options.FailureKeywords.Any(keyword => ContainsFailureKeyword(entry.RawText, keyword)))
            .ToArray();

        var matchedRule = _ruleMatcher.Match(diagnosisGroups.FirstOrDefault(), diagnosisEvents, options);
        var summary = _messageBuilder.BuildSummary(question, searchedFiles, relatedEntries, allFailureEntries, diagnosisGroups, matchedRule);
        var suspectedCause = _messageBuilder.BuildSuspectedCause(diagnosisGroups, allFailureEntries, matchedRule);
        var recommendedActions = BuildRecommendedActions(diagnosisGroups, options, matchedRule);

        return new DiagnosisResult
        {
            Question = question,
            LogRootPath = logRootPath,
            SearchedFiles = searchedFiles,
            RelatedEntries = relatedEntries,
            FailureEntries = allFailureEntries.Take(30).ToArray(),
            Events = events,
            EventGroups = diagnosisGroups.Take(10).ToArray(),
            MatchedRule = matchedRule,
            Summary = summary,
            SuspectedCause = suspectedCause,
            RecommendedActions = recommendedActions
        };
    }

    private static IReadOnlyList<string> BuildRecommendedActions(
        IReadOnlyList<EventGroup> groups,
        AssistantOptions options,
        MatchedRule? matchedRule)
    {
        if (matchedRule is not null && matchedRule.RecommendedActions.Count > 0)
        {
            return matchedRule.RecommendedActions;
        }

        if (groups.Count == 0)
        {
            return [];
        }

        var key = groups[0].Type switch
        {
            LogEventType.IoError => "IoError",
            LogEventType.InspectionComplete => "InspectionCompleteFalse",
            LogEventType.Timeout => "Timeout",
            _ => "Default"
        };

        if (options.RecommendedActions.TryGetValue(key, out var actions) && actions.Length > 0)
        {
            return actions;
        }

        return options.RecommendedActions.TryGetValue("Default", out var defaultActions)
            ? defaultActions
            : [];
    }

    private static IReadOnlyList<EventGroup> GroupProblemEvents(IReadOnlyList<LogEvent> events)
    {
        return events
            .Where(logEvent => logEvent.Type is LogEventType.IoError or LogEventType.Timeout or LogEventType.Alarm or LogEventType.Error ||
                               (logEvent.Type == LogEventType.InspectionComplete && logEvent.Result == false))
            .GroupBy(logEvent => logEvent.GroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderBy(logEvent => logEvent.Timestamp ?? DateTime.MaxValue).ToArray();
                var first = ordered[0];
                var last = ordered[^1];
                return new EventGroup(group.Key, first.Type, DescribeEvent(first), ordered.Length, first.Timestamp, last.Timestamp, first.Source);
            })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.FirstTimestamp ?? DateTime.MaxValue)
            .ToArray();
    }

    private static string DescribeEvent(LogEvent logEvent)
    {
        return logEvent.Type switch
        {
            LogEventType.InspectionComplete when logEvent.Result == false => $"{logEvent.Inspection} 검사 결과 False",
            LogEventType.IoError => FormatModuleMessage(logEvent.Module, logEvent.Message),
            _ => logEvent.Message
        };
    }

    private static bool ContainsFailureKeyword(string text, string keyword)
    {
        if (keyword.Equals("NG", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.IsMatch(text, @"(^|[^A-Za-z0-9])NG([^A-Za-z0-9]|$)", RegexOptions.IgnoreCase);
        }

        return text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatModuleMessage(string? module, string message)
    {
        if (string.IsNullOrWhiteSpace(module) || message.StartsWith(module, StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        return $"{module} {message}";
    }
}
