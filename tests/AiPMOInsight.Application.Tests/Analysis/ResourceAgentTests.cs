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

    // A PM assignment for a person on a specific project — used to build cross-project concentration.
    private static AssignmentRecord PmIn(string project, string person) => new()
    {
        ProjectKey = project,
        Person = person,
        Role = "Project Manager",
        AllocationPercent = 30,
        CapacityPercent = 100,
        OnLeave = false,
        Source = new SourceRef($"Resources!{project}:{person}"),
    };

    // Analyse a specific project's slice over a portfolio-wide set of assignments.
    private static Task<IReadOnlyList<Finding>> RunFor(string projectKey, params AssignmentRecord[] assignments)
    {
        var slice = AnalysisFixtures.Slice(projectKey: projectKey, data: AnalysisFixtures.Data(
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

    [Theory]
    [InlineData("Project Management")] // the fixture value that the old Contains("Manager") match failed on
    [InlineData("Project Manager")]
    [InlineData("PM")]
    public async Task A_project_manager_role_is_recognised(string pmRole)
    {
        // A project WITH a PM (under any of these role spellings) must not be flagged "no project manager".
        var findings = await Run(Assign("Sam", pmRole, allocation: 40));

        findings.Should().NotContain(f =>
            f.Summary.Contains("No Project Manager", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task A_person_on_five_projects_is_a_red_concentration_risk()
    {
        // Anna is allocated across five distinct projects; analysing one of them (P1) surfaces the
        // portfolio-wide concentration, attached to this project, at Red (5+ band).
        var findings = await RunFor("P1",
            PmIn("P1", "Anna"), PmIn("P2", "Anna"), PmIn("P3", "Anna"),
            PmIn("P4", "Anna"), PmIn("P5", "Anna"));

        findings.Should().Contain(f =>
            f.Summary.Contains("Anna", StringComparison.OrdinalIgnoreCase)
            && f.Summary.Contains("concentration", StringComparison.OrdinalIgnoreCase)
            && f.Severity == Severity.Red);
    }

    [Fact]
    public async Task A_person_on_three_projects_is_an_amber_concentration_risk()
    {
        var findings = await RunFor("P1", PmIn("P1", "Anna"), PmIn("P2", "Anna"), PmIn("P3", "Anna"));

        findings.Should().Contain(f =>
            f.Summary.Contains("concentration", StringComparison.OrdinalIgnoreCase)
            && f.Severity == Severity.Amber);
    }

    [Fact]
    public async Task A_person_on_two_projects_is_not_a_concentration_risk()
    {
        var findings = await RunFor("P1", PmIn("P1", "Bob"), PmIn("P2", "Bob"));

        findings.Should().NotContain(f => f.Summary.Contains("concentration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Concentration_is_only_emitted_on_projects_the_person_is_on()
    {
        // Anna is on P2..P6 (5 projects) but NOT on P1; analysing P1 must not emit her concentration here.
        var findings = await RunFor("P1",
            PmIn("P1", "Bob"),
            PmIn("P2", "Anna"), PmIn("P3", "Anna"), PmIn("P4", "Anna"),
            PmIn("P5", "Anna"), PmIn("P6", "Anna"));

        findings.Should().NotContain(f => f.Summary.Contains("Anna", StringComparison.OrdinalIgnoreCase));
    }
}
