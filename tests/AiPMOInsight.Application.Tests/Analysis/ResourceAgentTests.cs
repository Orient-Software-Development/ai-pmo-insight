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
}
