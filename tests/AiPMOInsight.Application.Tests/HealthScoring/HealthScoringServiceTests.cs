using AwesomeAssertions;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Domain.Findings;
using Xunit;
using static AiPMOInsight.Application.Tests.HealthScoring.HealthScoringFixtures;

namespace AiPMOInsight.Application.Tests.HealthScoring;

public class HealthScoringServiceTests
{
    [Fact]
    public void Weighted_score_combines_area_severities()
    {
        var run = Guid.NewGuid();
        var findings = new[]
        {
            AnalysisFinding(HealthArea.Schedule, Severity.Amber, run),
            AnalysisFinding(HealthArea.Budget, Severity.Red, run),
        };

        var score = new HealthScoringService(Options()).Score("ALPHA", findings);

        // Weight-normalised over the two present areas: (70*20 + 30*30) / (20+30) = 2300/50 = 46.
        score.Should().NotBeNull();
        score!.RawScore.Should().Be(46d);
        score.RawBucket.Should().Be(Severity.Red); // 46 < Amber(60)
        score.Areas.Should().HaveCount(2);
        score.Areas.Sum(a => a.Contribution).Should().BeApproximately(score.RawScore, 0.0001);
    }

    [Fact]
    public void Score_buckets_by_configured_thresholds()
    {
        var run = Guid.NewGuid();
        // All areas Green → 100 → Green.
        var findings = new[] { AnalysisFinding(HealthArea.Schedule, Severity.Green, run) };

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        score.RawScore.Should().Be(100d);
        score.RawBucket.Should().Be(Severity.Green);
    }

    [Fact]
    public void Boundary_score_lands_in_the_configured_band()
    {
        var options = Options();
        options.Weights["Schedule"] = 30;
        options.Weights["Budget"] = 40;
        options.Weights["Risk"] = 30; // keep the total at 100 for validation parity
        options.Weights["Resource"] = 0;
        options.Weights["DataQuality"] = 0;

        var run = Guid.NewGuid();
        var findings = new[]
        {
            AnalysisFinding(HealthArea.Schedule, Severity.Green, run), // 100 * 30
            AnalysisFinding(HealthArea.Budget, Severity.Red, run),     //  30 * 40
        };

        var score = new HealthScoringService(options).Score("ALPHA", findings)!;

        // (100*30 + 30*40) / (30+40) = 4200/70 = 60 → exactly the Amber lower bound → Amber, not Red.
        score.RawScore.Should().Be(60d);
        score.RawBucket.Should().Be(Severity.Amber);
    }

    [Fact]
    public void Only_the_newest_run_contributes()
    {
        var oldRun = Guid.NewGuid();
        var newRun = Guid.NewGuid();
        var findings = new[]
        {
            AnalysisFinding(HealthArea.Schedule, Severity.Red, oldRun, createdAt: T0),          // stale — excluded
            AnalysisFinding(HealthArea.Schedule, Severity.Green, newRun, createdAt: T0.AddHours(1)),
        };

        var score = new HealthScoringService(Options()).Score("ALPHA", findings)!;

        score.RunId.Should().Be(newRun);
        score.Areas.Should().ContainSingle();
        score.Areas[0].Severity.Should().Be(Severity.Green); // newer run's severity, not the old Red
    }

    [Fact]
    public void Scoring_is_deterministic()
    {
        var run = Guid.NewGuid();
        var findings = new[]
        {
            AnalysisFinding(HealthArea.Schedule, Severity.Amber, run),
            AnalysisFinding(HealthArea.Budget, Severity.Red, run),
        };
        var service = new HealthScoringService(Options());

        var first = service.Score("ALPHA", findings)!;
        var second = service.Score("ALPHA", findings)!;

        first.RawScore.Should().Be(second.RawScore);
        first.RawBucket.Should().Be(second.RawBucket);
        first.FinalBucket.Should().Be(second.FinalBucket);
    }

    [Fact]
    public void No_findings_yields_no_score()
    {
        var score = new HealthScoringService(Options()).Score("ALPHA", []);

        score.Should().BeNull();
    }

    [Fact]
    public void Findings_without_a_scoreable_analysis_finding_yield_no_score()
    {
        var run = Guid.NewGuid();
        // A narrative-only run: Finding.Create nulls area/severity for non-analysis kinds.
        var narrative = Finding.Create(
            "ALPHA", "prose", Citation.Create(Guid.NewGuid(), "loc"), T0, run, "Narrative",
            FindingKind.Narrative, Confidence.Medium, promptVersion: "sha256:x");

        var score = new HealthScoringService(Options()).Score("ALPHA", [narrative]);

        score.Should().BeNull();
    }
}
