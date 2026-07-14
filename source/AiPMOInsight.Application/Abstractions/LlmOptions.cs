namespace AiPMOInsight.Application.Abstractions;

/// <summary>
/// LLM runtime settings bound from the <c>Llm</c> configuration section — the model-swap seam.
/// <see cref="Default"/> selects the fallback provider/model for any agent without an override;
/// each entry in <see cref="Agents"/> (keyed case-insensitively by the calling agent's
/// <c>SkillName</c>: <c>RiskAndIssue</c>, <c>Narrative</c>, <c>Challenge</c>, <c>Review</c>) overrides
/// individual fields for that agent only. Field-level merge: an empty string on an agent block
/// means "inherit from <see cref="Default"/>".
/// <para>
/// The legacy flat keys (<see cref="Provider"/>, <see cref="ModelId"/>, <see cref="ApiKey"/>) are
/// kept for one release for back-compat with the pre-routing shape; they are folded into
/// <see cref="Default"/> by the DI composition root when <see cref="Default"/>'s <c>Provider</c>
/// is empty. Newer code SHOULD NOT read these fields directly.
/// </para>
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Fallback provider settings used by any agent without a specific override.</summary>
    public LlmProviderOptions Default { get; init; } = new();

    /// <summary>Per-agent overrides, keyed by the agent's <c>SkillName</c> (case-insensitive).</summary>
    public IReadOnlyDictionary<string, LlmProviderOptions> Agents { get; init; } =
        new Dictionary<string, LlmProviderOptions>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Legacy — pre-routing flat key. Folded into <see cref="Default"/> by DI.</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>Legacy — pre-routing flat key. Folded into <see cref="Default"/> by DI.</summary>
    public string ModelId { get; init; } = string.Empty;

    /// <summary>Legacy — pre-routing flat key. Folded into <see cref="Default"/> by DI.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Legacy — pre-routing flat key. Folded into <see cref="Default"/> by DI.</summary>
    public int PerAnalysisTokenBudget { get; init; }

    /// <summary>
    /// Returns the effective <see cref="LlmProviderOptions"/> for the given agent, merging any
    /// per-agent override with <see cref="Default"/> field-by-field (an empty string on the
    /// override means "inherit"). Returns <see cref="Default"/> when no override is registered.
    /// </summary>
    public LlmProviderOptions ResolvedFor(string skillName)
    {
        ArgumentNullException.ThrowIfNull(skillName);

        if (!Agents.TryGetValue(skillName, out var agentOverride) || agentOverride is null)
        {
            return Default;
        }

        return new LlmProviderOptions
        {
            Provider = !string.IsNullOrEmpty(agentOverride.Provider) ? agentOverride.Provider : Default.Provider,
            ModelId = !string.IsNullOrEmpty(agentOverride.ModelId) ? agentOverride.ModelId : Default.ModelId,
            ApiKey = !string.IsNullOrEmpty(agentOverride.ApiKey) ? agentOverride.ApiKey : Default.ApiKey,
            PerAnalysisTokenBudget = agentOverride.PerAnalysisTokenBudget != 0
                ? agentOverride.PerAnalysisTokenBudget
                : Default.PerAnalysisTokenBudget,
            EnableExtendedThinking = agentOverride.EnableExtendedThinking ?? Default.EnableExtendedThinking,
        };
    }
}
