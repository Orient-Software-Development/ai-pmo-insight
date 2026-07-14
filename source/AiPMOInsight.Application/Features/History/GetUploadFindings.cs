using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Messaging;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.History;

/// <summary>
/// Vertical slice: read an upload's <b>latest</b> analysis run for the history surface. Mirrors the
/// project-scoped Level-2 view but keyed by upload id: findings partition into analysis / narrative /
/// challenge / review, showing only the most recent run (re-analysis appends; prior runs stay
/// persisted but are not surfaced). Returns <c>null</c> when the upload id is unknown (endpoint maps
/// that to 404); a known upload with no findings returns empty sections (200).
/// </summary>
public static class GetUploadFindings
{
    public sealed record Query(Guid UploadId) : IRequest<Result?>;

    public sealed record Result(
        Guid UploadId,
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
        DateTimeOffset CreatedAt);

    public sealed record CitationView(Guid UploadId, string Locator, string? StructuredExcerpt, string? TextSnippet);

    internal sealed class Handler(IUploadRepository uploads, IFindingRepository findings)
        : IRequestHandler<Query, Result?>
    {
        public async Task<Result?> Handle(Query request, CancellationToken cancellationToken)
        {
            // Distinguish "unknown upload" (404) from "known upload, no findings" (200 empty) —
            // findings alone cannot tell them apart, so check existence first.
            var upload = await uploads.GetAsync(request.UploadId, cancellationToken);
            if (upload is null)
            {
                return null;
            }

            var all = await findings.GetByUploadIdAsync(request.UploadId, cancellationToken);
            if (all.Count == 0)
            {
                return new Result(request.UploadId, [], [], [], []);
            }

            // Show the latest run only (re-analysis appends; prior runs stay persisted).
            var latestRunId = all.MaxBy(f => f.CreatedAt)!.RunId;
            var latest = all.Where(f => f.RunId == latestRunId).Select(ToView).ToList();

            return new Result(
                request.UploadId,
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
                f.CreatedAt);
    }
}
