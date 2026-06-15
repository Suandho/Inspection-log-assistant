namespace LocalLLM.Llm;

public sealed class StaticLlmClient : ILlmClient
{
    private readonly string _response;

    public StaticLlmClient(string response)
    {
        _response = response;
    }

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_response);
    }
}
