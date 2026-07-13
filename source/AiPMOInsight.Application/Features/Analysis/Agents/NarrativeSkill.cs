using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>Input to the Narrative agent: the run slice, DQ signal, and the merged analysis findings.</summary>
public sealed record NarrativeInput(ProjectSlice Slice, DataQualitySignal Quality, IReadOnlyList<Finding> Findings);

/// <summary>An actionable recommendation naming an owner, a deadline, and the rationale.</summary>
public sealed record Recommendation(string Owner, string Deadline, string Action, string Rationale);

/// <summary>The Narrative agent's output contract (template- or LLM-produced).</summary>
public sealed record NarrativeResult(string Status, string Narrative, Recommendation Recommendation);

/// <summary>
/// Agent #7 — Narrative (hybrid, template-first). Synthesises the merged findings into a prose status
/// plus a recommendation (owner / deadline / rationale). Recurring shapes render deterministically
/// from templates (routine GREEN, DQ-driven "Needs PM Review", single- or two-signal RED); only
/// genuinely complex cases (3+ cross-referencing signals, or a minute-extracted signal) fall back to
/// the <see cref="ILlmClient"/>. Output is a single finding of <see cref="FindingKind.Narrative"/>;
/// the LLM path stamps the prompt version, the template path does not.
/// </summary>
public sealed class NarrativeSkill(ILlmClient llm, PromptRegistry prompts)
    : IAgentSkill<NarrativeInput, Finding>
{
    private const string PromptKey = "narrative";
    private const int ComplexSignalThreshold = 3;

    public string Name => "Narrative";

    public async Task<Finding> ExecuteAsync(NarrativeInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var analysisFindings = input.Findings.Where(f => f.Kind == FindingKind.Analysis).ToList();
        var minuteExtracted = analysisFindings.Any(f => f.PromptVersion is not null);
        var complex = analysisFindings.Count >= ComplexSignalThreshold || minuteExtracted;

        if (analysisFindings.Count > 0 && complex)
        {
            return await LlmNarrative(input, analysisFindings, cancellationToken);
        }

        return TemplateNarrative(input, analysisFindings);
    }

    private Finding TemplateNarrative(NarrativeInput input, IReadOnlyList<Finding> findings)
    {
        var dqConfidence = ConfidencePolicy.FromSignals(input.Quality);
        var result = ClassifyTemplate(findings, dqConfidence);
        return ToFinding(input.Slice, result, dqConfidence, promptVersion: null);
    }

    private async Task<Finding> LlmNarrative(NarrativeInput input, IReadOnlyList<Finding> findings, CancellationToken ct)
    {
        var prompt = prompts.Get(PromptKey);
        var request = new LlmRequest
        {
            SkillName = Name,
            Prompt = $"{prompt.Content}\n\nFINDINGS:\n{string.Join("\n", findings.Select(f => $"- {f.Summary}"))}",
            PromptVersion = prompt.Version,
        };

        var result = await llm.CompleteAsync<NarrativeResult>(request, ct);
        var confidence = ConfidencePolicy.Cap(Confidence.Medium, ConfidencePolicy.FromSignals(input.Quality));
        return ToFinding(input.Slice, result, confidence, prompt.Version);
    }

    private static NarrativeResult ClassifyTemplate(IReadOnlyList<Finding> findings, Confidence dqConfidence)
    {
        if (findings.Count == 0)
        {
            return new NarrativeResult(
                "green",
                "No material issues detected across status, financial, and resource checks.",
                new Recommendation("Project Manager", "n/a", "Maintain the current plan", "All checks within tolerance."));
        }

        // Data-quality-dominated + low trust → ask a human to look before acting.
        if (dqConfidence == Confidence.Low && findings.All(f => f.ProducingAgent == "DataQuality"))
        {
            return new NarrativeResult(
                "needs-review",
                "Data quality is too low to assess status confidently; the underlying data needs attention.",
                new Recommendation("Project Manager", "this week", "Resolve the data-quality gaps, then re-run analysis", "Findings rest on incomplete or stale data."));
        }

        // One or two dominant signals → concise RED narrative naming the primary driver.
        var primary = findings[0];
        var secondary = findings.Count > 1 ? $" A secondary signal: {findings[1].Summary}" : string.Empty;
        return new NarrativeResult(
            "red",
            $"Status is RED, driven by: {primary.Summary}.{secondary}",
            new Recommendation("Project Manager", "next 2 weeks", "Address the primary driver above", primary.Summary));
    }

    private static Finding ToFinding(ProjectSlice slice, NarrativeResult result, Confidence confidence, string? promptVersion)
    {
        var summary =
            $"[{result.Status}] {result.Narrative}\n" +
            $"Recommendation ({result.Recommendation.Owner}, by {result.Recommendation.Deadline}): " +
            $"{result.Recommendation.Action} — {result.Recommendation.Rationale}";

        return Finding.Create(
            projectKey: slice.ProjectKey,
            summary: summary,
            citation: new SourceRef("synthesis:narrative").ToCitation(slice.Run.UploadId),
            now: slice.Run.StartedAt,
            runId: slice.Run.RunId,
            producingAgent: "Narrative",
            kind: FindingKind.Narrative,
            confidence: confidence,
            promptVersion: promptVersion);
    }
}
