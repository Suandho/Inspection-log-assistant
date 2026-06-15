using LocalLLM.Core;

namespace LocalLLM.Llm;

public sealed class LlmAnswerService
{
    private readonly ILlmClient _llmClient;
    private readonly LlmPromptBuilder _promptBuilder;

    public LlmAnswerService(ILlmClient llmClient, LlmPromptBuilder? promptBuilder = null)
    {
        _llmClient = llmClient;
        _promptBuilder = promptBuilder ?? new LlmPromptBuilder();
    }

    public async Task<string> GenerateAnswerAsync(
        AssistantResponse response,
        CancellationToken cancellationToken = default)
    {
        var prompt = _promptBuilder.Build(response);
        return await _llmClient.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false);
    }
}
