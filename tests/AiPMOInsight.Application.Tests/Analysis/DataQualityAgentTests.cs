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

        result.Findings.Should().BeEmpty();
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
