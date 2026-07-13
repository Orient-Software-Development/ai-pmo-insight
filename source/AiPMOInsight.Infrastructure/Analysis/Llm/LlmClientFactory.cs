using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// The single-file provider switch. Recognised selectors: <c>fake</c>, <c>anthropic</c>,
/// <c>openai</c>. Recognition is case-insensitive. <c>fake</c> returns a fully working
/// <see cref="FakeLlmClient"/>; <c>anthropic</c> and <c>openai</c> return working vendor adapters
/// (<see cref="AnthropicLlmClient"/> / <see cref="OpenAiLlmClient"/>) that call the provider's API
/// with structured JSON output. All three construct successfully so a prod-shape config boots; a
/// missing key surfaces as a request-time provider failure, never at startup. Any other value —
/// including empty — fails startup with a message naming both the requested provider and the agent's
/// <c>SkillName</c>, so misconfiguration cannot slip past DI registration and surface only at
/// request time.
/// </summary>
public sealed class LlmClientFactory : ILlmClientFactory
{
    public ILlmClient Create(string skillNameForDiagnostics, LlmProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(skillNameForDiagnostics);
        ArgumentNullException.ThrowIfNull(options);

        return options.Provider?.ToLowerInvariant() switch
        {
            "fake" => new FakeLlmClient(FakeLlmFixtures.Default()),
            "anthropic" => new AnthropicLlmClient(options),
            "openai" => new OpenAiLlmClient(options),
            _ => throw new InvalidOperationException(
                $"Unknown LLM provider '{options.Provider}' configured for agent '{skillNameForDiagnostics}'. " +
                "Recognised providers this build: 'fake', 'anthropic', 'openai'. Fix the " +
                "'Llm.Default.Provider' or 'Llm.Agents.<SkillName>.Provider' setting."),
        };
    }
}
