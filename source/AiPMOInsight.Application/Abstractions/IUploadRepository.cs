using AiPMOInsight.Domain.Ingest;

namespace AiPMOInsight.Application.Abstractions;

/// <summary>
/// Port for upload (landing area) persistence. Implemented in Infrastructure
/// (dependency rule: Application defines the interface, Infrastructure depends on it).
/// </summary>
public interface IUploadRepository
{
    Task AddAsync(Upload upload, CancellationToken cancellationToken);

    Task<Upload?> GetAsync(Guid uploadId, CancellationToken cancellationToken);

    /// <summary>All uploads, most-recently-uploaded first. Used by the history read surface.</summary>
    Task<IReadOnlyList<Upload>> ListAsync(CancellationToken cancellationToken);
}
