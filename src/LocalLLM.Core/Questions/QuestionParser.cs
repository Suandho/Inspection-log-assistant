using System.Globalization;
using System.Text.RegularExpressions;

namespace LocalLLM.Core;

public sealed class QuestionParser
{
    private static readonly Regex CompactDateRegex = new(@"(?<!\d)(?<year>20\d{2})(?<month>\d{2})(?<day>\d{2})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex DashedDateRegex = new(@"(?<!\d)(?<year>20\d{2})[-./](?<month>\d{1,2})[-./](?<day>\d{1,2})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex KoreanMonthDayRegex = new(@"(?<month>\d{1,2})\s*월\s*(?<day>\d{1,2})\s*일", RegexOptions.Compiled);
    private static readonly Regex HourRegex = new(@"(?<period>새벽|오전|오후|밤)?\s*(?<hour>\d{1,2})\s*시\s*(?<approx>쯤|경|정도)?", RegexOptions.Compiled);

    private readonly AssistantOptions _options;

    public QuestionParser()
        : this(AssistantOptions.CreateDefault())
    {
    }

    public QuestionParser(AssistantOptions options)
    {
        _options = options;
    }

    public UserQuestion Parse(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question is required.", nameof(question));
        }

        var date = TryParseDate(question);
        var timeWindow = TryParseTimeWindow(question);
        var targetInspection = DetectInspectionArea(question);
        var eventName = DetectEventName(question, targetInspection, timeWindow);

        return new UserQuestion(question.Trim(), date, eventName, targetInspection, timeWindow);
    }

    private static DateOnly? TryParseDate(string question)
    {
        foreach (var match in new[] { CompactDateRegex.Match(question), DashedDateRegex.Match(question) })
        {
            if (!match.Success)
            {
                continue;
            }

            var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);

            if (DateOnly.TryParseExact($"{year:D4}-{month:D2}-{day:D2}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
        }

        var koreanMatch = KoreanMonthDayRegex.Match(question);
        if (koreanMatch.Success)
        {
            var month = int.Parse(koreanMatch.Groups["month"].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(koreanMatch.Groups["day"].Value, CultureInfo.InvariantCulture);
            var year = DateTime.Today.Year;

            if (DateOnly.TryParseExact($"{year:D4}-{month:D2}-{day:D2}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static TimeWindow? TryParseTimeWindow(string question)
    {
        var match = HourRegex.Match(question);
        if (!match.Success)
        {
            return null;
        }

        var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var period = match.Groups["period"].Value;

        if (period is "오후" or "밤")
        {
            if (hour < 12)
            {
                hour += 12;
            }
        }
        else if (period is "새벽" or "오전")
        {
            if (hour == 12)
            {
                hour = 0;
            }
        }

        if (hour is < 0 or > 23)
        {
            return null;
        }

        var center = new TimeOnly(hour, 0);
        var start = center.AddMinutes(-30);
        var end = center.AddMinutes(30);
        var display = $"{start:HH\\:mm}~{end:HH\\:mm}";

        return new TimeWindow(start, end, display);
    }

    private static string DetectEventName(string question, string? targetInspection, TimeWindow? timeWindow)
    {
        if (timeWindow is not null)
        {
            return "TimeWindow";
        }

        if (!string.IsNullOrWhiteSpace(targetInspection))
        {
            return "InspectionArea";
        }

        if (question.Contains("StartupFailure", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("Load Error", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("실행", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("구동", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("로딩", StringComparison.OrdinalIgnoreCase))
        {
            return "StartupFailure";
        }

        if (question.Contains("Start", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("시작", StringComparison.OrdinalIgnoreCase))
        {
            return "InspectionStart";
        }

        if (question.Contains("검사", StringComparison.OrdinalIgnoreCase))
        {
            return "Inspection";
        }

        return "General";
    }

    private string? DetectInspectionArea(string question)
    {
        foreach (var (inspection, aliases) in _options.InspectionAliases)
        {
            if (question.Contains(inspection, StringComparison.OrdinalIgnoreCase) ||
                aliases.Any(alias => question.Contains(alias, StringComparison.OrdinalIgnoreCase)))
            {
                return inspection;
            }
        }

        return null;
    }
}
