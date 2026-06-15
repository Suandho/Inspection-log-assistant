using System.Text.Json;

namespace LocalLLM.Llm;

public sealed class LocalLlmOptions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public string Endpoint { get; set; } = "http://127.0.0.1:8080/v1/chat/completions";

    public string Model { get; set; } = "local-model";

    public double Temperature { get; set; } = 0.1;

    public int MaxTokens { get; set; } = 512;

    public int TimeoutSeconds { get; set; } = 120;

    public static LocalLlmOptions Load(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return new LocalLlmOptions();
        }

        using var document = JsonDocument.Parse(File.ReadAllText(configPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (!document.RootElement.TryGetProperty("llm", out var llmElement))
        {
            return new LocalLlmOptions();
        }

        return JsonSerializer.Deserialize<LocalLlmOptions>(llmElement.GetRawText(), JsonOptions)
            ?? new LocalLlmOptions();
    }

    public LocalLlmOptions WithOverrides(IReadOnlyDictionary<string, string> arguments)
    {
        var result = new LocalLlmOptions
        {
            Endpoint = Endpoint,
            Model = Model,
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            TimeoutSeconds = TimeoutSeconds
        };

        if (arguments.TryGetValue("llm-endpoint", out var endpoint) && !string.IsNullOrWhiteSpace(endpoint))
        {
            result.Endpoint = endpoint;
        }

        if (arguments.TryGetValue("llm-model", out var model) && !string.IsNullOrWhiteSpace(model))
        {
            result.Model = model;
        }

        if (arguments.TryGetValue("llm-temperature", out var temperature)
            && double.TryParse(temperature, out var parsedTemperature))
        {
            result.Temperature = parsedTemperature;
        }

        if (arguments.TryGetValue("llm-max-tokens", out var maxTokens)
            && int.TryParse(maxTokens, out var parsedMaxTokens))
        {
            result.MaxTokens = parsedMaxTokens;
        }

        if (arguments.TryGetValue("llm-timeout-seconds", out var timeoutSeconds)
            && int.TryParse(timeoutSeconds, out var parsedTimeoutSeconds))
        {
            result.TimeoutSeconds = parsedTimeoutSeconds;
        }

        return result;
    }
}
