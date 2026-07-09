using Microsoft.EntityFrameworkCore;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Domain.Ingest;
using AiPMOInsight.Infrastructure.Persistence;

namespace AiPMOInsight.Infrastructure.Ingest;

/// <summary>EF Core adapter for <see cref="IUploadRepository"/>, backed by <see cref="AppDbContext"/>.</summary>
internal sealed class EfUploadRepository(AppDbContext db) : IUploadRepository
{
    public async Task AddAsync(Upload upload, CancellationToken cancellationToken)
    {
        await db.Uploads.AddAsync(upload, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Upload?> GetAsync(Guid uploadId, CancellationToken cancellationToken) =>
        await db.Uploads
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == uploadId, cancellationToken);
}
