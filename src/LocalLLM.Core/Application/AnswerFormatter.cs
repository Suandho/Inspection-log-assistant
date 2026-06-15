using System.Text;

namespace LocalLLM.Core;

public sealed class AnswerFormatter
{
    public string Format(DiagnosisResult result)
    {
        var builder = new StringBuilder();

        builder.AppendLine(result.Summary);

        if (result.MatchedRule is not null)
        {
            builder.AppendLine();
            builder.AppendLine($"고객사 규칙: {result.MatchedRule.DisplayName}");
            if (!string.IsNullOrWhiteSpace(result.MatchedRule.Meaning))
            {
                builder.AppendLine($"의미: {result.MatchedRule.Meaning}");
            }
        }

        if (!string.IsNullOrWhiteSpace(result.SuspectedCause))
        {
            builder.AppendLine();
            builder.AppendLine("가능 원인:");
            foreach (var cause in SplitCauses(result.SuspectedCause))
            {
                builder.AppendLine($"- {cause}");
            }
        }

        if (result.EventGroups.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("반복/주요 이벤트:");
            foreach (var group in result.EventGroups.Take(5))
            {
                var first = group.FirstTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "시간 미확인";
                var last = group.LastTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "시간 미확인";
                builder.AppendLine($"- {group.Description}: {group.Count}건 ({first} ~ {last})");
            }
        }

        if (result.RecommendedActions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("권장 확인:");
            foreach (var action in result.RecommendedActions)
            {
                builder.AppendLine($"- {action}");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"로그 경로: {result.LogRootPath}");

        if (result.Question.Date is not null)
        {
            builder.AppendLine($"분석 날짜: {result.Question.Date:yyyy-MM-dd}");
        }

        builder.AppendLine($"검색 파일 수: {result.SearchedFiles.Count}");
        builder.AppendLine($"관련 로그 수: {result.RelatedEntries.Count}");
        builder.AppendLine($"구조화 이벤트 수: {result.Events.Count}");
        builder.AppendLine($"실패 단서 수: {result.FailureEntries.Count}");

        var evidence = result.FailureEntries.Count > 0
            ? result.FailureEntries
            : result.RelatedEntries.Take(10).ToArray();

        if (evidence.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("관련 로그:");
            foreach (var entry in evidence.Take(10))
            {
                var timestamp = entry.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "시간 미확인";
                builder.AppendLine($"- {timestamp} [{entry.Severity}] {Path.GetFileName(entry.FilePath)}:{entry.LineNumber} {entry.RawText}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> SplitCauses(string suspectedCause)
    {
        return suspectedCause
            .Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
