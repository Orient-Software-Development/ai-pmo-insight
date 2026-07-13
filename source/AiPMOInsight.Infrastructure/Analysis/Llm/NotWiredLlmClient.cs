using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Shared base for the recognised-but-not-yet-wired vendor adapters
/// (<see cref="AnthropicLlmClient"/>, <see cref="OpenAiLlmClient"/>). Each carries the resolved
/// <see cref="LlmProviderOptions"/> so a production-shape config (e.g. <c>Provider = "anthropic"</c>)
/// boots and passes DI resolution, but throws <see cref="NotImplementedException"/> the first time an
/// agent actually calls the model (design §4). Real vendor HTTP / structured-output wiring is a
/// deliberate follow-up change (issue #27).
/// <para>
/// The thrown message names the provider and the calling skill but NEVER the
/// <see cref="LlmProviderOptions.ApiKey"/> (R3 secret-leak guard).
/// </para>
/// </summary>
public abstract class NotWiredLlmClient(LlmProviderOptions options) : ILlmClient
{
    private readonly LlmProviderOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Human-readable adapter name used in the not-implemented message (e.g. <c>Anthropic</c>).</summary>
    protected abstract string AdapterName { get; }

    public Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
        where TOutput : notnull
    {
        ArgumentNullException.ThrowIfNull(request);

        throw new NotImplementedException(
            $"Provider '{_options.Provider}' is not yet wired for skill '{request.SkillName}'. " +
            $"The {AdapterName} adapter is a deliberate follow-up change (issue #27); point this agent " +
            "at the 'fake' provider to run without a live vendor call.");
    }
}
