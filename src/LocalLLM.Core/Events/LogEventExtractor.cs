using System.Text.RegularExpressions;

namespace LocalLLM.Core;

public sealed class LogEventExtractor
{
    private readonly AssistantOptions _options;

    public LogEventExtractor()
        : this(AssistantOptions.CreateDefault())
    {
    }

    public LogEventExtractor(AssistantOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<LogEvent> Extract(IReadOnlyList<LogEntry> entries)
    {
        return entries
            .Select(Parse)
            .Where(logEvent => logEvent.Type != LogEventType.Unknown)
            .ToArray();
    }

    public IReadOnlyList<EventGroup> Group(IReadOnlyList<LogEvent> events)
    {
        return events
            .Where(IsProblemEvent)
            .GroupBy(logEvent => logEvent.GroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderBy(logEvent => logEvent.Timestamp ?? DateTime.MaxValue).ToArray();
                var first = ordered[0];
                var last = ordered[^1];
                return new EventGroup(
                    group.Key,
                    first.Type,
                    Describe(first),
                    ordered.Length,
                    first.Timestamp,
                    last.Timestamp,
                    first.Source);
            })
            .OrderByDescending(group => SeverityRank(group.Type))
            .ThenByDescending(group => group.Count)
            .ThenBy(group => group.FirstTimestamp ?? DateTime.MaxValue)
            .ToArray();
    }

    private LogEvent Parse(LogEntry entry)
    {
        foreach (var pattern in _options.EventPatterns)
        {
            var parsed = TryParsePattern(entry, pattern);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        if (entry.Severity == "Error")
        {
            return new LogEvent(LogEventType.Error, entry.RawText, null, null, null, null, null, null, entry);
        }

        return new LogEvent(LogEventType.Unknown, entry.RawText, null, null, null, null, null, null, entry);
    }

    private static LogEvent? TryParsePattern(LogEntry entry, EventPatternRule pattern)
    {
        var line = entry.RawText;

        if (pattern.Contains.Length > 0 &&
            !pattern.Contains.All(value => line.Contains(value, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        Match? match = null;
        if (!string.IsNullOrWhiteSpace(pattern.Regex))
        {
            match = Regex.Match(line, pattern.Regex, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }
        }

        if (pattern.Contains.Length == 0 && match is null)
        {
            return null;
        }

        var type = ParseEventType(pattern.Type);
        var station = GroupValue(match, "station");
        var inspection = FirstGroupValue(match, "inspection", "area");
        var serialNumber = FirstGroupValue(match, "serialNumber", "sn");
        var grabIndex = TryParseInt(FirstGroupValue(match, "grabIndex", "grab"));
        var result = TryParseBool(GroupValue(match, "result"));
        var message = BuildMessage(line, pattern, match, type, inspection, result);

        return new LogEvent(
            type,
            message,
            station,
            pattern.Module,
            inspection,
            serialNumber,
            grabIndex,
            result,
            entry);
    }

    private static string BuildMessage(
        string line,
        EventPatternRule pattern,
        Match? match,
        LogEventType type,
        string? inspection,
        bool? result)
    {
        if (!string.IsNullOrWhiteSpace(pattern.Message))
        {
            return pattern.Message;
        }

        var messageGroup = GroupValue(match, "message");
        if (!string.IsNullOrWhiteSpace(messageGroup))
        {
            return messageGroup;
        }

        if (!string.IsNullOrWhiteSpace(pattern.MessageAfter))
        {
            return ExtractMessageAfterMarker(line, pattern.MessageAfter);
        }

        return type switch
        {
            LogEventType.InspectionComplete when inspection is not null => $"{inspection} inspection completed with result {result}",
            LogEventType.ProcessStart when inspection is not null => $"Process started: {inspection}",
            LogEventType.StationStart when GroupValue(match, "station") is { Length: > 0 } station => $"{station} started",
            LogEventType.ResetStart when GroupValue(match, "station") is { Length: > 0 } station => $"{station} reset started",
            LogEventType.ResetComplete when GroupValue(match, "station") is { Length: > 0 } station => $"{station} reset completed",
            LogEventType.Timeout => "Timeout 발생",
            LogEventType.Alarm => "Alarm 발생",
            _ => line
        };
    }

    private static string Describe(LogEvent logEvent)
    {
        return logEvent.Type switch
        {
            LogEventType.InspectionComplete when logEvent.Result == false => $"{logEvent.Inspection} 검사 결과 False",
            LogEventType.IoError => FormatModuleMessage(logEvent.Module, logEvent.Message),
            LogEventType.Timeout => "Timeout 발생",
            LogEventType.Alarm => "Alarm 발생",
            LogEventType.Error => logEvent.Message,
            _ => logEvent.Message
        };
    }

    private static bool IsProblemEvent(LogEvent logEvent)
    {
        return logEvent.Type is LogEventType.IoError or LogEventType.Timeout or LogEventType.Alarm or LogEventType.Error ||
               (logEvent.Type == LogEventType.InspectionComplete && logEvent.Result == false);
    }

    private static int SeverityRank(LogEventType type)
    {
        return type switch
        {
            LogEventType.IoError => 100,
            LogEventType.Error => 90,
            LogEventType.Timeout => 80,
            LogEventType.Alarm => 70,
            LogEventType.InspectionComplete => 60,
            _ => 0
        };
    }

    private static LogEventType ParseEventType(string type)
    {
        return Enum.TryParse<LogEventType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : LogEventType.Unknown;
    }

    private static string? FirstGroupValue(Match? match, params string[] groupNames)
    {
        foreach (var groupName in groupNames)
        {
            var value = GroupValue(match, groupName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? GroupValue(Match? match, string groupName)
    {
        if (match is null || !match.Groups.ContainsKey(groupName))
        {
            return null;
        }

        var value = match.Groups[groupName].Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool? TryParseBool(string? value)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        if (value?.Equals("OK", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (value?.Equals("NG", StringComparison.OrdinalIgnoreCase) == true)
        {
            return false;
        }

        return null;
    }

    private static string ExtractMessageAfterMarker(string line, string marker)
    {
        var index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? line : line[(index + marker.Length)..].Trim();
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
