using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Abstractions;

/// <summary>
/// Port for finding persistence. Implemented in Infrastructure
/// (dependency rule: Application defines the interface, Infrastructure depends on it).
/// </summary>
public interface IFindingRepository
{
    Task AddAsync(Finding finding, CancellationToken cancellationToken);

    Task<IReadOnlyList<Finding>> GetByProjectKeyAsync(string projectKey, CancellationToken cancellationToken);
}
