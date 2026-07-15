using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.DataQuality;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Domain.Findings;
using Xunit;
using static AiPMOInsight.Application.Tests.HealthScoring.HealthScoringFixtures;

namespace AiPMOInsight.Application.Tests.DataQuality;

/// <summary>
/// The Level-3 data-quality roll-up (add-data-quality-dashboard). Enumerates every project, collects
/// its latest run's <see cref="HealthArea.DataQuality"/> findings, and rolls them up into: a confidence
/// block (mean of scored projects' <see cref="HealthScore.Confidence"/> + the configured publish
/// threshold + a below-target flag), a worst-first cited items list, and counts. Reuses the same pure
/// <see cref="HealthScoringService"/> as L1/L2, so the confidence figure never disagrees with the scores.
/// Fixture options: ConfidenceFloor=50; Low=30/Medium=70/High=100; DataQuality weight=5.
/// </summary>
public class SummarizeDataQualityTests
{
    private sealed class MultiRepo(IReadOnlyList<Finding> findings) : IFindingRepository
    {
        public Task AddAsync(Finding f, CancellationToken ct) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Finding> f, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Finding>> GetByProjectKeyAsync(string projectKey, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Finding>>(findings.Where(f => f.ProjectKey == projectKey).ToList());
        public Task<IReadOnlyList<Finding>> GetByUploadIdAsync(Guid uploadId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Finding>>(findings.Where(f => f.Citation.UploadId == uploadId).ToList());
        public Task<IReadOnlyList<string>> DistinctProjectKeysAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(findings.Select(f => f.ProjectKey).Distinct().ToList());
    }

    private static Task<SummarizeDataQuality.Result> Run(IReadOnlyList<Finding> seed) =>
        new SummarizeDataQuality.Handler(new MultiRepo(seed), new HealthScoringService(Options()), Options())
            .Handle(new SummarizeDataQuality.Query(), CancellationToken.None);

    private static Finding Dq(Severity sev, string project, Guid runId,
        DateTimeOffset? createdAt = null, Confidence confidence = Confidence.High, string locator = "sheet!row2") =>
        AnalysisFinding(HealthArea.DataQuality, sev, runId, createdAt, confidence, project, locator);

    // ── §1 collection + latest-run filtering ─────────────────────────────────────────────────

    [Fact]
    public async Task Collects_only_the_latest_runs_data_quality_findings()
    {
        var project = "ALPHA";
        var olderRun = Guid.NewGuid();
        var newerRun = Guid.NewGuid();
        var seed = new[]
        {
            Dq(Severity.Amber, project, olderRun, createdAt: T0, locator: "old!r1"),
            Dq(Severity.Amber, project, newerRun, createdAt: T0.AddDays(1), locator: "new!r1"),
        };

        var result = await Run(seed);

        result.Items.Should().ContainSingle();
        result.Items.Single().CitationLocator.Should().Be("new!r1");
    }

    [Fact]
    public async Task Excludes_non_data_quality_findings_from_the_same_run()
    {
        var run = Guid.NewGuid();
        var seed = new[]
        {
            Dq(Severity.Amber, "ALPHA", run, locator: "dq!r1"),
            AnalysisFinding(HealthArea.Budget, Severity.Red, run, projectKey: "ALPHA", locator: "budget!r1"),
        };

        var result = await Run(seed);

        result.Items.Should().ContainSingle();
        result.Items.Single().CitationLocator.Should().Be("dq!r1");
    }

    // ── §2 confidence block ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Mean_confidence_is_the_mean_over_scored_projects()
    {
        // High=100, Low=30 → mean of {100, 30} = 65 (≥ floor 50 → not below target).
        var seed = new[]
        {
            Dq(Severity.Amber, "A", Guid.NewGuid(), confidence: Confidence.High),
            Dq(Severity.Amber, "B", Guid.NewGuid(), confidence: Confidence.Low),
        };

        var result = await Run(seed);

        result.Confidence.Mean.Should().BeApproximately(65d, 0.001);
        result.Confidence.Threshold.Should().Be(50);
        result.Confidence.BelowTarget.Should().BeFalse();
    }

    [Fact]
    public async Task Below_target_flag_is_set_when_mean_is_under_the_configured_floor()
    {
        // Single Low-confidence project → mean 30 < floor 50 → below target.
        var seed = new[] { Dq(Severity.Amber, "A", Guid.NewGuid(), confidence: Confidence.Low) };

        var result = await Run(seed);

        result.Confidence.Mean.Should().BeApproximately(30d, 0.001);
        result.Confidence.Threshold.Should().Be(50);
        result.Confidence.BelowTarget.Should().BeTrue();
    }

    [Fact]
    public async Task Unscoreable_project_does_not_contribute_to_the_mean()
    {
        // A narrative-only project has a null score; it must not widen the denominator.
        var narrative = Finding.Create(
            "NOSCORE", "prose", Citation.Create(Guid.NewGuid(), "loc"), T0, Guid.NewGuid(), "Narrative",
            FindingKind.Narrative, Confidence.Medium, promptVersion: "sha256:x");
        var seed = new[]
        {
            narrative,
            Dq(Severity.Amber, "SCORED", Guid.NewGuid(), confidence: Confidence.High),
        };

        var result = await Run(seed);

        result.Confidence.Mean.Should().BeApproximately(100d, 0.001);
    }

    // ── §3 items list: worst-first, cited, counts ────────────────────────────────────────────

    [Fact]
    public async Task Each_item_carries_project_issue_severity_and_a_citation()
    {
        var seed = new[] { Dq(Severity.Amber, "ALPHA", Guid.NewGuid(), locator: "orbit.xlsx!C4") };

        var result = await Run(seed);

        var item = result.Items.Single();
        item.ProjectKey.Should().Be("ALPHA");
        item.Issue.Should().Be("DataQuality Amber"); // fixture summary = "{area} {severity}"
        item.Severity.Should().Be("Amber");
        item.CitationLocator.Should().Be("orbit.xlsx!C4");
    }

    [Fact]
    public async Task Items_are_ordered_worst_first_by_severity()
    {
        var run = Guid.NewGuid();
        var seed = new[]
        {
            Dq(Severity.Green, "P", run, locator: "g!r1"),
            Dq(Severity.Red, "P", run, locator: "r!r1"),
            Dq(Severity.Amber, "P", run, locator: "a!r1"),
        };

        var result = await Run(seed);

        result.Items.Select(i => i.Severity).Should().ContainInOrder("Red", "Amber", "Green");
    }

    [Fact]
    public async Task Counts_report_total_and_per_project()
    {
        // One run per project (latest-run resolution keeps a single run per key).
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        var seed = new[]
        {
            Dq(Severity.Amber, "A", runA, locator: "a!1"),
            Dq(Severity.Red, "A", runA, locator: "a!2"),
            Dq(Severity.Green, "A", runA, locator: "a!3"),
            Dq(Severity.Amber, "B", runB, locator: "b!1"),
            Dq(Severity.Red, "B", runB, locator: "b!2"),
        };

        var result = await Run(seed);

        result.TotalItems.Should().Be(5);
        result.PerProject.Should().HaveCount(2);
        result.PerProject.Single(p => p.ProjectKey == "A").Count.Should().Be(3);
        result.PerProject.Single(p => p.ProjectKey == "B").Count.Should().Be(2);
    }

    [Fact]
    public async Task Empty_portfolio_is_zeroed()
    {
        var result = await Run([]);

        result.Confidence.Mean.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
        result.PerProject.Should().BeEmpty();
    }
}
