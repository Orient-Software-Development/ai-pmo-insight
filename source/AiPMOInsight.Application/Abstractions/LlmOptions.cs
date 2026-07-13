namespace AiPMOInsight.Application.Abstractions;

/// <summary>
/// LLM runtime settings, bound from the <c>Llm</c> configuration section. This is the <b>model-swap
/// seam</b>: <see cref="Provider"/> selects which <see cref="ILlmClient"/> adapter is used and
/// <see cref="ModelId"/> which model, so changing models is a config change — no code churn. The
/// section is inert this slice (only the fake is registered) but is wired now so the real adapter
/// lands next change without touching Application.
/// <para>
/// <see cref="ApiKey"/> is never committed: it is supplied per environment via the
/// <c>Llm__ApiKey</c> secret / env var only (mirrors <c>Jwt__SigningKey</c>).
/// </para>
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Selects the <see cref="ILlmClient"/> adapter (e.g. <c>fake</c>, <c>anthropic</c>).</summary>
    public string Provider { get; init; } = "fake";

    /// <summary>The model identifier passed to the provider (empty for the fake).</summary>
    public string ModelId { get; init; } = string.Empty;

    /// <summary>Upper bound on output tokens per analysis run; guards cost once a real adapter lands.</summary>
    public int PerAnalysisTokenBudget { get; init; } = 100_000;

    /// <summary>Provider API key — supplied via the <c>Llm__ApiKey</c> secret/env var only, never committed.</summary>
    public string ApiKey { get; init; } = string.Empty;
}
