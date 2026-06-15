using System.Text;
using LocalLLM.Core;

namespace LocalLLM.Llm;

public sealed class CompactAnswerService
{
    public string GenerateAnswer(AssistantResponse response)
    {
        var builder = new StringBuilder();

        builder.AppendLine(response.Diagnosis.Summary);

        if (response.EventGroups.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("근거:");
            foreach (var group in response.EventGroups.Take(3))
            {
                var first = group.FirstTimestamp?.ToString("HH:mm:ss") ?? "시간 미확인";
                var last = group.LastTimestamp?.ToString("HH:mm:ss") ?? "시간 미확인";
                builder.AppendLine($"- {group.Description}: {group.Count}건 반복 ({first}~{last})");
            }
        }

        if (response.Diagnosis.MatchedRule is not null)
        {
            builder.AppendLine();
            builder.AppendLine("적용된 고객사 규칙:");
            builder.AppendLine($"- {response.Diagnosis.MatchedRule.DisplayName}");
            if (!string.IsNullOrWhiteSpace(response.Diagnosis.MatchedRule.Meaning))
            {
                builder.AppendLine($"- {response.Diagnosis.MatchedRule.Meaning}");
            }
        }

        if (!string.IsNullOrWhiteSpace(response.Diagnosis.SuspectedCause))
        {
            builder.AppendLine();
            builder.AppendLine("가능 원인:");
            foreach (var cause in SplitItems(response.Diagnosis.SuspectedCause).Take(3))
            {
                builder.AppendLine($"- {cause}");
            }
        }

        if (response.RecommendedActions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("확인 조치:");
            foreach (var action in response.RecommendedActions.Take(3))
            {
                builder.AppendLine($"- {action}");
            }
        }

        if (response.EvidenceLogs.Count > 0)
        {
            var firstEvidence = response.EvidenceLogs[0];
            builder.AppendLine();
            builder.AppendLine("첫 근거 로그:");
            builder.AppendLine($"- {Path.GetFileName(firstEvidence.FilePath)}:{firstEvidence.LineNumber} {firstEvidence.RawText}");
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> SplitItems(string value)
    {
        return value.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
