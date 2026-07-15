using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using AiPMOInsight.Domain.Findings;
using AiPMOInsight.Infrastructure.Findings;
using AiPMOInsight.Infrastructure.Persistence;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Direct EF (in-memory) tests for portfolio-wide project discovery
/// (<see cref="EfFindingRepository.DistinctProjectKeysAsync"/>, add-executive-portfolio-dashboard) —
/// enumerate the distinct project keys on record without a caller already knowing any key.
/// </summary>
public class FindingRepositoryDistinctKeysTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"repo-{Guid.NewGuid()}")
            .Options);

    [Fact]
    public async Task Returns_each_distinct_key_once()
    {
        using var db = NewDb();
        // Three distinct keys; ALPHA has several findings so "distinct" is actually exercised.
        db.Findings.AddRange(
            MakeFinding("ALPHA", "a1"), MakeFinding("ALPHA", "a2"), MakeFinding("ALPHA", "a3"),
            MakeFinding("BETA", "b1"),
            MakeFinding("GAMMA", "g1"));
        await db.SaveChangesAsync();

        var keys = await new EfFindingRepository(db).DistinctProjectKeysAsync(CancellationToken.None);

        keys.Should().BeEquivalentTo(["ALPHA", "BETA", "GAMMA"]);
    }

    [Fact]
    public async Task Empty_store_returns_no_keys()
    {
        using var db = NewDb();

        var keys = await new EfFindingRepository(db).DistinctProjectKeysAsync(CancellationToken.None);

        keys.Should().BeEmpty();
    }

    private static Finding MakeFinding(string projectKey, string summary) =>
        Finding.Create(
            projectKey: projectKey,
            summary: summary,
            citation: Citation.Create(Guid.NewGuid(), "loc"),
            now: new DateTimeOffset(2026, 07, 01, 0, 0, 0, TimeSpan.Zero),
            runId: Guid.NewGuid(),
            producingAgent: "X",
            kind: FindingKind.Analysis,
            confidence: Confidence.Medium,
            area: HealthArea.Schedule,
            severity: Severity.Amber);
}
