namespace AiPMOInsight.Application.Abstractions;

/// <summary>
/// The stable <c>SkillName</c> of each LLM-backed agent — the single source of truth shared by the
/// agents (which stamp it onto <see cref="LlmRequest.SkillName"/>), the routing config validator, and
/// the startup diagnostics. The per-agent <c>Llm.Agents.&lt;SkillName&gt;</c> override keys are matched
/// against <see cref="All"/> case-insensitively, so adding or removing an LLM agent is a one-place edit
/// here rather than scattered string literals.
/// </summary>
public static class LlmAgentSkills
{
    /// <summary>Agent #4 — Risk &amp; Issue.</summary>
    public const string RiskAndIssue = "RiskAndIssue";

    /// <summary>Agent #7 — Narrative.</summary>
    public const string Narrative = "Narrative";

    /// <summary>Agent #8 — Challenge.</summary>
    public const string Challenge = "Challenge";

    /// <summary>Agent #9 — Review.</summary>
    public const string Review = "Review";

    /// <summary>All four LLM-backed agent skill names, compared case-insensitively.</summary>
    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { RiskAndIssue, Narrative, Challenge, Review };
}
