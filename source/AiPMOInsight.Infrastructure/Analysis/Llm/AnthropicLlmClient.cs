using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Stub Anthropic adapter. It carries the resolved <see cref="LlmProviderOptions"/> so a
/// production-shape config (<c>Provider = "anthropic"</c>) boots and passes DI resolution, but
/// throws <see cref="NotImplementedException"/> the first time an agent actually calls the model
/// (design §4). Real HTTP / structured-output wiring, retries, and budget enforcement are a
/// deliberate follow-up change.
/// <para>
/// The thrown message names the provider and the calling skill but NEVER the
/// <see cref="LlmProviderOptions.ApiKey"/> (R3 secret-leak guard).
/// </para>
/// </summary>
public sealed class AnthropicLlmClient(LlmProviderOptions options) : ILlmClient
{
    private readonly LlmProviderOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
        where TOutput : notnull
    {
        ArgumentNullException.ThrowIfNull(request);

        throw new NotImplementedException(
            $"Provider '{_options.Provider}' is not yet wired for skill '{request.SkillName}'. " +
            "The Anthropic adapter is a deliberate follow-up change; point this agent at the " +
            "'fake' provider to run without a live vendor call.");
    }
}
