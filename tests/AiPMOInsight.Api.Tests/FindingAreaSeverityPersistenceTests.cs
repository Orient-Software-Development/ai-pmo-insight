using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using AiPMOInsight.Domain.Findings;
using AiPMOInsight.Infrastructure.Findings;
using AiPMOInsight.Infrastructure.Persistence;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Round-trip tests for the structured health <see cref="HealthArea"/> + <see cref="Severity"/> added
/// to <see cref="Finding"/>: an Analysis finding persists and reads back with the same values; a
/// non-analysis finding reads back null for both (the fields only describe Analysis findings).
/// A fresh <see cref="AppDbContext"/> shares the store by database name, so writing then re-reading
/// through a second context exercises the mapping, not just the in-memory object graph.
/// </summary>
public class FindingAreaSeverityPersistenceTests
{
    private static DbContextOptions<AppDbContext> SharedStore(string name) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

    [Fact]
    public async Task Analysis_finding_round_trips_area_and_severity()
    {
        var uploadId = Guid.NewGuid();
        var options = SharedStore($"area-sev-{uploadId}");

        using (var writeDb = new AppDbContext(options))
        {
            var finding = Finding.Create(
                projectKey: "ALPHA",
                summary: "Forecast exceeds budget by 18%.",
                citation: Citation.Create(uploadId, "Budget!row2"),
                now: DateTimeOffset.UtcNow,
                runId: Guid.NewGuid(),
                producingAgent: "Financial",
                kind: FindingKind.Analysis,
                confidence: Confidence.High,
                area: HealthArea.Budget,
                severity: Severity.Red);
            await new EfFindingRepository(writeDb).AddAsync(finding, CancellationToken.None);
        }

        using var readDb = new AppDbContext(options);
        var read = await new EfFindingRepository(readDb).GetByUploadIdAsync(uploadId, CancellationToken.None);

        read.Should().ContainSingle();
        read[0].Area.Should().Be(HealthArea.Budget);
        read[0].Severity.Should().Be(Severity.Red);
    }

    [Fact]
    public async Task Non_analysis_finding_reads_back_null_area_and_severity()
    {
        var uploadId = Guid.NewGuid();
        var options = SharedStore($"area-sev-null-{uploadId}");

        using (var writeDb = new AppDbContext(options))
        {
            var narrative = Finding.Create(
                projectKey: "ALPHA",
                summary: "Overall the project is on track.",
                citation: Citation.Create(uploadId, "narrative"),
                now: DateTimeOffset.UtcNow,
                runId: Guid.NewGuid(),
                producingAgent: "Narrative",
                kind: FindingKind.Narrative,
                confidence: Confidence.Medium,
                promptVersion: "sha256:abc");
            await new EfFindingRepository(writeDb).AddAsync(narrative, CancellationToken.None);
        }

        using var readDb = new AppDbContext(options);
        var read = await new EfFindingRepository(readDb).GetByUploadIdAsync(uploadId, CancellationToken.None);

        read.Should().ContainSingle();
        read[0].Area.Should().BeNull();
        read[0].Severity.Should().BeNull();
    }
}
