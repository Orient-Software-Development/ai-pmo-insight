using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.ExecutivePortfolio;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Domain.Findings;
using Xunit;
using static AiPMOInsight.Application.Tests.HealthScoring.HealthScoringFixtures;

namespace AiPMOInsight.Application.Tests.ExecutivePortfolio;

/// <summary>
/// Portfolio roll-up (add-executive-portfolio-dashboard). Fans out over the existing pure
/// <see cref="HealthScoringService"/> and aggregates: G/A/R counts, needs-PM-review count, aggregate
/// confidence, and a worst-first intervention list with a cited reason per entry. With the fixture
/// options (Green≥80, Amber≥60; Green=100/Amber=70/Red=30; Budget/Risk overrides floor to Red):
///   • one Green area finding      → Green
///   • one Amber area finding      → Amber (no override)
///   • one Red Budget/Risk finding → Red (raw + override floor)
/// </summary>
public class ScorePortfolioTests
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

    private static Task<ScorePortfolio.Result> Run(IReadOnlyList<Finding> seed) =>
        new ScorePortfolio.Handler(new MultiRepo(seed), new HealthScoringService(Options()))
            .Handle(new ScorePortfolio.Query(), CancellationToken.None);

    private static Finding Area(HealthArea area, Severity sev, string project,
        Confidence confidence = Confidence.High, string locator = "sheet!row2") =>
        AnalysisFinding(area, sev, Guid.NewGuid(), confidence: confidence, projectKey: project, locator: locator);

    // ── §2 aggregation ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rag_counts_reflect_each_projects_final_bucket()
    {
        var seed = new[]
        {
            Area(HealthArea.Risk, Severity.Red, "RED1"),
            Area(HealthArea.Budget, Severity.Red, "RED2"),
            Area(HealthArea.Resource, Severity.Amber, "AMB1"),
            Area(HealthArea.Schedule, Severity.Green, "GRN1"),
            Area(HealthArea.Schedule, Severity.Green, "GRN2"),
            Area(HealthArea.Schedule, Severity.Green, "GRN3"),
        };

        var result = await Run(seed);

        result.Red.Should().Be(2);
        result.Amber.Should().Be(1);
        result.Green.Should().Be(3);
    }

    [Fact]
    public async Task Needs_pm_review_is_counted_independently_of_colour()
    {
        var seed = new[]
        {
            // Green colour but Low confidence → NeedsPmReview.
            Area(HealthArea.Schedule, Severity.Green, "GRN_LOWCONF", confidence: Confidence.Low),
            Area(HealthArea.Resource, Severity.Amber, "AMB_LOWCONF", confidence: Confidence.Low),
            Area(HealthArea.Schedule, Severity.Green, "GRN_OK", confidence: Confidence.High),
        };

        var result = await Run(seed);

        result.NeedsPmReview.Should().Be(2);
    }

    [Fact]
    public async Task Aggregate_confidence_is_the_mean_over_scored_projects()
    {
        // High=100, Low=30 → mean of {100, 30} = 65.
        var seed = new[]
        {
            Area(HealthArea.Schedule, Severity.Green, "A", confidence: Confidence.High),
            Area(HealthArea.Schedule, Severity.Green, "B", confidence: Confidence.Low),
        };

        var result = await Run(seed);

        result.AverageConfidence.Should().BeApproximately(65d, 0.001);
    }

    [Fact]
    public async Task Empty_portfolio_is_all_zero()
    {
        var result = await Run([]);

        result.Red.Should().Be(0);
        result.Amber.Should().Be(0);
        result.Green.Should().Be(0);
        result.NeedsPmReview.Should().Be(0);
        result.AverageConfidence.Should().Be(0);
        result.Intervention.Should().BeEmpty();
    }

    [Fact]
    public async Task Unscoreable_project_does_not_distort_counts()
    {
        var narrative = Finding.Create(
            "NOSCORE", "prose", Citation.Create(Guid.NewGuid(), "loc"), T0, Guid.NewGuid(), "Narrative",
            FindingKind.Narrative, Confidence.Medium, promptVersion: "sha256:x");
        var seed = new[]
        {
            narrative,
            Area(HealthArea.Schedule, Severity.Green, "GRN"),
        };

        var result = await Run(seed);

        result.Green.Should().Be(1);
        result.Red.Should().Be(0);
        result.Amber.Should().Be(0);
        result.Intervention.Should().NotContain(i => i.ProjectKey == "NOSCORE");
    }

    // ── §3 intervention list ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Intervention_ranks_red_before_amber_and_excludes_green()
    {
        var seed = new[]
        {
            Area(HealthArea.Resource, Severity.Amber, "AMBER"),
            Area(HealthArea.Risk, Severity.Red, "RED"),
            Area(HealthArea.Schedule, Severity.Green, "GREEN"),
        };

        var result = await Run(seed);

        result.Intervention.Select(i => i.ProjectKey).Should().ContainInOrder("RED", "AMBER");
        result.Intervention.Should().NotContain(i => i.ProjectKey == "GREEN");
    }

    [Fact]
    public async Task Override_driven_entry_carries_the_override_reason_and_citation()
    {
        var seed = new[] { Area(HealthArea.Budget, Severity.Red, "REDBUD", locator: "budget.xlsx!B7") };

        var result = await Run(seed);

        var entry = result.Intervention.Single();
        entry.Status.Should().Be("Red");
        entry.Reason.Should().Contain("forecast-overrun-critical");
        entry.CitationLocator.Should().Be("budget.xlsx!B7");
    }

    [Fact]
    public async Task Raw_score_driven_entry_still_carries_a_cited_reason()
    {
        // Amber Resource: no override fires, but the entry must still name the area and cite a finding.
        var seed = new[] { Area(HealthArea.Resource, Severity.Amber, "AMB", locator: "resource.xlsx!R3") };

        var result = await Run(seed);

        var entry = result.Intervention.Single();
        entry.Status.Should().Be("Amber");
        entry.Reason.Should().Contain("Resource");
        entry.CitationLocator.Should().Be("resource.xlsx!R3");
    }
}
