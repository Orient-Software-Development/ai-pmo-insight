using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Domain.Findings;
using Xunit;
using static AiPMOInsight.Application.Tests.HealthScoring.HealthScoringFixtures;

namespace AiPMOInsight.Application.Tests.HealthScoring;

public class ScoreProjectTests
{
    private sealed class StubRepo(IReadOnlyList<Finding> findings) : IFindingRepository
    {
        public Task AddAsync(Finding finding, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Finding> f, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<Finding>> GetByProjectKeyAsync(string projectKey, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Finding>>(findings.Where(f => f.ProjectKey == projectKey).ToList());
        public Task<IReadOnlyList<Finding>> GetByUploadIdAsync(Guid uploadId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Finding>>(findings.Where(f => f.Citation.UploadId == uploadId).ToList());
    }

    private static Task<ScoreProject.Result?> Run(IReadOnlyList<Finding> seed, string projectKey = "ALPHA") =>
        new ScoreProject.Handler(new StubRepo(seed), new HealthScoringService(Options()))
            .Handle(new ScoreProject.Query(projectKey), CancellationToken.None);

    [Fact]
    public async Task Returns_the_full_audit_result_for_a_scored_project()
    {
        var run = Guid.NewGuid();
        var seed = new[]
        {
            AnalysisFinding(HealthArea.Schedule, Severity.Amber, run),
            AnalysisFinding(HealthArea.Budget, Severity.Red, run),
        };

        var result = await Run(seed);

        result.Should().NotBeNull();
        result!.ProjectKey.Should().Be("ALPHA");
        result.Score.Should().NotBeNull();
        result.Score!.RawBucket.Should().Be("Red");
        result.Score.Areas.Should().HaveCount(2);
        result.Score.AppliedOverrides.Should().Contain(o => o.RuleId == "forecast-overrun-critical");
    }

    [Fact]
    public async Task Unknown_project_returns_null()
    {
        var result = await Run([], projectKey: "does-not-exist");

        result.Should().BeNull(); // endpoint maps null → 404
    }

    [Fact]
    public async Task Project_with_no_scoreable_findings_returns_a_no_score_result()
    {
        var run = Guid.NewGuid();
        var narrative = Finding.Create(
            "ALPHA", "prose", Citation.Create(Guid.NewGuid(), "loc"), T0, run, "Narrative",
            FindingKind.Narrative, Confidence.Medium, promptVersion: "sha256:x");

        var result = await Run([narrative]);

        result.Should().NotBeNull();     // the project exists (has findings) → 200, not 404
        result!.Score.Should().BeNull(); // but there is nothing to score yet
    }
}
