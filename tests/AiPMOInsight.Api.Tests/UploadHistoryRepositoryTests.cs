using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using AiPMOInsight.Domain.Findings;
using AiPMOInsight.Domain.Ingest;
using AiPMOInsight.Infrastructure.Findings;
using AiPMOInsight.Infrastructure.Ingest;
using AiPMOInsight.Infrastructure.Persistence;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Direct EF (in-memory) tests for the two history read paths added to the repositories:
/// <see cref="EfUploadRepository.ListAsync"/> (newest-first) and
/// <see cref="EfFindingRepository.GetByUploadIdAsync"/> (filter by cited upload, oldest-first).
/// </summary>
public class UploadHistoryRepositoryTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"repo-{Guid.NewGuid()}")
            .Options);

    [Fact]
    public async Task ListAsync_returns_all_uploads_newest_first()
    {
        using var db = NewDb();
        var t0 = new DateTimeOffset(2026, 07, 01, 0, 0, 0, TimeSpan.Zero);
        var oldest = Upload.Create("oldest.xlsx", [1], t0);
        var newest = Upload.Create("newest.xlsx", [1], t0.AddHours(2));
        var middle = Upload.Create("middle.xlsx", [1], t0.AddHours(1));
        // Insert out of order so ordering can't be an accident of insertion order.
        db.Uploads.AddRange(oldest, newest, middle);
        await db.SaveChangesAsync();

        var result = await new EfUploadRepository(db).ListAsync(CancellationToken.None);

        result.Select(u => u.FileName).Should().ContainInOrder("newest.xlsx", "middle.xlsx", "oldest.xlsx");
    }

    [Fact]
    public async Task GetByUploadIdAsync_returns_only_that_uploads_findings_oldest_first()
    {
        using var db = NewDb();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 07, 01, 0, 0, 0, TimeSpan.Zero);

        var later = MakeFinding(mine, t0.AddHours(1), "later");
        var earlier = MakeFinding(mine, t0, "earlier");
        var foreign = MakeFinding(other, t0, "foreign");
        db.Findings.AddRange(later, earlier, foreign);
        await db.SaveChangesAsync();

        var result = await new EfFindingRepository(db).GetByUploadIdAsync(mine, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(f => f.Citation.UploadId == mine);
        result.Select(f => f.Summary).Should().ContainInOrder("earlier", "later");
    }

    private static Finding MakeFinding(Guid uploadId, DateTimeOffset createdAt, string summary) =>
        Finding.Create(
            projectKey: "ALPHA",
            summary: summary,
            citation: Citation.Create(uploadId, "loc"),
            now: createdAt,
            runId: Guid.NewGuid(),
            producingAgent: "X",
            kind: FindingKind.Analysis,
            confidence: Confidence.Medium);
}
