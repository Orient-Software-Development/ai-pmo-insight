using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Findings;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Findings;

public class GetProjectFindingsTests
{
    private sealed class StubRepo(IReadOnlyList<Finding> findings) : IFindingRepository
    {
        public Task AddAsync(Finding finding, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Finding> f, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<Finding>> GetByProjectKeyAsync(string projectKey, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Finding>>(findings.Where(f => f.ProjectKey == projectKey).ToList());
        public Task<IReadOnlyList<Finding>> GetByUploadIdAsync(Guid uploadId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Finding>>(findings.Where(f => f.Citation.UploadId == uploadId).ToList());
    }

    private static Finding Make(Guid runId, FindingKind kind, DateTimeOffset createdAt, string agent = "X")
    {
        var citation = Citation.Create(Guid.NewGuid(), "loc");
        return Finding.Create("ALPHA", $"{kind} summary", citation, createdAt, runId, agent, kind, Confidence.Medium);
    }

    private static Task<GetProjectFindings.Result> Run(IReadOnlyList<Finding> seed) =>
        new GetProjectFindings.Handler(new StubRepo(seed)).Handle(new GetProjectFindings.Query("ALPHA"), CancellationToken.None);

    [Fact]
    public async Task Partitions_the_latest_run_into_four_sections()
    {
        var oldRun = Guid.NewGuid();
        var newRun = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 07, 10, 0, 0, 0, TimeSpan.Zero);
        var t1 = t0.AddHours(1);

        var seed = new[]
        {
            Make(oldRun, FindingKind.Analysis, t0),         // prior run — excluded
            Make(newRun, FindingKind.Analysis, t1, "Status"),
            Make(newRun, FindingKind.Analysis, t1, "Financial"),
            Make(newRun, FindingKind.Narrative, t1),
            Make(newRun, FindingKind.Challenge, t1),
            Make(newRun, FindingKind.Review, t1),
        };

        var result = await Run(seed);

        result.ProjectKey.Should().Be("ALPHA");
        result.Findings.Should().HaveCount(2);
        result.Findings.Should().OnlyContain(f => f.Kind == "Analysis");
        result.Narrative.Should().ContainSingle();
        result.Challenge.Should().ContainSingle();
        result.Review.Should().ContainSingle();
        // Only the latest run is surfaced.
        result.Findings.Should().OnlyContain(f => f.RunId == newRun);
    }

    [Fact]
    public async Task Unanalyzed_project_returns_empty_sections()
    {
        var result = await Run([]);

        result.Findings.Should().BeEmpty();
        result.Narrative.Should().BeEmpty();
        result.Challenge.Should().BeEmpty();
        result.Review.Should().BeEmpty();
    }
}
