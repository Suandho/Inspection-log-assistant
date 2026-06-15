using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using LocalLLM.Core;

Console.OutputEncoding = Encoding.UTF8;

var arguments = ParseArguments(args);
var inputRoot = arguments.GetValueOrDefault("input") ?? @"C:\Users\jkhong\Desktop\LLMTestLog";
var outputRoot = arguments.GetValueOrDefault("output") ?? Path.Combine(Environment.CurrentDirectory, "LLMIndex");
var includeSynthetic = !arguments.TryGetValue("synthetic", out var syntheticValue) ||
                       !syntheticValue.Equals("false", StringComparison.OrdinalIgnoreCase);

Directory.CreateDirectory(outputRoot);

var options = AssistantOptionsLoader.Load(arguments.GetValueOrDefault("config"));
var files = Directory.EnumerateFiles(inputRoot, "*.log", SearchOption.AllDirectories)
    .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (files.Length == 0)
{
    Console.WriteLine($"No log files found: {inputRoot}");
    return;
}

foreach (var file in files)
{
    var date = ExtractDateFromFileName(file) ?? DateOnly.FromDateTime(File.GetLastWriteTime(file));
    var events = BuildEvents(file, date, options).ToList();

    if (includeSynthetic)
    {
        events.AddRange(BuildSyntheticEvents(file, date));
    }

    WriteEvents(outputRoot, date, events);
    WriteSummary(outputRoot, date, events, options);
    WriteTimeline(outputRoot, date, events);

    Console.WriteLine($"Indexed {Path.GetFileName(file)} -> {events.Count} events");
}

static IReadOnlyList<LlmIndexEvent> BuildEvents(string file, DateOnly date, AssistantOptions options)
{
    var entries = new List<LogEntry>();
    var lineNumber = 0;
    var analyzer = new LogAnalyzer();

    foreach (var line in File.ReadLines(file))
    {
        lineNumber++;
        var question = new UserQuestion(string.Empty, date, "Indexing", null, null);
        var timestamp = ParseTimestamp(line, date);
        var severity = DetectSeverity(line);

        if (severity == "Info" &&
            !line.Contains("InspectionComplete", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("Process Start", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("Load Error", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        entries.Add(new LogEntry(file, lineNumber, line.Trim(), timestamp, severity));
    }

    var extracted = new LogEventExtractor(options).Extract(entries);
    var result = new List<LlmIndexEvent>();

    foreach (var logEvent in extracted)
    {
        result.Add(ConvertEvent(logEvent));
    }

    return result;

    static DateTime? ParseTimestamp(string line, DateOnly date)
    {
        var match = Regex.Match(
            line,
            @"(?<year>\d{2})-(?<month>\d{2})-(?<day>\d{2})_(?<hour>\d{2}):(?<minute>\d{2}):(?<second>\d{2}):(?<millisecond>\d{3})");
        if (!match.Success)
        {
            return null;
        }

        return new DateTime(
            2000 + int.Parse(match.Groups["year"].Value),
            int.Parse(match.Groups["month"].Value),
            int.Parse(match.Groups["day"].Value),
            int.Parse(match.Groups["hour"].Value),
            int.Parse(match.Groups["minute"].Value),
            int.Parse(match.Groups["second"].Value),
            int.Parse(match.Groups["millisecond"].Value));
    }

    static string DetectSeverity(string line)
    {
        if (line.Contains("[ERR]", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Fail", StringComparison.OrdinalIgnoreCase) ||
            ContainsInspectionFalse(line) ||
            ContainsNgToken(line))
        {
            return "Error";
        }

        if (line.Contains("[WRN]", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        return "Info";
    }

    static bool ContainsInspectionFalse(string line) =>
        line.Contains("InspectionComplete", StringComparison.OrdinalIgnoreCase) &&
        Regex.IsMatch(line, @",\s*False\b", RegexOptions.IgnoreCase);

    static bool ContainsNgToken(string line) =>
        Regex.IsMatch(line, @"(^|[^A-Za-z0-9])NG([^A-Za-z0-9]|$)", RegexOptions.IgnoreCase) &&
        !line.Contains(@"\NG", StringComparison.OrdinalIgnoreCase) &&
        !line.Contains(@"/NG", StringComparison.OrdinalIgnoreCase);
}

static LlmIndexEvent ConvertEvent(LogEvent logEvent)
{
    var eventType = logEvent.Type.ToString();
    if (logEvent.Type == LogEventType.InspectionComplete && logEvent.Result == false)
    {
        eventType = "InspectionFail";
    }
    else if (logEvent.Message.Contains("Load ", StringComparison.OrdinalIgnoreCase) &&
             logEvent.Message.Contains("Fail", StringComparison.OrdinalIgnoreCase))
    {
        eventType = "StartupFailure";
    }

    return new LlmIndexEvent
    {
        Time = logEvent.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff"),
        Level = logEvent.Source.Severity,
        Type = eventType,
        Station = logEvent.Station,
        Module = logEvent.Module,
        Inspection = logEvent.Inspection,
        Result = logEvent.Result?.ToString(),
        Message = NormalizeMessage(logEvent),
        SourceFile = Path.GetFileName(logEvent.Source.FilePath),
        SourceLine = logEvent.Source.LineNumber,
        RawText = logEvent.Source.RawText
    };
}

static string NormalizeMessage(LogEvent logEvent)
{
    if (logEvent.Type == LogEventType.InspectionComplete && logEvent.Result == false)
    {
        return $"{logEvent.Inspection} 검사 결과 False";
    }

    return logEvent.Message;
}

static IReadOnlyList<LlmIndexEvent> BuildSyntheticEvents(string sourceFile, DateOnly date)
{
    var result = new List<LlmIndexEvent>();
    var inspections = new[]
    {
        ("WheelOuterDiameter", 15.2, 10.0, 5.0),
        ("PadOutboard", 7.8, 6.0, 4.5),
        ("HubSurfaceDark", 22.4, 20.0, 12.0)
    };
    var baseTime = date.ToDateTime(new TimeOnly(10, 30));
    var line = 900000;

    foreach (var item in inspections)
    {
        result.Add(new LlmIndexEvent
        {
            Time = baseTime.AddMinutes(result.Count * 7).ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Level = "Error",
            Type = "InspectionNgValue",
            Station = "Station1",
            Module = "SyntheticInspection",
            Inspection = item.Item1,
            Result = "NG",
            Message = $"{item.Item1} NG - 측정값 : {item.Item2:0.0} / 임계값 : {item.Item3:0.0} / 기준값 : {item.Item4:0.0}",
            MeasuredValue = item.Item2,
            ThresholdValue = item.Item3,
            ReferenceValue = item.Item4,
            SourceFile = Path.GetFileName(sourceFile),
            SourceLine = line++,
            RawText = $"[SYNTHETIC] {item.Item1} NG - 측정값 : {item.Item2:0.0} / 임계값 : {item.Item3:0.0} / 기준값 : {item.Item4:0.0}"
        });
    }

    var syntheticErrors = new[]
    {
        ("IoError", "FnIO", "FnIO read output bit fail - synthetic burst"),
        ("IoError", "FnIO", "FnIO write output bit fail - synthetic burst"),
        ("StartupFailure", "LoadingThread", "Load Light Controller Fail"),
        ("CameraError", "Camera", "Cam2 Grab Timeout")
    };

    foreach (var error in syntheticErrors)
    {
        result.Add(new LlmIndexEvent
        {
            Time = baseTime.AddMinutes(30 + result.Count * 3).ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Level = "Error",
            Type = error.Item1,
            Module = error.Item2,
            Message = error.Item3,
            SourceFile = Path.GetFileName(sourceFile),
            SourceLine = line++,
            RawText = $"[SYNTHETIC] {error.Item3}"
        });
    }

    return result;
}

static void WriteEvents(string outputRoot, DateOnly date, IReadOnlyList<LlmIndexEvent> events)
{
    var path = Path.Combine(outputRoot, $"{date:yyyyMMdd}.events.ndjson");
    using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    foreach (var item in events.OrderBy(item => item.Time, StringComparer.Ordinal))
    {
        writer.WriteLine(JsonSerializer.Serialize(item, NdjsonOptions));
    }
}

static void WriteSummary(string outputRoot, DateOnly date, IReadOnlyList<LlmIndexEvent> events, AssistantOptions options)
{
    var summaries = events
        .Where(item => item.Level is "Error" or "Warning" || item.Result is "False" or "NG")
        .GroupBy(BuildGroupKey, StringComparer.OrdinalIgnoreCase)
        .Select(group =>
        {
            var ordered = group.OrderBy(item => item.Time, StringComparer.Ordinal).ToArray();
            var first = ordered[0];
            var rule = FindRule(first, options);

            return new LlmIndexSummaryItem
            {
                TimeRange = $"{ShortTime(first.Time)}~{ShortTime(ordered[^1].Time)}",
                Type = first.Type,
                Title = rule?.DisplayName ?? first.Message ?? first.Type,
                Count = ordered.Length,
                Meaning = rule?.Meaning ?? first.Message,
                ProbableCauses = rule?.ProbableCauses.ToArray() ?? [],
                Actions = rule?.RecommendedActions.ToArray() ?? [],
                Evidence = ordered.Take(5).Select(item => $"{item.SourceFile}:{item.SourceLine}").ToArray()
            };
        })
        .OrderByDescending(item => item.Count)
        .ThenBy(item => item.TimeRange, StringComparer.Ordinal)
        .ToArray();

    var summary = new LlmIndexSummary
    {
        Date = date.ToString("yyyy-MM-dd"),
        GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        Summaries = summaries
    };

    var path = Path.Combine(outputRoot, $"{date:yyyyMMdd}.summary.json");
    File.WriteAllText(path, JsonSerializer.Serialize(summary, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
}

static void WriteTimeline(string outputRoot, DateOnly date, IReadOnlyList<LlmIndexEvent> events)
{
    var timeline = events
        .Where(item => !string.IsNullOrWhiteSpace(item.Time))
        .GroupBy(item => item.Time![..13] + ":00")
        .Select(group => new LlmTimelineBucket
        {
            Hour = group.Key,
            ErrorCount = group.Count(item => item.Level == "Error"),
            WarningCount = group.Count(item => item.Level == "Warning"),
            TopEvents = group
                .GroupBy(item => item.Message ?? item.Type)
                .OrderByDescending(item => item.Count())
                .Take(5)
                .Select(item => new LlmTimelineEvent(item.Key, item.Count()))
                .ToArray()
        })
        .OrderBy(item => item.Hour, StringComparer.Ordinal)
        .ToArray();

    var path = Path.Combine(outputRoot, $"{date:yyyyMMdd}.timeline.json");
    File.WriteAllText(path, JsonSerializer.Serialize(timeline, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
}

static string BuildGroupKey(LlmIndexEvent item)
{
    return item.Type switch
    {
        "InspectionFail" or "InspectionNgValue" => $"{item.Type}:{item.Inspection}:{item.Result}",
        _ => $"{item.Type}:{item.Module}:{item.Message}"
    };
}

static MatchedRule? FindRule(LlmIndexEvent item, AssistantOptions options)
{
    if (!string.IsNullOrWhiteSpace(item.Inspection) &&
        options.InspectionRules.TryGetValue(item.Inspection, out var inspectionRule))
    {
        return new MatchedRule(
            "Inspection",
            item.Inspection,
            inspectionRule.DisplayName,
            inspectionRule.FailureMeaning,
            inspectionRule.ProbableCauses,
            inspectionRule.RecommendedActions);
    }

    foreach (var (pattern, rule) in options.ErrorRules)
    {
        if ((item.Message?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true) ||
            (item.RawText?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true))
        {
            return new MatchedRule("Error", pattern, rule.DisplayName, rule.Impact, rule.ProbableCauses, rule.RecommendedActions);
        }
    }

    return null;
}

static string ShortTime(string? time)
{
    return DateTime.TryParse(time, out var parsed)
        ? parsed.ToString("HH:mm:ss")
        : "시간미상";
}

static DateOnly? ExtractDateFromFileName(string file)
{
    var match = Regex.Match(Path.GetFileNameWithoutExtension(file), @"(?<year>20\d{2})(?<month>\d{2})(?<day>\d{2})");
    if (!match.Success)
    {
        return null;
    }

    return DateOnly.TryParse($"{match.Groups["year"].Value}-{match.Groups["month"].Value}-{match.Groups["day"].Value}", out var date)
        ? date
        : null;
}

static Dictionary<string, string> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < args.Length; index++)
    {
        var argument = args[index];
        if (!argument.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = argument[2..];
        if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            result[key] = args[++index];
        }
        else
        {
            result[key] = "true";
        }
    }

    return result;
}

internal sealed class LlmIndexEvent
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

internal sealed class LlmIndexSummary
{
    public string Date { get; set; } = string.Empty;
    public string GeneratedAt { get; set; } = string.Empty;
    public LlmIndexSummaryItem[] Summaries { get; set; } = [];
}

internal sealed class LlmIndexSummaryItem
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

internal sealed class LlmTimelineBucket
{
    public string Hour { get; set; } = string.Empty;
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public LlmTimelineEvent[] TopEvents { get; set; } = [];
}

internal sealed record LlmTimelineEvent(string Message, int Count);

internal static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions NdjsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
