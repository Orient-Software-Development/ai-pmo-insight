using System.Text;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>Input to the Review agent: the run slice, the findings, the narrative (#7), and the challenge (#8).</summary>
public sealed record ReviewInput(
    ProjectSlice Slice,
    IReadOnlyList<Finding> Findings,
    Finding Narrative,
    Finding Challenge);

/// <summary>The Review agent's LLM output contract: anticipated questions grouped by audience.</summary>
public sealed record ReviewResult(IReadOnlyDictionary<string, IReadOnlyList<string>> QuestionsByAudience);

/// <summary>
/// Agent #9 — Review (LLM hybrid). Predicts the questions stakeholders will ask, grouped by audience
/// (executive, sponsor, data lead, peer PM), reading the narrative (#7), the challenge (#8), and the
/// findings. Persists a finding of <see cref="FindingKind.Review"/>. It is NOT a keep/drop gate — it
/// never removes findings; its output is preparation guidance the reader sees.
/// </summary>
public sealed class ReviewSkill(ILlmClient llm, PromptRegistry prompts)
    : IAgentSkill<ReviewInput, Finding>
{
    private const string PromptKey = "review";

    public string Name => LlmAgentSkills.Review;

    public async Task<Finding> ExecuteAsync(ReviewInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var analysisFindings = input.Findings.Where(f => f.Kind == FindingKind.Analysis).ToList();
        var summary = new StringBuilder();
        string? promptVersion = null;

        if (analysisFindings.Count == 0)
        {
            summary.AppendLine("Limited questions anticipated for a clean status; be ready to confirm the data is current.");
        }
        else
        {
            var prompt = prompts.Get(PromptKey);
            promptVersion = prompt.Version;

            var request = new LlmRequest
            {
                SkillName = Name,
                SystemPrompt = prompt.Content,
                Prompt = $"NARRATIVE:\n{input.Narrative.Summary}\n\nCHALLENGE:\n{input.Challenge.Summary}\n\nFINDINGS:\n" +
                         string.Join("\n", analysisFindings.Select(f => $"- {f.Summary}")),
                PromptVersion = prompt.Version,
            };

            var result = await llm.CompleteAsync<ReviewResult>(request, cancellationToken);
            foreach (var (audience, questions) in result.QuestionsByAudience)
            {
                summary.AppendLine($"{audience}: {string.Join(" | ", questions)}");
            }
        }

        return Finding.Create(
            projectKey: input.Slice.ProjectKey,
            summary: summary.ToString().TrimEnd(),
            citation: new SourceRef("synthesis:review").ToCitation(input.Slice.Run.UploadId),
            now: input.Slice.Run.StartedAt,
            runId: input.Slice.Run.RunId,
            producingAgent: Name,
            kind: FindingKind.Review,
            confidence: Confidence.Medium,
            promptVersion: promptVersion);
    }
}
