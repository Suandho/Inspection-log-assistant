using System.Text;
using LocalLLM.Core;

namespace LocalLLM.Llm;

public sealed class LlmPromptBuilder
{
    public string Build(AssistantResponse response)
    {
        var builder = new StringBuilder();

        builder.AppendLine("당신은 장비 로그 분석 결과를 고객에게 설명하는 한국어 지원 담당자입니다.");
        builder.AppendLine("아래 원인, 근거, 조치만 사용해서 짧게 답변하세요.");
        builder.AppendLine("항목 제목, 번호 목록, 지시문을 쓰지 말고 최종 답변 한 단락만 작성하세요.");
        builder.AppendLine("같은 문장을 반복하지 마세요.");
        builder.AppendLine();

        builder.AppendLine("원인:");
        builder.AppendLine(response.Diagnosis.Summary);
        builder.AppendLine();

        if (response.EventGroups.Count > 0)
        {
            builder.AppendLine("근거:");
            foreach (var group in response.EventGroups.Take(3))
            {
                var first = group.FirstTimestamp?.ToString("HH:mm:ss") ?? "시간 미확인";
                var last = group.LastTimestamp?.ToString("HH:mm:ss") ?? "시간 미확인";
                builder.AppendLine($"- {group.Description}: {group.Count}건 반복 ({first}~{last})");
            }

            builder.AppendLine();
        }

        if (response.Diagnosis.MatchedRule is not null)
        {
            builder.AppendLine("고객사 규칙:");
            builder.AppendLine($"- {response.Diagnosis.MatchedRule.DisplayName}");
            if (!string.IsNullOrWhiteSpace(response.Diagnosis.MatchedRule.Meaning))
            {
                builder.AppendLine($"- {response.Diagnosis.MatchedRule.Meaning}");
            }

            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(response.Diagnosis.SuspectedCause))
        {
            builder.AppendLine("가능 원인:");
            foreach (var cause in SplitItems(response.Diagnosis.SuspectedCause).Take(3))
            {
                builder.AppendLine($"- {cause}");
            }

            builder.AppendLine();
        }

        if (response.RecommendedActions.Count > 0)
        {
            builder.AppendLine("확인 조치:");
            foreach (var action in response.RecommendedActions.Take(3))
            {
                builder.AppendLine($"- {action}");
            }

            builder.AppendLine();
        }

        if (response.EvidenceLogs.Count > 0)
        {
            var firstEvidence = response.EvidenceLogs[0];
            builder.AppendLine("첫 근거 로그:");
            builder.AppendLine($"- {Path.GetFileName(firstEvidence.FilePath)}:{firstEvidence.LineNumber} {firstEvidence.RawText}");
            builder.AppendLine();
        }

        builder.AppendLine("최종 답변만 한 단락으로 작성하세요.");

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> SplitItems(string value)
    {
        return value.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
