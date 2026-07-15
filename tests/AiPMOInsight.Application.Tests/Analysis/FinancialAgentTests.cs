using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class FinancialAgentTests
{
    private static BudgetLineRecord Budget(decimal budget, decimal forecast, decimal actual) => new()
    {
        ProjectKey = "ALPHA",
        Category = "Development",
        Budget = budget,
        Forecast = forecast,
        Actual = actual,
        Source = new SourceRef("Budget!row2"),
    };

    private static Task<IReadOnlyList<Finding>> Run(double? percentComplete, params BudgetLineRecord[] lines)
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project(percentComplete: percentComplete)],
            budgetLines: lines));
        return new FinancialSkill().ExecuteAsync(new AnalysisInput(slice, DataQualitySignal.Clean()), CancellationToken.None);
    }

    [Fact]
    public async Task Flags_a_forecast_that_exceeds_budget()
    {
        var findings = await Run(45, Budget(budget: 100000, forecast: 118000, actual: 60000));

        findings.Should().Contain(f => f.Summary.Contains("forecast", StringComparison.OrdinalIgnoreCase)
                                       && f.Summary.Contains("18"));
        findings.Should().OnlyContain(f => f.ProducingAgent == "Financial" && f.Citation.Locator.Length > 0);
        // Confidence is set deterministically from the DQ signal (clean data → High).
        findings.Should().OnlyContain(f => f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task Flags_spending_running_ahead_of_progress()
    {
        // 60% of budget spent but only 45% complete.
        var findings = await Run(45, Budget(budget: 100000, forecast: 100000, actual: 60000));

        findings.Should().Contain(f => f.Summary.Contains("progress", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task On_budget_and_on_track_produces_no_findings()
    {
        var findings = await Run(60, Budget(budget: 100000, forecast: 100000, actual: 60000));

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Every_finding_carries_the_budget_area()
    {
        var findings = await Run(45, Budget(budget: 100000, forecast: 118000, actual: 60000));

        findings.Should().NotBeEmpty();
        findings.Should().OnlyContain(f => f.Area == HealthArea.Budget && f.Severity != null);
    }

    [Fact]
    public async Task Overrun_above_fifteen_percent_is_red()
    {
        // 18% over budget → beyond the critical band → Red.
        var findings = await Run(45, Budget(budget: 100000, forecast: 118000, actual: 60000));

        findings.Should().Contain(f => f.Summary.Contains("forecast", StringComparison.OrdinalIgnoreCase)
                                       && f.Severity == Severity.Red);
    }

    [Fact]
    public async Task Overrun_within_fifteen_percent_is_amber()
    {
        // 10% over budget → within the critical band → Amber.
        var findings = await Run(45, Budget(budget: 100000, forecast: 110000, actual: 60000));

        findings.Should().Contain(f => f.Summary.Contains("forecast", StringComparison.OrdinalIgnoreCase)
                                       && f.Severity == Severity.Amber);
    }
}
