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

    // Build a scoreable analysis finding for a project on a shared run, optionally carrying a metric.
    private static Finding AreaOnRun(HealthArea area, Severity sev, string project, Guid runId,
        decimal? metricValue = null, string? metricUnit = null, IReadOnlyDictionary<string, string>? detail = null,
        string agent = "Agent", string locator = "sheet!row2") =>
        Finding.Create(project, $"{area} {sev}", Citation.Create(Guid.NewGuid(), locator), T0, runId, agent,
            FindingKind.Analysis, Confidence.High, area: area, severity: sev,
            metricValue: metricValue, metricUnit: metricUnit, metricDetail: detail);

    // A narrative finding on a shared run carrying the project's customer (the read-side customer channel).
    private static Finding NarrativeWithCustomer(string project, Guid runId, string customer) =>
        Finding.Create(project, "[amber] narrative", Citation.Create(Guid.NewGuid(), "synthesis:narrative"), T0,
            runId, "Narrative", FindingKind.Narrative, Confidence.Medium, promptVersion: "sha256:x",
            metricDetail: new Dictionary<string, string> { ["customer"] = customer });

    // ── §4 additional backed L1 signals (add-l1-portfolio-signals slices D/E) ─────────────────

    [Fact]
    public async Task Financial_exposure_sums_the_metric_across_projects()
    {
        var seed = new[]
        {
            AreaOnRun(HealthArea.Budget, Severity.Amber, "P1", Guid.NewGuid(), metricValue: 80000m, metricUnit: "EUR", agent: "Financial"),
            AreaOnRun(HealthArea.Budget, Severity.Amber, "P2", Guid.NewGuid(), metricValue: 20000m, metricUnit: "EUR", agent: "Financial"),
        };

        var result = await Run(seed);

        result.FinancialExposure.Amount.Should().Be(100000m);
        result.FinancialExposure.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task Financial_exposure_is_zero_when_no_finding_carries_an_amount()
    {
        var result = await Run(new[] { Area(HealthArea.Schedule, Severity.Green, "P1") });

        result.FinancialExposure.Amount.Should().Be(0m);
    }

    [Fact]
    public async Task Decision_backlog_counts_decision_findings()
    {
        var seed = new[]
        {
            AreaOnRun(HealthArea.Decision, Severity.Red, "P1", Guid.NewGuid(), agent: "Decision"),
            AreaOnRun(HealthArea.Decision, Severity.Amber, "P2", Guid.NewGuid(), agent: "Decision"),
            Area(HealthArea.Schedule, Severity.Green, "P3"),
        };

        var result = await Run(seed);

        result.DecisionBacklog.Should().Be(2);
    }

    [Fact]
    public async Task Key_person_concentration_is_distinct_by_person()
    {
        // "Anna" concentration emitted on two of her projects → one distinct key-person entry.
        var person = new Dictionary<string, string> { ["person"] = "Anna" };
        var seed = new[]
        {
            AreaOnRun(HealthArea.Resource, Severity.Red, "P1", Guid.NewGuid(), metricValue: 5m, detail: person, agent: "Resource"),
            AreaOnRun(HealthArea.Resource, Severity.Red, "P2", Guid.NewGuid(), metricValue: 5m, detail: person, agent: "Resource"),
        };

        var result = await Run(seed);

        result.KeyPersons.Should().ContainSingle(k => k.Person == "Anna" && k.ProjectCount == 5 && k.Status == "Red");
    }

    [Fact]
    public async Task Customer_exposure_groups_at_risk_projects_by_customer()
    {
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        var seed = new[]
        {
            // Two Red projects for the same customer (each: an area finding + a narrative carrying the customer).
            AreaOnRun(HealthArea.Risk, Severity.Red, "P1", runA),
            NarrativeWithCustomer("P1", runA, "Fjord Bank"),
            AreaOnRun(HealthArea.Risk, Severity.Red, "P2", runB),
            NarrativeWithCustomer("P2", runB, "Fjord Bank"),
        };

        var result = await Run(seed);

        result.CustomerExposure.Should().ContainSingle(c => c.Customer == "Fjord Bank" && c.AtRiskCount == 2);
    }

    [Fact]
    public async Task New_fields_are_empty_on_an_empty_portfolio()
    {
        var result = await Run([]);

        result.FinancialExposure.Amount.Should().Be(0m);
        result.DecisionBacklog.Should().Be(0);
        result.KeyPersons.Should().BeEmpty();
        result.CustomerExposure.Should().BeEmpty();
    }

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
