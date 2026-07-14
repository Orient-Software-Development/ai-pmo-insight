using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>A risk/issue the LLM extracted from meeting minutes (structured output contract).</summary>
public sealed record ExtractedRisk(string Title, string Kind, string Severity, string Rationale);

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
                slice, Name, $"{raid.Type}{severity}: {raid.Description}", raid.Source, dqConfidence));
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
            Prompt = $"MINUTES:\n{string.Join("\n", minutes.Select(m => m.Text))}",
            PromptVersion = prompt.Version,
        };

        var extraction = await llm.CompleteAsync<MinuteRiskExtraction>(request, cancellationToken);

        // Cite the minutes the extraction rests on; cap confidence by data quality.
        var minutesSource = minutes[0].Source;
        var confidence = ConfidencePolicy.Cap(Confidence.Medium, dqConfidence);

        foreach (var risk in extraction.Risks)
        {
            var citation = new SourceRef(minutesSource.Locator, minutesSource.StructuredExcerpt, risk.Rationale);
            findings.Add(Finding.Create(
                projectKey: slice.ProjectKey,
                summary: $"{risk.Kind}: {risk.Title} — {risk.Rationale}",
                citation: citation.ToCitation(slice.Run.UploadId),
                now: slice.Run.StartedAt,
                runId: slice.Run.RunId,
                producingAgent: Name,
                kind: FindingKind.Analysis,
                confidence: confidence,
                promptVersion: prompt.Version));
        }

        return findings;
    }
}
