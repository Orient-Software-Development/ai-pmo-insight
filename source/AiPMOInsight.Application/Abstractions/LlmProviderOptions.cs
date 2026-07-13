namespace AiPMOInsight.Application.Abstractions;

/// <summary>
/// Provider-specific LLM settings — bound as the <c>Llm.Default</c> block and as each entry in
/// <c>Llm.Agents.&lt;SkillName&gt;</c>. Empty <see cref="Provider"/> / <see cref="ModelId"/> /
/// <see cref="ApiKey"/> on an agent block signal "inherit this field from
/// <see cref="LlmOptions.Default"/>"; <see cref="LlmOptions.ResolvedFor(string)"/> does the merge.
/// <para>
/// <see cref="ApiKey"/> is never committed to source-controlled config: supply it only via the
/// environment binding path (<c>Llm__Default__ApiKey</c> or <c>Llm__Agents__&lt;SkillName&gt;__ApiKey</c>).
/// </para>
/// </summary>
public sealed class LlmProviderOptions
{
    /// <summary>Selector matched by the client factory (e.g. <c>fake</c>, <c>anthropic</c>, <c>openai</c>).</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>Vendor model id passed to the provider (empty for the fake).</summary>
    public string ModelId { get; init; } = string.Empty;

    /// <summary>Provider API key — never committed; supplied per-environment via env vars only.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Upper bound on output tokens per analysis run; guards cost once real adapters land. The
    /// value <c>0</c> on an agent override means "inherit from <see cref="LlmOptions.Default"/>";
    /// the <c>Llm.Default</c> block ships with a real number in <c>appsettings.json</c>.
    /// </summary>
    public int PerAnalysisTokenBudget { get; init; }
}
