using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Stub OpenAI adapter — same shape as <see cref="AnthropicLlmClient"/>. It carries the resolved
/// <see cref="LlmProviderOptions"/> so a production-shape config (<c>Provider = "openai"</c>) boots
/// and passes DI resolution, but throws <see cref="NotImplementedException"/> the first time an
/// agent actually calls the model (design §4). Real vendor wiring is a follow-up change.
/// <para>
/// The thrown message names the provider and the calling skill but NEVER the
/// <see cref="LlmProviderOptions.ApiKey"/> (R3 secret-leak guard).
/// </para>
/// </summary>
public sealed class OpenAiLlmClient(LlmProviderOptions options) : ILlmClient
{
    private readonly LlmProviderOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
        where TOutput : notnull
    {
        ArgumentNullException.ThrowIfNull(request);

        throw new NotImplementedException(
            $"Provider '{_options.Provider}' is not yet wired for skill '{request.SkillName}'. " +
            "The OpenAI adapter is a deliberate follow-up change; point this agent at the " +
            "'fake' provider to run without a live vendor call.");
    }
}
