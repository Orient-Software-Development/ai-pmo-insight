using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Stub Anthropic adapter — see <see cref="NotWiredLlmClient"/> for the shared boots-but-throws
/// behaviour. Recognised by the factory as <c>Provider = "anthropic"</c>; real HTTP / structured-output
/// wiring, retries, and budget enforcement land in issue #27.
/// </summary>
public sealed class AnthropicLlmClient(LlmProviderOptions options) : NotWiredLlmClient(options)
{
    protected override string AdapterName => "Anthropic";
}
