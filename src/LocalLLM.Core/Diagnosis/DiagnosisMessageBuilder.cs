using System.Text.RegularExpressions;

namespace LocalLLM.Core;

public sealed class DiagnosisMessageBuilder
{
    private static readonly Regex InspectionStartRegex = new(
        @"Process Start SN\s*:\s*(?<sn>.*?),\s*Area\s*:\s*(?<area>[^,]+),\s*Grab Idx\s*:\s*(?<grab>\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InspectionCompleteRegex = new(
        @"InspectionComplete\s*-\s*(?<area>[^,]+),\s*(?<result>True|False)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ValueRegex = new(
        @"(?<name>threshold|limit|base|standard|reference|value|measured|current|기준값|임계값|측정값|현재값)\s*[:=]\s*(?<value>-?\d+(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string BuildSummary(
        UserQuestion question,
        IReadOnlyList<string> searchedFiles,
        IReadOnlyList<LogEntry> relatedEntries,
        IReadOnlyList<LogEntry> failureEntries,
        IReadOnlyList<EventGroup> eventGroups,
        MatchedRule? matchedRule)
    {
        if (searchedFiles.Count == 0)
        {
            return question.Date is null
                ? "분석할 로그 파일을 찾지 못했습니다."
                : $"{question.Date:yyyy-MM-dd} 날짜에 해당하는 로그 파일을 찾지 못했습니다.";
        }

        if (question.TimeWindow is not null)
        {
            return BuildTimeWindowSummary(question, relatedEntries, failureEntries, eventGroups, matchedRule);
        }

        if (question.Date is null)
        {
            return "질문에서 날짜를 찾지 못했습니다. 예: 20261104, 2026-11-04, 5월14일";
        }

        if (!string.IsNullOrWhiteSpace(question.TargetInspection))
        {
            return BuildInspectionAreaSummary(question.TargetInspection, relatedEntries, eventGroups, matchedRule);
        }

        if (eventGroups.Count > 0)
        {
            var primary = eventGroups[0];
            var ruleText = matchedRule is null ? string.Empty : $" 고객사 규칙 기준으로 '{matchedRule.DisplayName}'에 해당합니다.";
            return $"가장 큰 실패 단서는 {DescribeGroup(primary)}입니다.{ruleText} 첫 발생 위치는 {FormatEntryLocation(primary.FirstEntry)}입니다.";
        }

        if (failureEntries.Count > 0)
        {
            var first = failureEntries[0];
            var values = ExtractValues(first.RawText);
            if (values.Count > 0)
            {
                return $"가장 먼저 발견된 실패 단서는 {FormatEntryLocation(first)}의 값 조건입니다. {string.Join(", ", values)}";
            }

            return $"가장 먼저 발견된 실패 단서는 {FormatEntryLocation(first)}의 '{first.RawText}' 로그입니다.";
        }

        if (relatedEntries.Count > 0)
        {
            return "관련 로그는 발견했지만 명확한 Error/Warning/Fail 로그는 찾지 못했습니다.";
        }

        return "해당 날짜의 로그 파일은 찾았지만 질문과 관련된 로그를 찾지 못했습니다.";
    }

    public string BuildSuspectedCause(
        IReadOnlyList<EventGroup> groups,
        IReadOnlyList<LogEntry> failureEntries,
        MatchedRule? matchedRule)
    {
        if (matchedRule is not null && matchedRule.ProbableCauses.Count > 0)
        {
            return string.Join(" / ", matchedRule.ProbableCauses);
        }

        if (groups.Count == 0)
        {
            return failureEntries.Count > 0 ? failureEntries[0].RawText : string.Empty;
        }

        var primary = groups[0];
        return primary.Type switch
        {
            LogEventType.IoError => "IO 모듈 또는 출력 비트 읽기 경로 이상 가능성이 가장 큽니다.",
            LogEventType.InspectionComplete => "해당 검사 알고리즘 또는 검사 조건이 False를 반환했습니다.",
            LogEventType.Timeout => "외부 장치 응답 지연 또는 신호 대기 시간 초과 가능성이 큽니다.",
            LogEventType.Alarm => "장비 알람 조건이 발생했습니다.",
            _ => primary.Description
        };
    }

    private static string BuildTimeWindowSummary(
        UserQuestion question,
        IReadOnlyList<LogEntry> relatedEntries,
        IReadOnlyList<LogEntry> failureEntries,
        IReadOnlyList<EventGroup> eventGroups,
        MatchedRule? matchedRule)
    {
        var windowText = question.TimeWindow?.DisplayText ?? "time window";
        var windowEntries = relatedEntries
            .Where(entry => entry.Timestamp is not null &&
                            question.TimeWindow is not null &&
                            question.TimeWindow.Contains(entry.Timestamp.Value))
            .ToArray();
        var dateText = BuildDateText(question, windowEntries);

        if (windowEntries.Length == 0)
        {
            return $"{dateText} {windowText} 범위에서 시간 정보가 있는 로그를 찾지 못했습니다.";
        }

        if (eventGroups.Count > 0)
        {
            var primary = eventGroups[0];
            var ruleText = matchedRule is null ? string.Empty : $" 고객사 규칙 기준으로 '{matchedRule.DisplayName}'이며, {matchedRule.Meaning}";
            return $"{dateText} {windowText} 전후 기계 멈춤 원인은 {DescribeGroup(primary)}로 추정됩니다.{ruleText} 첫 단서는 {FormatEntryLocation(primary.FirstEntry)}의 '{primary.FirstEntry.RawText}'입니다.";
        }

        if (failureEntries.Count > 0)
        {
            var first = failureEntries[0];
            return $"{dateText} {windowText} 전후 기계 멈춤 원인은 로그상 Error/False 계열 이벤트로 추정됩니다. 첫 단서는 {FormatEntryLocation(first)}의 '{first.RawText}'입니다.";
        }

        var repeatedWait = windowEntries.FirstOrDefault(entry => entry.RawText.Contains("Wait", StringComparison.OrdinalIgnoreCase));
        if (repeatedWait is not null)
        {
            return $"{dateText} {windowText} 전후 명확한 Error/False는 없지만 Wait 로그가 반복됩니다. 첫 단서는 {FormatEntryLocation(repeatedWait)}의 '{repeatedWait.RawText}'이며, 신호 대기 또는 외부 장치 응답 지연 가능성을 먼저 확인해야 합니다.";
        }

        return $"{dateText} {windowText} 범위의 로그는 확인했지만 기계 정지를 설명할 만한 큰 Error/False/Timeout 단서는 발견되지 않았습니다.";
    }

    private static string BuildInspectionAreaSummary(
        string targetInspection,
        IReadOnlyList<LogEntry> relatedEntries,
        IReadOnlyList<EventGroup> eventGroups,
        MatchedRule? matchedRule)
    {
        var targetEntries = relatedEntries
            .Where(entry => entry.RawText.Contains(targetInspection, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (targetEntries.Length == 0)
        {
            return $"{targetInspection} 검사 로그가 발견되지 않았습니다. 해당 검사 Area가 실행 대상에 포함되지 않았거나, 앞 단계에서 시퀀스가 중단되었을 가능성이 있습니다.";
        }

        if (eventGroups.Count > 0)
        {
            var primary = eventGroups[0];
            var ruleName = matchedRule?.DisplayName ?? targetInspection;
            var meaning = matchedRule is null ? string.Empty : $" 고객사 규칙 기준으로 {matchedRule.Meaning}";
            return $"{ruleName}는 {DescribeGroup(primary)}입니다.{meaning} 첫 실패 단서는 {FormatEntryLocation(primary.FirstEntry)}의 '{primary.FirstEntry.RawText}' 로그입니다.";
        }

        var startEntries = targetEntries.Where(entry => InspectionStartRegex.IsMatch(entry.RawText)).ToArray();
        var completeEntries = targetEntries.Where(entry => InspectionCompleteRegex.IsMatch(entry.RawText)).ToArray();

        if (startEntries.Length > completeEntries.Length)
        {
            return $"{targetInspection} 검사는 시작 로그가 {startEntries.Length}건 있지만 완료 로그가 {completeEntries.Length}건입니다. 일부 Grab에서 검사 완료 콜백이 누락되었을 가능성이 있습니다.";
        }

        if (startEntries.Length == 0 && completeEntries.Length > 0)
        {
            return $"{targetInspection} 검사 완료 로그는 있지만 시작 로그가 함께 발견되지 않았습니다. 로그 검색 범위 또는 기록 순서를 추가 확인해야 합니다.";
        }

        return $"{targetInspection} 검사는 관련 로그 기준으로 실행 및 완료되었습니다. 명확한 False/Error 단서는 발견되지 않았습니다.";
    }

    private static IReadOnlyList<string> ExtractValues(string text)
    {
        return ValueRegex.Matches(text)
            .Select(match => $"{match.Groups["name"].Value}={match.Groups["value"].Value}")
            .ToArray();
    }

    private static string FormatEntryLocation(LogEntry entry)
    {
        var fileName = Path.GetFileName(entry.FilePath);
        return $"{fileName}:{entry.LineNumber}";
    }

    private static string BuildDateText(UserQuestion question, IReadOnlyList<LogEntry> entries)
    {
        if (question.Date is not null)
        {
            return question.Date.Value.ToString("yyyy-MM-dd");
        }

        var dates = entries
            .Where(entry => entry.Timestamp is not null)
            .Select(entry => DateOnly.FromDateTime(entry.Timestamp!.Value))
            .Distinct()
            .OrderBy(date => date)
            .ToArray();

        return dates.Length switch
        {
            0 => "날짜 미지정",
            1 => $"{dates[0]:yyyy-MM-dd}(로그에서 추정)",
            _ => $"{dates[0]:yyyy-MM-dd}~{dates[^1]:yyyy-MM-dd}(로그에서 추정)"
        };
    }

    private static string DescribeGroup(EventGroup group)
    {
        var timeRange = group.FirstTimestamp is not null && group.LastTimestamp is not null
            ? $"({group.FirstTimestamp:HH:mm:ss}~{group.LastTimestamp:HH:mm:ss})"
            : string.Empty;

        return $"{group.Description} {group.Count}건 반복 {timeRange}".Trim();
    }
}
