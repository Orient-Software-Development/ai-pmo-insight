using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Messaging;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Findings;

/// <summary>
/// Vertical slice: the Level-2 (individual project status) read path. Returns the findings recorded
/// for a project key, each with its citation. Query + handler colocated.
/// </summary>
public static class GetProjectFindings
{
    public sealed record Query(string ProjectKey) : IRequest<IReadOnlyList<Result>>;

    public sealed record Result(Guid Id, string ProjectKey, string Summary, CitationResult Citation, DateTimeOffset CreatedAt);

    public sealed record CitationResult(Guid UploadId, string Locator);

    internal sealed class Handler(IFindingRepository repository) : IRequestHandler<Query, IReadOnlyList<Result>>
    {
        public async Task<IReadOnlyList<Result>> Handle(Query request, CancellationToken cancellationToken)
        {
            var findings = await repository.GetByProjectKeyAsync(request.ProjectKey, cancellationToken);

            return findings
                .Select(f => new Result(
                    f.Id,
                    f.ProjectKey,
                    f.Summary,
                    new CitationResult(f.Citation.UploadId, f.Citation.Locator),
                    f.CreatedAt))
                .ToList();
        }
    }
}
