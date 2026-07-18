using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class StatusAgentTests
{
    private static MilestoneRecord Milestone(
        string name, string? due, string? completed, string? dependsOn = null, string? status = null,
        string? baseline = null, bool isCritical = false) => new()
    {
        ProjectKey = "ALPHA",
        Name = name,
        DueDate = due is null ? null : DateTimeOffset.Parse(due),
        CompletedDate = completed is null ? null : DateTimeOffset.Parse(completed),
        Status = status,
        DependsOn = dependsOn,
        BaselineDate = baseline is null ? null : DateTimeOffset.Parse(baseline),
        IsCritical = isCritical,
        Source = new SourceRef($"Milestones!{name}"),
    };

    private static Task<IReadOnlyList<Finding>> Run(DataQualitySignal quality, params MilestoneRecord[] milestones)
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project()],
            milestones: milestones));
        return new StatusSkill().ExecuteAsync(new AnalysisInput(slice, quality), CancellationToken.None);
    }

    [Fact]
    public async Task Flags_a_milestone_completed_after_its_due_date()
    {
        var findings = await Run(DataQualitySignal.Clean(), Milestone("Design", "2026-05-01", "2026-06-10"));

        findings.Should().Contain(f => f.Summary.Contains("late", StringComparison.OrdinalIgnoreCase));
        findings.Should().OnlyContain(f => f.ProducingAgent == "Status" && f.Citation.Locator.Length > 0);
    }

    [Fact]
    public async Task Flags_an_incomplete_milestone_past_its_due_date_as_overdue()
    {
        var findings = await Run(DataQualitySignal.Clean(), Milestone("Beta", "2026-06-15", completed: null));

        findings.Should().Contain(f => f.Summary.Contains("overdue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Flags_dependency_risk_when_a_prerequisite_is_not_done()
    {
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Design", "2026-05-01", completed: null),
            Milestone("Beta", "2026-08-01", completed: null, dependsOn: "Design"));

        findings.Should().Contain(f => f.Summary.Contains("depend", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Confidence_comes_from_the_data_quality_signal_and_is_deterministic()
    {
        var stale = new DataQualitySignal { MissingFieldCount = 1, LastUpdateAgeDays = 10, SourceConsistent = true };

        var first = await Run(stale, Milestone("Beta", "2026-06-15", completed: null));
        var second = await Run(stale, Milestone("Beta", "2026-06-15", completed: null));

        first.Should().OnlyContain(f => f.Confidence == Confidence.Medium);
        first.Select(f => f.Summary).Should().BeEquivalentTo(second.Select(f => f.Summary));
    }

    [Fact]
    public async Task Every_finding_carries_the_schedule_area()
    {
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Design", "2026-05-01", "2026-06-10"),
            Milestone("Beta", "2026-06-15", completed: null));

        findings.Should().NotBeEmpty();
        findings.Should().OnlyContain(f => f.Area == HealthArea.Schedule && f.Severity != null);
    }

    [Fact]
    public async Task Major_schedule_variance_is_red()
    {
        // Completed ~40 days after the due date → "major" band → Red.
        var findings = await Run(DataQualitySignal.Clean(), Milestone("Design", "2026-05-01", "2026-06-10"));

        findings.Should().Contain(f => f.Summary.Contains("late", StringComparison.OrdinalIgnoreCase)
                                       && f.Severity == Severity.Red);
    }

    [Fact]
    public async Task Minor_schedule_variance_is_green()
    {
        // Completed 3 days after the due date → "minor" band → Green (a variance, but not alarming).
        var findings = await Run(DataQualitySignal.Clean(), Milestone("Design", "2026-05-01", "2026-05-04"));

        findings.Should().Contain(f => f.Summary.Contains("late", StringComparison.OrdinalIgnoreCase)
                                       && f.Severity == Severity.Green);
    }

    [Fact]
    public async Task A_missed_milestone_is_red_even_when_its_due_date_is_upcoming()
    {
        // Due 5 days out (inside the "due soon" window) but recorded as Missed — the old code rendered
        // this as a Green informational "due soon". A missed milestone must be Red and not lost in green.
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Cutover rehearsal", due: "2026-07-15", completed: null, status: "Missed"));

        findings.Should().Contain(f => f.Severity == Severity.Red);
        findings.Should().NotContain(f => f.Summary.Contains("due soon", StringComparison.OrdinalIgnoreCase)
                                          && f.Severity == Severity.Green);
    }

    [Fact]
    public async Task An_at_risk_milestone_is_amber()
    {
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Certification", due: "2026-07-20", completed: null, status: "At Risk"));

        findings.Should().Contain(f => f.Severity == Severity.Amber);
    }

    [Fact]
    public async Task An_upcoming_milestone_with_no_adverse_status_is_still_informational_green()
    {
        // Regression guard: a plain upcoming milestone keeps the existing Green "due soon" behaviour.
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Kickoff", due: "2026-07-18", completed: null));

        findings.Should().Contain(f => f.Summary.Contains("due soon", StringComparison.OrdinalIgnoreCase)
                                       && f.Severity == Severity.Green);
    }

    [Fact]
    public async Task A_milestone_due_within_four_weeks_is_upcoming()
    {
        // 2026-07-30 is 20 days out — outside the old 2-week window, inside the widened 4-week one (as-of 07-10).
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Pilot go-live", due: "2026-07-30", completed: null));

        findings.Should().Contain(f =>
            f.Summary.Contains("due soon", StringComparison.OrdinalIgnoreCase)
            && f.MetricDetail != null && f.MetricDetail["kind"] == "upcoming");
    }

    [Fact]
    public async Task Upcoming_finding_carries_structured_milestone_name_and_due_date()
    {
        // The L2 "Upcoming milestones" panel renders dated rows — so the finding carries milestone/dueDate/kind.
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Pilot go-live", due: "2026-07-18", completed: null));

        var upcoming = findings.Should()
            .ContainSingle(f => f.MetricDetail != null && f.MetricDetail.GetValueOrDefault("kind") == "upcoming").Which;
        upcoming.MetricDetail!["milestone"].Should().Be("Pilot go-live");
        upcoming.MetricDetail["dueDate"].Should().Be("2026-07-18");
    }

    [Fact]
    public async Task A_slipped_milestone_reports_the_slip_from_its_baseline()
    {
        // Baseline 2026-06-15, adjusted due 2026-07-30 → 45-day slip. Still upcoming (as-of 07-10),
        // so the slip is surfaced as info on the finding (magnitude is display-only in v0).
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Pilot go-live", due: "2026-07-30", completed: null, baseline: "2026-06-15"));

        var f = findings.Should().ContainSingle(x => x.MetricDetail != null && x.MetricDetail.ContainsKey("slipDays")).Which;
        f.MetricDetail!["slipDays"].Should().Be("45");
        f.MetricDetail["baselineDate"].Should().Be("2026-06-15");
        f.Summary.Should().Contain("slipped");
    }

    [Fact]
    public async Task A_critical_milestone_in_trouble_is_red_even_when_only_slightly_late()
    {
        // Overdue by only 5 days → normally the "minor" Green band; but a CRITICAL milestone in trouble
        // must escalate to Red (and it carries the critical flag for the panel badge).
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Cutover", due: "2026-07-05", completed: null, isCritical: true));

        var f = findings.Should().ContainSingle(x => x.Summary.Contains("overdue", StringComparison.OrdinalIgnoreCase)).Which;
        f.Severity.Should().Be(Severity.Red);
        f.MetricDetail!["critical"].Should().Be("true");
    }

    [Fact]
    public async Task A_non_critical_slightly_late_milestone_stays_green()
    {
        // Same 5-day overdue but NOT critical → keeps the minor Green band (regression guard for the elevation).
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Minor task", due: "2026-07-05", completed: null, isCritical: false));

        findings.Should().Contain(f => f.Summary.Contains("overdue", StringComparison.OrdinalIgnoreCase)
                                       && f.Severity == Severity.Green);
    }

    [Fact]
    public async Task An_overdue_milestone_is_marked_a_deviation_not_upcoming()
    {
        // Deviations (overdue/late) must NOT be tagged upcoming, so the view keeps them out of the
        // Upcoming-milestones panel and under Key deviations > Time.
        var findings = await Run(
            DataQualitySignal.Clean(),
            Milestone("Beta", due: "2026-06-15", completed: null));

        var overdue = findings.Should()
            .ContainSingle(f => f.Summary.Contains("overdue", StringComparison.OrdinalIgnoreCase)).Which;
        overdue.MetricDetail.Should().NotBeNull();
        overdue.MetricDetail!["kind"].Should().Be("overdue");
    }
}
