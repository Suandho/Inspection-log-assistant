namespace LocalLLM.Llm;

public interface ILlmClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
