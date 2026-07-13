using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Stub OpenAI adapter — see <see cref="NotWiredLlmClient"/> for the shared boots-but-throws
/// behaviour. Recognised by the factory as <c>Provider = "openai"</c>; real vendor wiring lands in
/// issue #27.
/// </summary>
public sealed class OpenAiLlmClient(LlmProviderOptions options) : NotWiredLlmClient(options)
{
    protected override string AdapterName => "OpenAI";
}
