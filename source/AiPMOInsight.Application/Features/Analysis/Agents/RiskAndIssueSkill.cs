using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// A risk/issue the LLM extracted from meeting minutes (structured output contract).
/// <paramref name="SourceLocator"/> must be copied verbatim from the "[LOCATOR: ...]" header of the
/// minutes block the item was found in (see risk-and-issue.prompt.md) so it cites the right entry
/// when a project has more than one minutes block in a run.
/// </summary>
public sealed record ExtractedRisk(string Title, string Kind, string Severity, string Rationale, string SourceLocator);

/// <summary>The Risk & Issue agent's LLM output contract: the risks/issues found in the minutes.</summary>
public sealed record MinuteRiskExtraction(IReadOnlyList<ExtractedRisk> Risks);

/// <summary>
/// Agent #4 — Risk &amp; Issue (hybrid). Filters the structured RAID records deterministically (no
/// LLM) AND, <b>only when meeting minutes are present</b>, extracts additional risks from the
/// unstructured minutes text via <see cref="ILlmClient"/>. With no minutes it makes zero LLM calls.
/// RAID findings cite their record (no prompt version); minute-extracted findings cite the minutes
/// locator, carry the prompt's content-hash version, and have their confidence capped by the DQ
/// signal (an extraction can't be more trustworthy than its source data).
/// </summary>
public sealed class RiskAndIssueSkill(ILlmClient llm, PromptRegistry prompts)
    : IAgentSkill<AnalysisInput, IReadOnlyList<Finding>>
{
    private const string PromptKey = "risk-and-issue";

    public string Name => LlmAgentSkills.RiskAndIssue;

    public async Task<IReadOnlyList<Finding>> ExecuteAsync(AnalysisInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var slice = input.Slice;
        var dqConfidence = ConfidencePolicy.FromSignals(input.Quality);
        var findings = new List<Finding>();

        // Deterministic RAID path — always runs, no LLM.
        foreach (var raid in slice.Data.RaidItems.Where(r => r.ProjectKey == slice.ProjectKey))
        {
            var severity = raid.Severity is null ? string.Empty : $" [{raid.Severity}]";
            findings.Add(FindingFactory.Analysis(
                slice, Name, $"{raid.Type}{severity}: {raid.Description}", raid.Source, dqConfidence,
                HealthArea.Risk, RiskSeverity(raid.Severity)));
        }

        // LLM minutes path — only when minutes exist for this project.
        var minutes = slice.Data.Minutes.Where(m => m.ProjectKey == slice.ProjectKey).ToList();
        if (minutes.Count == 0)
        {
            return findings;
        }

        var prompt = prompts.Get(PromptKey);
        var request = new LlmRequest
        {
            SkillName = Name,
            SystemPrompt = prompt.Content,
            Prompt = $"MINUTES:\n{string.Join("\n\n", minutes.Select(m => $"[LOCATOR: {m.Source.Locator}]\n{m.Text}"))}",
            PromptVersion = prompt.Version,
        };

        var extraction = await llm.CompleteAsync<MinuteRiskExtraction>(request, cancellationToken);

        // Cite each risk against the minutes block it actually names; an unrecognized or blank
        // locator (a model that didn't copy the tag verbatim) falls back to the first block rather
        // than throwing.
        var minutesByLocator = minutes.GroupBy(m => m.Source.Locator).ToDictionary(g => g.Key, g => g.First());
        var confidence = ConfidencePolicy.Cap(Confidence.Medium, dqConfidence);

        foreach (var risk in extraction.Risks)
        {
            var minuteSource = minutesByLocator.TryGetValue(risk.SourceLocator, out var matched)
                ? matched.Source
                : minutes[0].Source;
            var citation = new SourceRef(minuteSource.Locator, minuteSource.StructuredExcerpt, risk.Rationale);
            findings.Add(Finding.Create(
                projectKey: slice.ProjectKey,
                summary: $"{risk.Kind}: {risk.Title} — {risk.Rationale}",
                citation: citation.ToCitation(slice.Run.UploadId),
                now: slice.Run.StartedAt,
                runId: slice.Run.RunId,
                producingAgent: Name,
                kind: FindingKind.Analysis,
                confidence: confidence,
                promptVersion: prompt.Version,
                area: HealthArea.Risk,
                severity: RiskSeverity(risk.Severity)));
        }

        return findings;
    }

    /// <summary>
    /// Maps a free-text risk/issue severity label (from a RAID record or the LLM extraction) to the
    /// RAG severity the health scorer reads for the Risk area. Unknown/blank labels default to Amber
    /// (a caution) rather than Green, so an unclassified risk is never silently treated as healthy.
    /// </summary>
    private static Severity RiskSeverity(string? label)
    {
        var text = label?.Trim().ToLowerInvariant();
        return text switch
        {
            "critical" or "high" or "severe" or "major" => Severity.Red,
            "low" or "minor" or "informational" or "info" => Severity.Green,
            _ => Severity.Amber,
        };
    }
}
