using System.Text;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>Input to the Challenge agent: the run slice, DQ signal, the findings, and the narrative (#7).</summary>
public sealed record ChallengeInput(
    ProjectSlice Slice,
    DataQualitySignal Quality,
    IReadOnlyList<Finding> Findings,
    Finding Narrative);

/// <summary>One adversarial critique of a finding or the narrative.</summary>
public sealed record Critique(string Target, string Concern, string Severity, string Suggestion);

/// <summary>The Challenge agent's LLM output contract.</summary>
public sealed record ChallengeResult(IReadOnlyList<Critique> Critiques);

/// <summary>
/// Agent #8 — Challenge (LLM hybrid). Produces an adversarial critique of the findings and narrative
/// (weak claims, unsupported numbers, alternative interpretations, missing caveats) via the
/// <see cref="ILlmClient"/>, augmented with deterministic checks (broken evidence links, low
/// data-quality caveat). Reads #7 + the findings and persists a finding of
/// <see cref="FindingKind.Challenge"/>. It NEVER deletes findings — it only attaches a critique.
/// </summary>
public sealed class ChallengeSkill(ILlmClient llm, PromptRegistry prompts)
    : IAgentSkill<ChallengeInput, Finding>
{
    private const string PromptKey = "challenge";

    public string Name => "Challenge";

    public async Task<Finding> ExecuteAsync(ChallengeInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var analysisFindings = input.Findings.Where(f => f.Kind == FindingKind.Analysis).ToList();
        var summary = new StringBuilder();
        string? promptVersion = null;

        if (analysisFindings.Count == 0)
        {
            summary.AppendLine("No findings to challenge; nothing to critique this run.");
        }
        else
        {
            var prompt = prompts.Get(PromptKey);
            promptVersion = prompt.Version;

            var request = new LlmRequest
            {
                SkillName = Name,
                Prompt = $"{prompt.Content}\n\nNARRATIVE:\n{input.Narrative.Summary}\n\nFINDINGS:\n" +
                         string.Join("\n", analysisFindings.Select(f => $"- {f.Summary}")),
                PromptVersion = prompt.Version,
            };

            var result = await llm.CompleteAsync<ChallengeResult>(request, cancellationToken);
            foreach (var critique in result.Critiques)
            {
                summary.AppendLine($"[{critique.Severity}] {critique.Target}: {critique.Concern} → {critique.Suggestion}");
            }
        }

        // Deterministic augmentation — independent of the LLM.
        foreach (var broken in analysisFindings.Where(f => string.IsNullOrWhiteSpace(f.Citation.Locator)))
        {
            summary.AppendLine($"[high] Evidence link is broken for: {broken.Summary}");
        }

        if (ConfidencePolicy.FromSignals(input.Quality) == Confidence.Low)
        {
            summary.AppendLine("[high] Caveat: overall data quality is low — treat these findings with caution.");
        }

        return Finding.Create(
            projectKey: input.Slice.ProjectKey,
            summary: summary.ToString().TrimEnd(),
            citation: new SourceRef("synthesis:challenge").ToCitation(input.Slice.Run.UploadId),
            now: input.Slice.Run.StartedAt,
            runId: input.Slice.Run.RunId,
            producingAgent: Name,
            kind: FindingKind.Challenge,
            confidence: ConfidencePolicy.FromSignals(input.Quality),
            promptVersion: promptVersion);
    }
}
