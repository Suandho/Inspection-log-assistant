namespace LocalLLM.Llm;

public sealed record LocalLlmHealthResult(
    bool IsHealthy,
    string Endpoint,
    int? StatusCode,
    string ResponseText);
