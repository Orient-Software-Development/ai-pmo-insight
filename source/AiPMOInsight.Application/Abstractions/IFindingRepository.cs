using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Abstractions;

/// <summary>
/// Port for finding persistence. Implemented in Infrastructure
/// (dependency rule: Application defines the interface, Infrastructure depends on it).
/// </summary>
public interface IFindingRepository
{
    Task AddAsync(Finding finding, CancellationToken cancellationToken);

    /// <summary>Persists a whole run's findings in one unit of work.</summary>
    Task AddRangeAsync(IEnumerable<Finding> findings, CancellationToken cancellationToken);

    Task<IReadOnlyList<Finding>> GetByProjectKeyAsync(string projectKey, CancellationToken cancellationToken);

    /// <summary>All findings citing the given upload, oldest-first. Used by the history read surface.</summary>
    Task<IReadOnlyList<Finding>> GetByUploadIdAsync(Guid uploadId, CancellationToken cancellationToken);

    /// <summary>
    /// The distinct project keys present in the findings store, each once. Portfolio-wide discovery
    /// (Level-1 dashboard): enumerate projects without a caller already knowing any key. No first-class
    /// project entity — this is derived from the findings' <c>project_key</c> column.
    /// </summary>
    Task<IReadOnlyList<string>> DistinctProjectKeysAsync(CancellationToken cancellationToken);
}
