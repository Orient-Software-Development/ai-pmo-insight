using Microsoft.EntityFrameworkCore;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Domain.Findings;
using AiPMOInsight.Infrastructure.Persistence;

namespace AiPMOInsight.Infrastructure.Findings;

/// <summary>EF Core adapter for <see cref="IFindingRepository"/>, backed by <see cref="AppDbContext"/>.</summary>
internal sealed class EfFindingRepository(AppDbContext db) : IFindingRepository
{
    public async Task AddAsync(Finding finding, CancellationToken cancellationToken)
    {
        await db.Findings.AddAsync(finding, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Finding> findings, CancellationToken cancellationToken)
    {
        await db.Findings.AddRangeAsync(findings, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Finding>> GetByProjectKeyAsync(string projectKey, CancellationToken cancellationToken) =>
        await db.Findings
            .AsNoTracking()
            .Where(f => f.ProjectKey == projectKey)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
}
