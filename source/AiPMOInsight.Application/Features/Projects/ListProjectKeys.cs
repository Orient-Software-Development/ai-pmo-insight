using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Application.Features.Projects;

/// <summary>
/// Vertical slice: enumerate every project key on record, sorted alphabetically. Reads through
/// <see cref="IFindingRepository.DistinctProjectKeysAsync"/> (no first-class project entity — derived
/// from the findings' <c>project_key</c> column). Feeds the L2 project switcher dropdown so callers
/// no longer need to type keys by hand. Shared-workspace visibility: any authenticated caller sees
/// every project.
/// </summary>
public static class ListProjectKeys
{
    public sealed record Query : IRequest<IReadOnlyList<string>>;

    internal sealed class Handler(IFindingRepository findings) : IRequestHandler<Query, IReadOnlyList<string>>
    {
        public async Task<IReadOnlyList<string>> Handle(Query request, CancellationToken cancellationToken)
        {
            var keys = await findings.DistinctProjectKeysAsync(cancellationToken);
            // Sort in the slice — the repository makes no order guarantee, and a stable order is a
            // UX requirement for the dropdown that consumes this.
            return keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        }
    }
}
