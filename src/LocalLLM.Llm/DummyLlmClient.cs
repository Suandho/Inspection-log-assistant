namespace LocalLLM.Llm;

public sealed class DummyLlmClient : ILlmClient
{
    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(prompt);
    }
}
