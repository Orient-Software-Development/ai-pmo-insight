using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class StatusAgentTests
{
    private static MilestoneRecord Milestone(string name, string? due, string? completed, string? dependsOn = null) => new()
    {
        ProjectKey = "ALPHA",
        Name = name,
        DueDate = due is null ? null : DateTimeOffset.Parse(due),
        CompletedDate = completed is null ? null : DateTimeOffset.Parse(completed),
        DependsOn = dependsOn,
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
}
