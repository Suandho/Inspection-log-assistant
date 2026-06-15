using System.Text.Json;
using System.Text.RegularExpressions;

namespace LocalLLM.Core;

public sealed class LlmIndexSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public DiagnosisResult? TryAnalyze(UserQuestion question, AssistantOptions options)
    {
        if (!options.UseIndexFirst || string.IsNullOrWhiteSpace(options.IndexRootPath) || !Directory.Exists(options.IndexRootPath))
        {
            return null;
        }

        var summaryPath = FindSummaryPath(options.IndexRootPath, question.Date);
        if (summaryPath is null)
        {
            return null;
        }

        var summaryFile = JsonSerializer.Deserialize<LlmIndexSummaryFile>(File.ReadAllText(summaryPath), JsonOptions);
        if (summaryFile is null || summaryFile.Summaries.Length == 0)
        {
            return null;
        }

        var scored = summaryFile.Summaries
            .Where(IsDiagnosticCandidate)
            .Select(item => new
            {
                Item = item,
                Score = Score(item, question, options),
                MatchesTargetInspection = !string.IsNullOrWhiteSpace(question.TargetInspection) &&
                                          MatchesInspection(item, question.TargetInspection, options)
            })
            .Where(item => item.Score > 0)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(question.TargetInspection) &&
            scored.Any(item => item.MatchesTargetInspection))
        {
            scored = scored
                .Where(item => item.MatchesTargetInspection)
                .ToArray();
        }

        var ranked = scored
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => TypePriority(item.Item.Type))
            .ThenByDescending(item => item.Item.Count)
            .Take(5)
            .Select(item => item.Item)
            .ToArray();

        if (ranked.Length == 0)
        {
            return null;
        }

        var date = DateOnly.TryParse(summaryFile.Date, out var parsedDate)
            ? parsedDate
            : question.Date;
        var eventsPath = date is null
            ? null
            : Path.Combine(options.IndexRootPath, $"{date:yyyyMMdd}.events.ndjson");
        var evidenceEntries = LoadEvidenceEntries(eventsPath, ranked).ToArray();
        var groups = ranked.Select((item, index) => ToEventGroup(item, evidenceEntries.ElementAtOrDefault(index))).ToArray();
        var primary = ranked[0];
        var matchedRule = ToMatchedRule(primary);
        var summary = BuildSummary(question, summaryFile.Date, primary);

        return new DiagnosisResult
        {
            Question = question,
            LogRootPath = options.IndexRootPath,
            SearchedFiles = [summaryPath],
            RelatedEntries = evidenceEntries,
            FailureEntries = evidenceEntries,
            Events = [],
            EventGroups = groups,
            MatchedRule = matchedRule,
            Summary = summary,
            SuspectedCause = primary.ProbableCauses.Length > 0
                ? string.Join(" / ", primary.ProbableCauses)
                : primary.Meaning ?? primary.Title,
            RecommendedActions = primary.Actions
        };
    }

    private static string? FindSummaryPath(string indexRootPath, DateOnly? date)
    {
        if (date is not null)
        {
            var dated = Path.Combine(indexRootPath, $"{date:yyyyMMdd}.summary.json");
            return File.Exists(dated) ? dated : null;
        }

        return Directory.EnumerateFiles(indexRootPath, "*.summary.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
    }

    private static int Score(LlmIndexSummaryItem item, UserQuestion question, AssistantOptions options)
    {
        var text = $"{item.Type} {item.Title} {item.Meaning}".Trim();
        var score = 0;
        var asksValueCondition = question.OriginalText.Contains("측정", StringComparison.OrdinalIgnoreCase) ||
                                 question.OriginalText.Contains("임계", StringComparison.OrdinalIgnoreCase) ||
                                 question.OriginalText.Contains("기준", StringComparison.OrdinalIgnoreCase);

        if (asksValueCondition && !item.Type.Equals("InspectionNgValue", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (!asksValueCondition && item.Type.Equals("InspectionNgValue", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (question.EventName == "StartupFailure" && item.Type.Equals("StartupFailure", StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (question.EventName == "TimeWindow" && TimeRangeIntersects(item.TimeRange, question.TimeWindow))
        {
            score += 80;
        }

        if (!string.IsNullOrWhiteSpace(question.TargetInspection))
        {
            var matchesInspection = MatchesInspection(item, question.TargetInspection, options);
            if (IsInspectionSummary(item) && !matchesInspection)
            {
                return 0;
            }

            if (matchesInspection)
            {
                score += 100;
            }
        }

        if (asksValueCondition)
        {
            if (item.Type.Equals("InspectionNgValue", StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
        }

        foreach (var token in ExtractTokens(question.OriginalText))
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
        }

        if (score == 0 && item.Count > 1)
        {
            score = 1;
        }

        return score;
    }

    private static bool IsDiagnosticCandidate(LlmIndexSummaryItem item)
    {
        if (IsKnownNormalInfo(item))
        {
            return false;
        }

        return item.Type switch
        {
            "InspectionFail" or "InspectionNgValue" or "IoError" or "StartupFailure" or "Timeout" or "Alarm" or "CameraError" => true,
            "Error" => HasFailureSignal($"{item.Title} {item.Meaning}"),
            _ => false
        };
    }

    private static bool IsInspectionSummary(LlmIndexSummaryItem item) =>
        item.Type.Equals("InspectionFail", StringComparison.OrdinalIgnoreCase) ||
        item.Type.Equals("InspectionNgValue", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesInspection(
        LlmIndexSummaryItem item,
        string targetInspection,
        AssistantOptions options)
    {
        var text = $"{item.Title} {item.Meaning}".Trim();
        if (text.Contains(targetInspection, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (options.InspectionAliases.TryGetValue(targetInspection, out var aliases) &&
            aliases.Any(alias => ContainsAlias(text, alias)))
        {
            return true;
        }

        return options.InspectionRules.TryGetValue(targetInspection, out var rule) &&
               !string.IsNullOrWhiteSpace(rule.DisplayName) &&
               text.Contains(rule.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAlias(string text, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        if (alias.Length <= 2)
        {
            return Regex.IsMatch(
                text,
                $@"(^|[^A-Za-z0-9]){Regex.Escape(alias)}([^A-Za-z0-9]|$)",
                RegexOptions.IgnoreCase);
        }

        return text.Contains(alias, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownNormalInfo(LlmIndexSummaryItem item)
    {
        var text = $"{item.Title} {item.Meaning}";
        return text.Contains("[INF]", StringComparison.OrdinalIgnoreCase) &&
               (text.Contains("WorkspaceReady", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("워크스페이스 체크", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Load Complete", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Inspection Starting", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("InspectionAreaDoubleClick", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasFailureSignal(string text)
    {
        return text.Contains("[ERR]", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Fail", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Alarm", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("NotReady", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Not Ready", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(text, @"(^|[^A-Za-z0-9])NG([^A-Za-z0-9]|$)", RegexOptions.IgnoreCase);
    }

    private static int TypePriority(string type)
    {
        return type switch
        {
            "InspectionNgValue" => 100,
            "InspectionFail" => 90,
            "IoError" => 80,
            "StartupFailure" => 70,
            "Timeout" => 60,
            "Alarm" => 50,
            "CameraError" => 40,
            "Error" => 30,
            _ => 0
        };
    }

    private static IEnumerable<string> ExtractTokens(string question)
    {
        var tokens = question
            .Split([' ', ',', '.', '?', '!', '/', '\\', ':', ';', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3);

        foreach (var token in tokens)
        {
            yield return token;
        }
    }

    private static bool TimeRangeIntersects(string timeRange, TimeWindow? window)
    {
        if (window is null)
        {
            return false;
        }

        var parts = timeRange.Split('~', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !TimeOnly.TryParse(parts[0], out var start) ||
            !TimeOnly.TryParse(parts[1], out var end))
        {
            return false;
        }

        return window.Contains(DateTime.Today.Add(start.ToTimeSpan())) ||
               window.Contains(DateTime.Today.Add(end.ToTimeSpan())) ||
               (start <= window.Start && end >= window.End);
    }

    private static IReadOnlyList<LogEntry> LoadEvidenceEntries(string? eventsPath, IReadOnlyList<LlmIndexSummaryItem> summaries)
    {
        if (eventsPath is null || !File.Exists(eventsPath))
        {
            return summaries.Select(item => ToSyntheticEntry(item, item.Evidence.FirstOrDefault())).ToArray();
        }

        var evidenceKeys = summaries
            .SelectMany(item => item.Evidence)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entries = new List<LogEntry>();

        foreach (var line in File.ReadLines(eventsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<LlmIndexEventItem>(line, JsonOptions);
            if (item?.SourceFile is null)
            {
                continue;
            }

            var key = $"{item.SourceFile}:{item.SourceLine}";
            if (!evidenceKeys.Contains(key))
            {
                continue;
            }

            entries.Add(ToLogEntry(item, eventsPath));
        }

        return entries.Count > 0
            ? entries
            : summaries.Select(item => ToSyntheticEntry(item, item.Evidence.FirstOrDefault())).ToArray();
    }

    private static LogEntry ToLogEntry(LlmIndexEventItem item, string eventsPath)
    {
        var timestamp = DateTime.TryParse(item.Time, out var parsed) ? parsed : (DateTime?)null;
        var sourceFile = item.SourceFile ?? Path.GetFileName(eventsPath);
        var rawText = item.RawText ?? item.Message ?? item.Type;

        return new LogEntry(sourceFile, item.SourceLine, rawText, timestamp, item.Level);
    }

    private static LogEntry ToSyntheticEntry(LlmIndexSummaryItem item, string? evidence)
    {
        var (file, line) = ParseEvidence(evidence);
        return new LogEntry(file, line, item.Meaning ?? item.Title, null, "Error");
    }

    private static EventGroup ToEventGroup(LlmIndexSummaryItem item, LogEntry? entry)
    {
        var (start, end) = ParseTimeRange(item.TimeRange);
        return new EventGroup(
            $"{item.Type}:{item.Title}",
            ToLogEventType(item.Type),
            item.Title,
            item.Count,
            start,
            end,
            entry ?? ToSyntheticEntry(item, item.Evidence.FirstOrDefault()));
    }

    private static MatchedRule? ToMatchedRule(LlmIndexSummaryItem item)
    {
        if (item.ProbableCauses.Length == 0 && item.Actions.Length == 0 && string.IsNullOrWhiteSpace(item.Meaning))
        {
            return null;
        }

        return new MatchedRule(item.Type, item.Title, item.Title, item.Meaning ?? string.Empty, item.ProbableCauses, item.Actions);
    }

    private static string BuildSummary(UserQuestion question, string date, LlmIndexSummaryItem primary)
    {
        var dateText = string.IsNullOrWhiteSpace(date) ? "날짜 미상" : date;
        var timeText = string.IsNullOrWhiteSpace(primary.TimeRange) ? string.Empty : $" {primary.TimeRange}";

        if (primary.Type.Equals("InspectionNgValue", StringComparison.OrdinalIgnoreCase))
        {
            return $"{dateText}{timeText} 인덱스 기준으로 {primary.Title}가 발견되었습니다. {primary.Meaning}";
        }

        return $"{dateText}{timeText} 인덱스 기준 주요 원인은 {primary.Title} {primary.Count}건입니다. {primary.Meaning}";
    }

    private static (string File, int Line) ParseEvidence(string? evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
        {
            return ("LLMIndex", 0);
        }

        var parts = evidence.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[1], out var line)
            ? (parts[0], line)
            : (evidence, 0);
    }

    private static (DateTime? Start, DateTime? End) ParseTimeRange(string timeRange)
    {
        var parts = timeRange.Split('~', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !TimeOnly.TryParse(parts[0], out var start) ||
            !TimeOnly.TryParse(parts[1], out var end))
        {
            return (null, null);
        }

        return (DateTime.Today.Add(start.ToTimeSpan()), DateTime.Today.Add(end.ToTimeSpan()));
    }

    private static LogEventType ToLogEventType(string type)
    {
        return type switch
        {
            "IoError" => LogEventType.IoError,
            "InspectionFail" or "InspectionNgValue" => LogEventType.InspectionComplete,
            "Timeout" => LogEventType.Timeout,
            "Alarm" => LogEventType.Alarm,
            _ => LogEventType.Error
        };
    }
}
