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
    public async Task Item_surfaces_age_and_remediation_from_the_finding_metric()
    {
        // L3 #8 (Age) + #2 (Remediation): a stale finding carries the age (days) as a metric and a
        // suggested remediation on MetricDetail — the slice exposes both on the item.
        var stale = Finding.Create(
            "ALPHA", "Project data is stale (last updated 45 days ago).",
            Citation.Create(Guid.NewGuid(), "Projects!row2"), T0, Guid.NewGuid(), "DataQuality",
            FindingKind.Analysis, Confidence.High, area: HealthArea.DataQuality, severity: Severity.Amber,
            metricValue: 45m, metricUnit: "days",
            metricDetail: new Dictionary<string, string> { ["remediation"] = "Refresh the project data." });

        var result = await Run([stale]);

        var item = result.Items.Single();
        item.AgeDays.Should().Be(45);
        item.Remediation.Should().Be("Refresh the project data.");
    }

    [Fact]
    public async Task Duplicate_candidates_go_to_the_duplicates_list_not_the_items_list()
    {
        // L3 #4: a duplicate-candidate finding is surfaced in its own list (for Merge/Keep-separate),
        // not mixed into the missing/inconsistent items.
        var dup = Finding.Create(
            "ORB-1", "Possible duplicate of 'ORB-1a' — similarity 80%.",
            Citation.Create(Guid.NewGuid(), "Projects!row2"), T0, Guid.NewGuid(), "DataQuality",
            FindingKind.Analysis, Confidence.Medium, area: HealthArea.DataQuality, severity: Severity.Amber,
            metricValue: 80m, metricUnit: "percent",
            metricDetail: new Dictionary<string, string>
            {
                ["kind"] = "duplicate-candidate",
                ["candidate"] = "ORB-1a",
                ["candidateName"] = "Customer Data Migration Phase 2",
                ["score"] = "80",
            });

        var result = await Run([dup]);

        result.Items.Should().BeEmpty(); // excluded from the missing/inconsistent list
        var d = result.Duplicates.Should().ContainSingle().Which;
        d.ProjectKey.Should().Be("ORB-1");
        d.Candidate.Should().Be("ORB-1a");
        d.CandidateName.Should().Be("Customer Data Migration Phase 2");
        d.Score.Should().Be(80);
    }

    private static Finding DqTagged(Severity sev, string project, Guid run, string kind, string locator) =>
        Finding.Create(project, $"{kind} issue", Citation.Create(Guid.NewGuid(), locator), T0, run, "DataQuality",
            FindingKind.Analysis, Confidence.High, area: HealthArea.DataQuality, severity: sev,
            metricDetail: new Dictionary<string, string> { ["signalKind"] = kind });

    [Fact]
    public async Task Items_are_ordered_by_confidence_lift_ahead_of_severity()
    {
        // L3 #5: fixing the inconsistency lifts confidence (Medium→High); the Red item has no signal
        // impact (zero lift). So the lower-severity orphan ranks FIRST — lift beats severity.
        var run = Guid.NewGuid();
        var orphan = DqTagged(Severity.Green, "P", run, kind: "orphan", locator: "orphan!r1");
        var noise = DqTagged(Severity.Red, "P", run, kind: "none", locator: "noise!r1");

        var result = await Run([orphan, noise]);

        result.Items.First().CitationLocator.Should().Be("orphan!r1");
    }

    [Fact]
    public async Task Items_are_ordered_by_lift_globally_across_projects()
    {
        // The doc flags portfolio-wide lift ranking as needing "a small aggregation decision"
        // (docs/l3-data-quality-followups.md); the decision made in SummarizeDataQuality is a flat
        // global ranking by raw lift, not grouped/normalised per project first. Prove it here with two
        // projects: a very-stale finding (fixing it jumps Low→High, lift 2) must outrank a single
        // missing-field finding in a different project (fixing it jumps Medium→High, lift 1) — even
        // though the lower-lift item has the worse (Red) severity, which would win any severity-first
        // ordering.
        var bigLift = Finding.Create(
            "BIGLIFT", "Project data is stale (last updated 120 days ago).",
            Citation.Create(Guid.NewGuid(), "big!r1"), T0, Guid.NewGuid(), "DataQuality",
            FindingKind.Analysis, Confidence.High, area: HealthArea.DataQuality, severity: Severity.Green,
            metricValue: 120m, metricUnit: "days",
            metricDetail: new Dictionary<string, string> { ["signalKind"] = "stale" });

        var smallLift = Finding.Create(
            "SMALLLIFT", "Project percent-complete is missing.",
            Citation.Create(Guid.NewGuid(), "small!r1"), T0, Guid.NewGuid(), "DataQuality",
            FindingKind.Analysis, Confidence.High, area: HealthArea.DataQuality, severity: Severity.Red,
            metricDetail: new Dictionary<string, string> { ["signalKind"] = "missing" });

        var result = await Run([bigLift, smallLift]);

        result.Items.Select(i => i.CitationLocator).Should().ContainInOrder("big!r1", "small!r1");
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
    public async Task Completeness_grid_is_surfaced_per_project_and_kept_off_the_items_list()
    {
        var grid = Finding.Create(
            "ALPHA", "Areas-completeness grid.", Citation.Create(Guid.NewGuid(), "completeness:ALPHA"),
            T0, Guid.NewGuid(), "DataQuality", FindingKind.Analysis, Confidence.High,
            area: HealthArea.DataQuality, severity: Severity.Green,
            metricDetail: new Dictionary<string, string>
            {
                ["kind"] = "completeness-grid", ["remediation"] = "fix", ["Schedule"] = "80", ["Time"] = "n/a",
            });

        var result = await Run([grid]);

        result.Items.Should().BeEmpty(); // the grid is not a missing/inconsistent item
        var c = result.Completeness.Should().ContainSingle().Which;
        c.ProjectKey.Should().Be("ALPHA");
        c.Categories["Schedule"].Should().Be("80");
        c.Categories.Should().NotContainKey("kind"); // marker keys stripped
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
