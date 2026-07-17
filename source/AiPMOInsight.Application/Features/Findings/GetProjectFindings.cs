using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Messaging;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Findings;

/// <summary>
/// Vertical slice: the Level-2 (individual project status) read path. Returns the <b>latest analysis
/// run</b> for a project key, partitioned into four sections — analytic findings, the narrative, the
/// challenge critique, and the review — each finding with its citation and provenance. Prior runs
/// remain persisted (re-analysis appends) but the Level-2 view shows the current picture. An
/// unanalyzed key returns empty sections (200).
/// </summary>
public static class GetProjectFindings
{
    public sealed record Query(string ProjectKey) : IRequest<Result>;

    public sealed record Result(
        string ProjectKey,
        IReadOnlyList<FindingView> Findings,
        IReadOnlyList<FindingView> Narrative,
        IReadOnlyList<FindingView> Challenge,
        IReadOnlyList<FindingView> Review);

    public sealed record FindingView(
        Guid Id,
        string ProjectKey,
        string Summary,
        string Kind,
        string Confidence,
        string ProducingAgent,
        string? PromptVersion,
        Guid RunId,
        CitationView Citation,
        DateTimeOffset CreatedAt,
        // Self-describing analysis fields (null on non-Analysis findings): the health area + severity the
        // agent computed, and any structured metric (value/unit + a detail bag, e.g. a decision's
        // owner/deadline/consequence). The L2 view groups by Area and renders the decisions/milestones panels.
        string? Area,
        string? Severity,
        decimal? MetricValue,
        string? MetricUnit,
        IReadOnlyDictionary<string, string>? MetricDetail);

    public sealed record CitationView(Guid UploadId, string Locator, string? StructuredExcerpt, string? TextSnippet);

    internal sealed class Handler(IFindingRepository repository) : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            var all = await repository.GetByProjectKeyAsync(request.ProjectKey, cancellationToken);
            if (all.Count == 0)
            {
                return new Result(request.ProjectKey, [], [], [], []);
            }

            // Show the latest run only (re-analysis appends; prior runs stay persisted).
            var latestRunId = all.MaxBy(f => f.CreatedAt)!.RunId;
            var latest = all.Where(f => f.RunId == latestRunId).Select(ToView).ToList();

            return new Result(
                request.ProjectKey,
                latest.Where(f => f.Kind == nameof(FindingKind.Analysis)).ToList(),
                latest.Where(f => f.Kind == nameof(FindingKind.Narrative)).ToList(),
                latest.Where(f => f.Kind == nameof(FindingKind.Challenge)).ToList(),
                latest.Where(f => f.Kind == nameof(FindingKind.Review)).ToList());
        }

        private static FindingView ToView(Finding f) =>
            new(f.Id,
                f.ProjectKey,
                f.Summary,
                f.Kind.ToString(),
                f.Confidence.ToString(),
                f.ProducingAgent,
                f.PromptVersion,
                f.RunId,
                new CitationView(f.Citation.UploadId, f.Citation.Locator, f.Citation.StructuredExcerpt, f.Citation.TextSnippet),
                f.CreatedAt,
                f.Area?.ToString(),
                f.Severity?.ToString(),
                f.MetricValue,
                f.MetricUnit,
                f.MetricDetail);
    }
}
