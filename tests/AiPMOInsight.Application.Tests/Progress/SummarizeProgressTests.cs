using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Application.Features.Progress;
using AiPMOInsight.Domain.Findings;
using Xunit;
using static AiPMOInsight.Application.Tests.HealthScoring.HealthScoringFixtures;

namespace AiPMOInsight.Application.Tests.Progress;

public class SummarizeProgressTests
{
    private sealed class StubRepo(IReadOnlyList<Finding> findings) : IFindingRepository
    {
        public Task AddAsync(Finding finding, CancellationToken ct) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Finding> f, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Finding>> GetByProjectKeyAsync(string projectKey, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Finding>>(findings.Where(f => f.ProjectKey == projectKey).ToList());
        public Task<IReadOnlyList<Finding>> GetByUploadIdAsync(Guid uploadId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Finding>>(findings.Where(f => f.Citation.UploadId == uploadId).ToList());
        public Task<IReadOnlyList<string>> DistinctProjectKeysAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(findings.Select(f => f.ProjectKey).Distinct().ToList());
    }

    private static Task<SummarizeProgress.Result?> Run(IReadOnlyList<Finding> seed, string projectKey = "ALPHA") =>
        new SummarizeProgress.Handler(new StubRepo(seed), new HealthScoringService(Options()))
            .Handle(new SummarizeProgress.Query(projectKey), CancellationToken.None);

    [Fact]
    public async Task Compares_the_two_latest_runs_reporting_score_move_and_the_changes()
    {
        var prev = Guid.NewGuid();
        var latest = Guid.NewGuid();
        var earlier = T0;
        var later = T0.AddDays(7);

        var seed = new[]
        {
            // Previous run: schedule Red + budget Red.
            AnalysisFinding(HealthArea.Schedule, Severity.Red, prev, createdAt: earlier, locator: "Milestones!Beta"),
            AnalysisFinding(HealthArea.Budget, Severity.Red, prev, createdAt: earlier, locator: "Budget!Dev"),
            // Latest run: schedule improved to Green, budget still Red (unchanged), a NEW risk.
            AnalysisFinding(HealthArea.Schedule, Severity.Green, latest, createdAt: later, locator: "Milestones!Beta"),
            AnalysisFinding(HealthArea.Budget, Severity.Red, latest, createdAt: later, locator: "Budget!Dev"),
            AnalysisFinding(HealthArea.Risk, Severity.Amber, latest, createdAt: later, locator: "RAID!R1"),
        };

        var result = await Run(seed);

        result.Should().NotBeNull();
        result!.HasPrevious.Should().BeTrue();
        result.ScoreAfter.Should().BeGreaterThan(result.ScoreBefore!.Value); // schedule Red→Green lifts the score
        result.ScoreDelta.Should().BeGreaterThan(0);

        result.MovedForward.Should().Contain(c =>
            c.Area == "Schedule" && c.Change == "Improved" && c.FromSeverity == "Red" && c.ToSeverity == "Green");
        result.MovedBackward.Should().Contain(c =>
            c.Area == "Risk" && c.Change == "New" && c.ToSeverity == "Amber");

        // Budget was unchanged (Red → Red) → in neither list.
        result.MovedForward.Should().NotContain(c => c.Area == "Budget");
        result.MovedBackward.Should().NotContain(c => c.Area == "Budget");
    }

    [Fact]
    public async Task A_single_run_reports_no_prior_period()
    {
        var run = Guid.NewGuid();
        var seed = new[] { AnalysisFinding(HealthArea.Schedule, Severity.Amber, run) };

        var result = await Run(seed);

        result.Should().NotBeNull();
        result!.HasPrevious.Should().BeFalse();
        result.MovedForward.Should().BeEmpty();
        result.MovedBackward.Should().BeEmpty();
    }

    [Fact]
    public async Task A_cleared_finding_counts_as_moved_forward()
    {
        var prev = Guid.NewGuid();
        var latest = Guid.NewGuid();

        var seed = new[]
        {
            AnalysisFinding(HealthArea.Risk, Severity.Red, prev, createdAt: T0, locator: "RAID!R1"),
            // Latest run keeps a benign finding but the risk is gone.
            AnalysisFinding(HealthArea.Schedule, Severity.Green, latest, createdAt: T0.AddDays(7), locator: "Milestones!Beta"),
        };

        var result = await Run(seed);

        result!.MovedForward.Should().Contain(c => c.Area == "Risk" && c.Change == "Cleared" && c.FromSeverity == "Red");
    }

    [Fact]
    public async Task Unknown_project_returns_null()
    {
        var result = await Run([], projectKey: "does-not-exist");

        result.Should().BeNull(); // endpoint maps null → 404
    }
}
