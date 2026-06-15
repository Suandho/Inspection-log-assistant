using System.Globalization;
using System.Text.Json;
using LocalLLM.Core;

namespace LocalLLM.Llm;

public sealed class LlmQuestionUnderstandingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ILlmClient _llmClient;
    private readonly QuestionUnderstandingPromptBuilder _promptBuilder;

    public LlmQuestionUnderstandingService(
        ILlmClient llmClient,
        QuestionUnderstandingPromptBuilder? promptBuilder = null)
    {
        _llmClient = llmClient;
        _promptBuilder = promptBuilder ?? new QuestionUnderstandingPromptBuilder();
    }

    public async Task<StructuredQuestion?> UnderstandAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var prompt = _promptBuilder.Build(question);
        var response = await _llmClient.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false);
        var json = ExtractJsonObject(response);
        if (json is null)
        {
            return BuildFallback(question);
        }

        try
        {
            var dto = JsonSerializer.Deserialize<StructuredQuestionDto>(json, JsonOptions);
            if (dto is null)
            {
                return null;
            }

            var structured = new StructuredQuestion
            {
                OriginalText = question,
                Intent = string.IsNullOrWhiteSpace(dto.Intent) ? "General" : dto.Intent,
                Date = ParseDate(dto.Date),
                StartTime = ParseTime(dto.StartTime),
                EndTime = ParseTime(dto.EndTime),
                TargetInspection = string.IsNullOrWhiteSpace(dto.TargetInspection) ? null : dto.TargetInspection,
                Keywords = dto.Keywords?.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).ToArray() ?? []
            };

            return EnrichWithFallback(question, structured);
        }
        catch (JsonException)
        {
            return BuildFallback(question);
        }
    }

    private static StructuredQuestion EnrichWithFallback(string question, StructuredQuestion structured)
    {
        var fallback = BuildFallback(question);
        if (fallback is null)
        {
            return structured;
        }

        if (fallback.Intent.Equals("StartupFailure", StringComparison.OrdinalIgnoreCase))
        {
            fallback.Date = structured.Date;
            fallback.StartTime = structured.StartTime;
            fallback.EndTime = structured.EndTime;
            return fallback;
        }

        if (structured.Intent.Equals("General", StringComparison.OrdinalIgnoreCase) ||
            structured.Keywords.Length == 0)
        {
            return fallback;
        }

        return structured;
    }

    private static StructuredQuestion? BuildFallback(string question)
    {
        if (ContainsAny(question, "실행", "시작", "구동", "로딩", "안되는", "안 되는", "안됨", "안 돼"))
        {
            return new StructuredQuestion
            {
                OriginalText = question,
                Intent = "StartupFailure",
                Keywords = ["Load Error", "Load"]
            };
        }

        if (ContainsAny(question, "멈췄", "멈춤", "정지", "스톱", "stop"))
        {
            return new StructuredQuestion
            {
                OriginalText = question,
                Intent = "MachineStop",
                Keywords = ["Error", "Fail", "Timeout", "Alarm", "Stop", "Wait", "FnIO"]
            };
        }

        return null;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{', StringComparison.Ordinal);
        var end = text.LastIndexOf('}');

        return start >= 0 && end > start
            ? text[start..(end + 1)]
            : null;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static TimeOnly? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private sealed class StructuredQuestionDto
    {
        public string? Intent { get; set; }

        public string? Date { get; set; }

        public string? StartTime { get; set; }

        public string? EndTime { get; set; }

        public string? TargetInspection { get; set; }

        public string[]? Keywords { get; set; }
    }
}
