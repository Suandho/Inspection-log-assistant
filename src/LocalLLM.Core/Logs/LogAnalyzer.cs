using System.Globalization;
using System.Text.RegularExpressions;

namespace LocalLLM.Core;

public sealed class LogAnalyzer
{
    private static readonly Regex TimestampRegex = new(
        @"(?<timestamp>20\d{2}[-./]\d{1,2}[-./]\d{1,2}[ T]\d{1,2}:\d{2}:\d{2}(?:\.\d+)?)|(?<time>\b\d{1,2}:\d{2}:\d{2}(?:\.\d+)?\b)",
        RegexOptions.Compiled);
    private static readonly Regex ShortTimestampRegex = new(
        @"(?<year>\d{2})-(?<month>\d{2})-(?<day>\d{2})_(?<hour>\d{2}):(?<minute>\d{2}):(?<second>\d{2}):(?<millisecond>\d{3})",
        RegexOptions.Compiled);
    private static readonly Regex QuestionTokenRegex = new(@"[A-Za-z][A-Za-z0-9]{2,}", RegexOptions.Compiled);

    public IReadOnlyList<LogEntry> ExtractRelatedEntries(
        IReadOnlyList<string> files,
        UserQuestion question,
        AssistantOptions options)
    {
        var entries = new List<LogEntry>();
        var keywords = BuildKeywords(question, options);

        foreach (var file in files)
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var timestamp = TryParseTimestamp(line, question.Date);

                var isInTimeWindow = question.TimeWindow is not null &&
                                     timestamp is not null &&
                                     question.TimeWindow.Contains(timestamp.Value);
                var hasKeyword = keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                var shouldInclude = question.TimeWindow is not null
                    ? isInTimeWindow
                    : hasKeyword;

                if (!shouldInclude)
                {
                    continue;
                }

                entries.Add(new LogEntry(
                    file,
                    lineNumber,
                    line.Trim(),
                    timestamp,
                    DetectSeverity(line)));
            }
        }

        var limit = question.TimeWindow is null ? 200 : 20000;

        return entries
            .OrderBy(entry => entry.Timestamp ?? DateTime.MaxValue)
            .ThenBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.LineNumber)
            .Take(limit)
            .ToArray();
    }

    private static string[] BuildKeywords(UserQuestion question, AssistantOptions options)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (question.EventName == "StartupFailure")
        {
            keywords.Add("Load Error");
            keywords.Add("Load");
            return keywords.ToArray();
        }

        foreach (var keyword in options.EventKeywords.Concat(options.FailureKeywords))
        {
            keywords.Add(keyword);
        }

        if (question.EventName == "InspectionStart")
        {
            keywords.Add("Start");
            keywords.Add("Inspection Start");
            keywords.Add("검사 Start");
            keywords.Add("검사 시작");
        }

        if (!string.IsNullOrWhiteSpace(question.TargetInspection))
        {
            keywords.Add(question.TargetInspection);
            keywords.Add("InspectionComplete");
            keywords.Add("Process Start");
        }

        if (question.TimeWindow is not null)
        {
            keywords.Add("InspectionComplete");
            keywords.Add("False");
            keywords.Add("Stop");
            keywords.Add("Wait");
            keywords.Add("Timeout");
            keywords.Add("Alarm");
            keywords.Add("Error");
        }

        AddQuestionTokens(question, keywords);

        return keywords.ToArray();
    }

    private static void AddQuestionTokens(UserQuestion question, HashSet<string> keywords)
    {
        foreach (Match match in QuestionTokenRegex.Matches(question.OriginalText))
        {
            keywords.Add(match.Value);
        }
    }

    private static DateTime? TryParseTimestamp(string line, DateOnly? questionDate)
    {
        var shortMatch = ShortTimestampRegex.Match(line);
        if (shortMatch.Success)
        {
            var year = 2000 + int.Parse(shortMatch.Groups["year"].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(shortMatch.Groups["month"].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(shortMatch.Groups["day"].Value, CultureInfo.InvariantCulture);
            var hour = int.Parse(shortMatch.Groups["hour"].Value, CultureInfo.InvariantCulture);
            var minute = int.Parse(shortMatch.Groups["minute"].Value, CultureInfo.InvariantCulture);
            var second = int.Parse(shortMatch.Groups["second"].Value, CultureInfo.InvariantCulture);
            var millisecond = int.Parse(shortMatch.Groups["millisecond"].Value, CultureInfo.InvariantCulture);

            return new DateTime(year, month, day, hour, minute, second, millisecond);
        }

        var match = TimestampRegex.Match(line);
        if (!match.Success)
        {
            return null;
        }

        var timestamp = match.Groups["timestamp"].Value;
        if (!string.IsNullOrWhiteSpace(timestamp))
        {
            timestamp = timestamp.Replace('.', '-').Replace('/', '-');
            if (DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                return parsed;
            }
        }

        if (questionDate is not null && TimeOnly.TryParse(match.Groups["time"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return questionDate.Value.ToDateTime(time);
        }

        return null;
    }

    private static string DetectSeverity(string line)
    {
        if (ContainsInspectionFalse(line) ||
            ContainsAny(line, "Error", "Exception", "Fail", "Failed", "Alarm", "오류", "에러", "실패", "알람") ||
            ContainsNgToken(line))
        {
            return "Error";
        }

        if (ContainsAny(line, "Warning", "Warn", "Timeout", "경고", "타임아웃"))
        {
            return "Warning";
        }

        return "Info";
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsNgToken(string text) =>
        Regex.IsMatch(text, @"(^|[^A-Za-z0-9])NG([^A-Za-z0-9]|$)", RegexOptions.IgnoreCase);

    private static bool ContainsInspectionFalse(string text) =>
        text.Contains("InspectionComplete", StringComparison.OrdinalIgnoreCase) &&
        Regex.IsMatch(text, @",\s*False\b", RegexOptions.IgnoreCase);
}
