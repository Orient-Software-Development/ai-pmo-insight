using AwesomeAssertions;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Domain.Findings;
using Xunit;
using static AiPMOInsight.Application.Tests.HealthScoring.HealthScoringFixtures;

namespace AiPMOInsight.Application.Tests.HealthScoring;

public class HealthScoringOverrideTests
{
    private static readonly Guid Run = Guid.NewGuid();

    /// <summary>The four non-schedule areas, all healthy (Green) — diluting filler for override tests.</summary>
    private static Finding[] GreenFillers() =>
    [
        AnalysisFinding(HealthArea.Budget, Severity.Green, Run),
        AnalysisFinding(HealthArea.Risk, Severity.Green, Run),
        AnalysisFinding(HealthArea.Resource, Severity.Green, Run),
        AnalysisFinding(HealthArea.DataQuality, Severity.Green, Run),
    ];

    [Fact]
    public void Override_raises_severity_above_the_raw_bucket()
    {
        // Raw is Green (one Red schedule area diluted by four Green areas → 86), but the Red schedule
        // finding trips "critical-milestone-missed → minimum Amber".
        var schedule = AnalysisFinding(HealthArea.Schedule, Severity.Red, Run);
        var findings = GreenFillers().Append(schedule).ToArray();

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        score.RawBucket.Should().Be(Severity.Green);
        score.FinalBucket.Should().Be(Severity.Amber);
        score.AppliedOverrides.Should().Contain(o => o.RuleId == "critical-milestone-missed");
    }

    [Fact]
    public void Overdue_key_decision_floors_the_bucket_to_amber()
    {
        // Raw is Green (all filler areas Green); a Red Decision finding trips "key-decision-overdue →
        // minimum Amber". The floor fires regardless of the Decision area's weight.
        var decision = AnalysisFinding(HealthArea.Decision, Severity.Red, Run, locator: "Decisions!D-1002-1");
        var findings = GreenFillers().Append(decision).ToArray();

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        score.RawBucket.Should().Be(Severity.Green);
        score.FinalBucket.Should().Be(Severity.Amber);
        score.AppliedOverrides.Should().Contain(o => o.RuleId == "key-decision-overdue");
    }

    [Fact]
    public void Worst_case_floor_wins_when_overrides_collide()
    {
        // Schedule Red (→ min Amber) AND Budget Red (→ min Red) both fire; the Red floor wins.
        var findings = new[]
        {
            AnalysisFinding(HealthArea.Schedule, Severity.Red, Run),
            AnalysisFinding(HealthArea.Budget, Severity.Red, Run),
            AnalysisFinding(HealthArea.Risk, Severity.Green, Run),
            AnalysisFinding(HealthArea.Resource, Severity.Green, Run),
            AnalysisFinding(HealthArea.DataQuality, Severity.Green, Run),
        };

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        score.AppliedOverrides.Should().HaveCountGreaterThanOrEqualTo(2);
        score.FinalBucket.Should().Be(Severity.Red);
    }

    [Fact]
    public void A_floor_never_lowers_severity()
    {
        // Only a Red schedule finding → raw is Red (30). The "minimum Amber" floor must not improve it.
        var findings = new[] { AnalysisFinding(HealthArea.Schedule, Severity.Red, Run) };

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        score.RawBucket.Should().Be(Severity.Red);
        score.FinalBucket.Should().Be(Severity.Red);
    }

    [Fact]
    public void An_absent_signal_does_not_fire_the_override()
    {
        // Schedule Amber (not Red) → the "critical-milestone-missed" rule (requires Red) must not fire,
        // and no synthetic warning is produced.
        var findings = new[] { AnalysisFinding(HealthArea.Schedule, Severity.Amber, Run) };

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        score.AppliedOverrides.Should().BeEmpty();
    }

    [Fact]
    public void Low_aggregate_confidence_flags_needs_pm_review_distinct_from_the_colour()
    {
        // All findings Low confidence (30) < floor (50) → Needs PM Review, alongside a RAG colour.
        var findings = new[]
        {
            AnalysisFinding(HealthArea.Schedule, Severity.Amber, Run, confidence: Confidence.Low),
            AnalysisFinding(HealthArea.Budget, Severity.Green, Run, confidence: Confidence.Low),
        };

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        score.NeedsPmReview.Should().BeTrue();
        score.FinalBucket.Should().BeOneOf(Severity.Green, Severity.Amber, Severity.Red); // colour still present
    }

    [Fact]
    public void Sufficient_confidence_does_not_flag_review()
    {
        var findings = new[]
        {
            AnalysisFinding(HealthArea.Schedule, Severity.Amber, Run, confidence: Confidence.High),
        };

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        score.NeedsPmReview.Should().BeFalse();
    }

    [Fact]
    public void Result_is_auditable_and_shows_pre_and_post_override_buckets()
    {
        var schedule = AnalysisFinding(HealthArea.Schedule, Severity.Red, Run, locator: "Milestones!Design");
        var findings = GreenFillers().Append(schedule).ToArray();

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        // Pre- and post-override buckets both visible.
        score.RawBucket.Should().Be(Severity.Green);
        score.FinalBucket.Should().Be(Severity.Amber);

        // Ordered applied overrides naming the rule and the tripping finding + citation.
        var applied = score.AppliedOverrides.Single(o => o.RuleId == "critical-milestone-missed");
        applied.Floor.Should().Be(Severity.Amber);
        applied.FindingId.Should().Be(schedule.Id);
        applied.CitationLocator.Should().Be("Milestones!Design");
        applied.Reason.Should().NotBeNullOrWhiteSpace();

        // Aggregate confidence and a per-area breakdown across all five areas.
        score.Confidence.Should().BeGreaterThan(0);
        score.Areas.Should().HaveCount(5);
        score.Areas.Should().Contain(a => a.Area == HealthArea.Schedule && a.Severity == Severity.Red);
    }
}
