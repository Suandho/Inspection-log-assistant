using System.Net.Http.Json;
using System.Text.Json;

namespace LocalLLM.Llm;

public sealed class LocalHttpLlmClient : ILlmClient, IDisposable
{
    private readonly LocalLlmOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;

    public LocalHttpLlmClient(LocalLlmOptions options)
        : this(options, new HttpClient(), disposeHttpClient: true)
    {
    }

    public LocalHttpLlmClient(
        LocalLlmOptions options,
        HttpClient httpClient,
        bool disposeHttpClient = false)
    {
        _options = options;
        _httpClient = httpClient;
        _disposeHttpClient = disposeHttpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            stream = false
        };

        using var response = await _httpClient.PostAsJsonAsync(
            _options.Endpoint,
            request,
            cancellationToken).ConfigureAwait(false);

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Local LLM request failed. Status={(int)response.StatusCode}, Body={content}");
        }

        return ExtractAnswer(content);
    }

    public async Task<LocalLlmHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var modelsEndpoint = ResolveModelsEndpoint(_options.Endpoint);

        try
        {
            using var response = await _httpClient.GetAsync(modelsEndpoint, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new LocalLlmHealthResult(
                response.IsSuccessStatusCode,
                modelsEndpoint.ToString(),
                (int)response.StatusCode,
                body);
        }
        catch (Exception exception)
        {
            return new LocalLlmHealthResult(false, modelsEndpoint.ToString(), null, exception.Message);
        }
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static Uri ResolveModelsEndpoint(string chatEndpoint)
    {
        var endpoint = new Uri(chatEndpoint);
        var path = endpoint.AbsolutePath;

        if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^"/chat/completions".Length] + "/models";
        }
        else if (!path.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
        {
            path = "/v1/models";
        }

        return new UriBuilder(endpoint)
        {
            Path = path,
            Query = string.Empty
        }.Uri;
    }

    private static string ExtractAnswer(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Local LLM response did not contain choices.");
        }

        var firstChoice = choices[0];
        if (firstChoice.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content))
        {
            return content.GetString()?.Trim() ?? string.Empty;
        }

        if (firstChoice.TryGetProperty("text", out var text))
        {
            return text.GetString()?.Trim() ?? string.Empty;
        }

        throw new InvalidOperationException("Local LLM response did not contain message.content or text.");
    }
}
