using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class ResourceAgentTests
{
    private static AssignmentRecord Assign(string person, string role, double allocation, double capacity = 100, bool onLeave = false) => new()
    {
        ProjectKey = "ALPHA",
        Person = person,
        Role = role,
        AllocationPercent = allocation,
        CapacityPercent = capacity,
        OnLeave = onLeave,
        Source = new SourceRef($"Resources!{person}"),
    };

    private static Task<IReadOnlyList<Finding>> Run(params AssignmentRecord[] assignments)
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project()],
            assignments: assignments));
        return new ResourceSkill().ExecuteAsync(new AnalysisInput(slice, DataQualitySignal.Clean()), CancellationToken.None);
    }

    [Fact]
    public async Task Flags_over_allocation_beyond_capacity()
    {
        var findings = await Run(Assign("Sam", "Project Manager", allocation: 120, capacity: 100));

        findings.Should().Contain(f => f.Summary.Contains("over-allocated", StringComparison.OrdinalIgnoreCase));
        findings.Should().OnlyContain(f => f.ProducingAgent == "Resource" && f.Citation.Locator.Length > 0);
        // Confidence is set deterministically from the DQ signal (clean data → High).
        findings.Should().OnlyContain(f => f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task Flags_a_missing_key_role()
    {
        var findings = await Run(Assign("Sam", "Engineer", allocation: 80));

        findings.Should().Contain(f => f.Summary.Contains("Project Manager", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Flags_a_heavily_allocated_person_on_leave()
    {
        var findings = await Run(Assign("Sam", "Project Manager", allocation: 80, onLeave: true));

        findings.Should().Contain(f => f.Summary.Contains("leave", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Well_staffed_project_produces_no_findings()
    {
        var findings = await Run(
            Assign("Pat", "Project Manager", allocation: 50),
            Assign("Sam", "Engineer", allocation: 80));

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Every_finding_carries_the_resource_area()
    {
        var findings = await Run(Assign("Sam", "Engineer", allocation: 130, capacity: 100));

        findings.Should().NotBeEmpty();
        findings.Should().OnlyContain(f => f.Area == HealthArea.Resource && f.Severity != null);
    }

    [Fact]
    public async Task Heavily_over_allocated_person_is_red()
    {
        // 30 points over capacity → beyond the critical band → Red.
        var findings = await Run(Assign("Sam", "Project Manager", allocation: 130, capacity: 100));

        findings.Should().Contain(f => f.Summary.Contains("over-allocated", StringComparison.OrdinalIgnoreCase)
                                       && f.Severity == Severity.Red);
    }

    [Fact]
    public async Task A_person_on_leave_with_a_heavy_allocation_is_red()
    {
        var findings = await Run(Assign("Sam", "Project Manager", allocation: 80, onLeave: true));

        findings.Should().Contain(f => f.Summary.Contains("leave", StringComparison.OrdinalIgnoreCase)
                                       && f.Severity == Severity.Red);
    }
}
