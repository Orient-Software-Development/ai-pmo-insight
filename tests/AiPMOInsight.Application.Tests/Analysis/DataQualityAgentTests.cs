using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class DataQualityAgentTests
{
    private static Task<DataQualityResult> Run(ProjectSlice slice) =>
        new DataQualitySkill().ExecuteAsync(slice, CancellationToken.None);

    [Fact]
    public async Task Clean_recent_consistent_data_yields_no_findings_and_high_confidence()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        // Clean data raises no problem findings (the always-present informational completeness grid aside).
        result.Findings.Where(f => f.MetricDetail?.GetValueOrDefault("kind") != "completeness-grid")
            .Should().BeEmpty();
        result.Signal.MissingFieldCount.Should().Be(0);
        result.Signal.SourceConsistent.Should().BeTrue();
    }

    [Fact]
    public async Task Missing_fields_are_flagged_and_counted()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(name: "  ", percentComplete: null, lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Signal.MissingFieldCount.Should().BeGreaterThan(0);
        result.Findings.Should().NotBeEmpty();
        result.Findings.Should().OnlyContain(f => f.Kind == FindingKind.Analysis && f.ProducingAgent == "DataQuality");
        result.Findings.Should().OnlyContain(f => f.Citation.Locator.Length > 0);
    }

    [Fact]
    public async Task Stale_last_update_is_flagged_and_reflected_in_the_signal()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-120))]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Signal.LastUpdateAgeDays.Should().BeGreaterThan(90);
        result.Findings.Should().Contain(f => f.Summary.Contains("stale", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Orphan_reference_marks_sources_inconsistent()
    {
        // A budget line references a project key that no Project row defines.
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))],
            budgetLines: [new BudgetLineRecord { ProjectKey = "GHOST", Category = "Dev", Budget = 1, Forecast = 1, Actual = 1, Source = AnalysisFixtures.Source }]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Signal.SourceConsistent.Should().BeFalse();
    }

    [Fact]
    public async Task Every_flag_carries_the_data_quality_area()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(name: "  ", percentComplete: null, lastUpdated: AnalysisFixtures.RunTime.AddDays(-120))]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().NotBeEmpty();
        result.Findings.Should().OnlyContain(f => f.Area == HealthArea.DataQuality && f.Severity != null);
    }

    [Fact]
    public async Task Missing_field_flag_is_amber()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(name: "  ", lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().Contain(f => f.Summary.Contains("name is missing", StringComparison.OrdinalIgnoreCase)
                                              && f.Severity == Severity.Amber);
    }

    [Fact]
    public async Task Every_flag_carries_a_suggested_remediation()
    {
        // L3 #2: a finite known check set → a deterministic remediation per finding (static rule-map, no LLM).
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(name: "  ", percentComplete: null, lastUpdated: AnalysisFixtures.RunTime.AddDays(-120))]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().NotBeEmpty();
        result.Findings.Should().OnlyContain(f =>
            f.MetricDetail != null && !string.IsNullOrWhiteSpace(f.MetricDetail!["remediation"]));
    }

    [Fact]
    public async Task Stale_finding_carries_the_age_as_a_structured_metric()
    {
        // L3 #8: the staleness age is a real metric (days), not only trapped in the summary string.
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-45))]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        var stale = result.Findings.Should().ContainSingle(f => f.Summary.Contains("stale", StringComparison.OrdinalIgnoreCase)).Which;
        stale.MetricValue.Should().Be(45m);
        stale.MetricUnit.Should().Be("days");
    }

    // ── L3 #4: duplicate-identity candidates (POC heuristic) ─────────────────────────────────

    private static ProjectRecord Proj(string key, string name, string? customer) => new()
    {
        Key = key,
        Name = name,
        PercentComplete = 40,
        LastUpdated = AnalysisFixtures.RunTime.AddDays(-3),
        Customer = customer,
        Source = AnalysisFixtures.Source,
    };

    private static AssignmentRecord Assign(string projectKey, string person) => new()
    {
        ProjectKey = projectKey,
        Person = person,
        Role = "Engineer",
        AllocationPercent = 30,
        CapacityPercent = 100,
        Source = AnalysisFixtures.Source,
    };

    private static bool IsDuplicateCandidate(Finding f, string candidate) =>
        f.MetricDetail is not null
        && f.MetricDetail.GetValueOrDefault("kind") == "duplicate-candidate"
        && f.MetricDetail.GetValueOrDefault("candidate") == candidate;

    [Fact]
    public async Task Flags_a_near_duplicate_project_as_a_candidate()
    {
        var data = AnalysisFixtures.Data(
            projects:
            [
                Proj("ORB-1", "Customer Data Migration", "Fjord Bank"),
                Proj("ORB-1a", "Customer Data Migration Phase 2", "Fjord Bank"),
            ],
            assignments: [Assign("ORB-1", "Anna Berg"), Assign("ORB-1a", "Anna Berg")]);
        var slice = AnalysisFixtures.Slice(projectKey: "ORB-1", data: data);

        var result = await Run(slice);

        result.Findings.Should().Contain(f => IsDuplicateCandidate(f, "ORB-1a") && f.Severity == Severity.Amber);
    }

    [Fact]
    public async Task Does_not_flag_dissimilar_projects_as_duplicates()
    {
        var data = AnalysisFixtures.Data(
            projects:
            [
                Proj("ORB-1", "Customer Data Migration", "Fjord Bank"),
                Proj("ORB-2", "Payments Compliance Upgrade", "Acme Corp"),
            ]);
        var slice = AnalysisFixtures.Slice(projectKey: "ORB-1", data: data);

        var result = await Run(slice);

        result.Findings.Should().NotContain(f =>
            f.MetricDetail != null && f.MetricDetail.GetValueOrDefault("kind") == "duplicate-candidate");
    }

    [Fact]
    public async Task Emits_the_duplicate_candidate_once_per_pair_from_the_canonical_first_key()
    {
        var data = AnalysisFixtures.Data(
            projects:
            [
                Proj("ORB-1", "Customer Data Migration", "Fjord Bank"),
                Proj("ORB-1a", "Customer Data Migration Phase 2", "Fjord Bank"),
            ],
            assignments: [Assign("ORB-1", "Anna Berg"), Assign("ORB-1a", "Anna Berg")]);

        // Running the agent on the SECOND key must not re-emit the same pair.
        var slice = AnalysisFixtures.Slice(projectKey: "ORB-1a", data: data);

        var result = await Run(slice);

        result.Findings.Should().NotContain(f =>
            f.MetricDetail != null && f.MetricDetail.GetValueOrDefault("kind") == "duplicate-candidate");
    }

    // ── L3 #7: areas-completeness grid ───────────────────────────────────────────────────────

    [Fact]
    public async Task Emits_an_areas_completeness_grid_over_the_eight_input_categories()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))],
            milestones:
            [
                new MilestoneRecord { ProjectKey = "ALPHA", Name = "M1", DueDate = AnalysisFixtures.RunTime, Source = AnalysisFixtures.Source },
            ]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        var grid = result.Findings
            .Should().ContainSingle(f => f.MetricDetail != null && f.MetricDetail.GetValueOrDefault("kind") == "completeness-grid").Which;
        grid.Severity.Should().Be(Severity.Green); // informational, not a gap
        grid.MetricDetail!.Should().ContainKeys("Schedule", "Budget", "Scope", "Resources", "Risks", "Decisions", "Minutes", "Time");
        grid.MetricDetail["Schedule"].Should().Be("100"); // 1 milestone with name + due date → 100%
        grid.MetricDetail["Budget"].Should().Be("n/a");    // no budget records
        grid.MetricDetail["Time"].Should().Be("n/a");      // no time-entries source yet
    }

    // ── L3 #6: budget-actuals-missing completeness ───────────────────────────────────────────

    private static BudgetLineRecord Budget(string category, decimal? actual) => new()
    {
        ProjectKey = "ALPHA",
        Category = category,
        Budget = 100m,
        Forecast = 110m,
        Actual = actual,
        Source = AnalysisFixtures.Source,
    };

    [Fact]
    public async Task Budget_line_missing_actuals_is_flagged()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))],
            budgetLines: [Budget("Development", actual: null)]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().Contain(f =>
            f.Summary.Contains("missing actuals", StringComparison.OrdinalIgnoreCase) && f.Severity == Severity.Amber);
    }

    [Fact]
    public async Task Budget_line_with_actuals_is_not_flagged_missing()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))],
            budgetLines: [Budget("Development", actual: 60m)]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().NotContain(f => f.Summary.Contains("missing actuals", StringComparison.OrdinalIgnoreCase));
    }

    // ── L3 #1: per-risk/issue staleness (POC N=21 days, EXAMPLE) ─────────────────────────────

    private static RaidItemRecord Raid(string description, int lastUpdatedDaysAgo) => new()
    {
        ProjectKey = "ALPHA",
        Type = RaidType.Risk,
        Description = description,
        LastUpdated = AnalysisFixtures.RunTime.AddDays(-lastUpdatedDaysAgo),
        Source = AnalysisFixtures.Source,
    };

    [Fact]
    public async Task A_stale_raid_item_is_flagged_with_its_age()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))],
            raidItems: [Raid("Vendor API may slip", lastUpdatedDaysAgo: 30)]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().Contain(f =>
            f.Summary.Contains("not been updated", StringComparison.OrdinalIgnoreCase)
            && f.MetricValue == 30m && f.MetricUnit == "days" && f.Severity == Severity.Amber);
    }

    [Fact]
    public async Task A_recently_updated_raid_item_is_not_flagged_stale()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))],
            raidItems: [Raid("Vendor API may slip", lastUpdatedDaysAgo: 5)]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().NotContain(f => f.Summary.Contains("not been updated", StringComparison.OrdinalIgnoreCase));
    }

    // ── L3 #3: resource-plan vs time-entries consistency (POC) ───────────────────────────────

    private static AssignmentRecord Alloc(string person, double pct) => new()
    {
        ProjectKey = "ALPHA", Person = person, Role = "Engineer",
        AllocationPercent = pct, CapacityPercent = 100, Source = AnalysisFixtures.Source,
    };

    private static TimeEntryRecord Time(string person, double hours) => new()
    {
        ProjectKey = "ALPHA", Person = person, HoursLogged = hours, Source = AnalysisFixtures.Source,
    };

    [Fact]
    public async Task Allocation_without_time_and_time_without_allocation_are_both_flagged()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))],
            assignments: [Alloc("Anna Berg", 50)],
            timeEntries: [Time("Sven Aalto", 40)]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().Contain(f => f.Summary.Contains("Anna Berg") && f.Summary.Contains("logged no time", StringComparison.OrdinalIgnoreCase));
        result.Findings.Should().Contain(f => f.Summary.Contains("Sven Aalto") && f.Summary.Contains("not on the resource plan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Allocation_matched_by_time_is_not_flagged()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))],
            assignments: [Alloc("Anna Berg", 50)],
            timeEntries: [Time("Anna Berg", 40)]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().NotContain(f => f.Summary.Contains("logged no time", StringComparison.OrdinalIgnoreCase));
        result.Findings.Should().NotContain(f => f.Summary.Contains("not on the resource plan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Inconsistent_source_flag_is_red()
    {
        var data = AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(lastUpdated: AnalysisFixtures.RunTime.AddDays(-3))],
            budgetLines: [new BudgetLineRecord { ProjectKey = "GHOST", Category = "Dev", Budget = 1, Forecast = 1, Actual = 1, Source = AnalysisFixtures.Source }]);
        var slice = AnalysisFixtures.Slice(data: data);

        var result = await Run(slice);

        result.Findings.Should().Contain(f => f.Summary.Contains("inconsistent", StringComparison.OrdinalIgnoreCase)
                                              && f.Severity == Severity.Red);
    }
}
