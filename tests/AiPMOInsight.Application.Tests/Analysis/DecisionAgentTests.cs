using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class DecisionAgentTests
{
    private static DecisionRecord Decision(string title, string? status, string? neededBy) => new()
    {
        ProjectKey = "ALPHA",
        Title = title,
        Status = status,
        Owner = "Steering Committee",
        NeededBy = neededBy is null ? null : DateTimeOffset.Parse(neededBy),
        Consequence = "work blocked",
        Source = new SourceRef($"Decisions!{title}"),
    };

    private static Task<IReadOnlyList<Finding>> Run(params DecisionRecord[] decisions)
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project()],
            decisions: decisions));
        return new DecisionSkill().ExecuteAsync(new AnalysisInput(slice, DataQualitySignal.Clean()), CancellationToken.None);
    }

    [Fact]
    public async Task Overdue_unapproved_decision_is_a_cited_red_finding()
    {
        // Run as-of 2026-07-10; needed-by 2026-06-20 has passed and status is not Approved.
        var findings = await Run(Decision("Approve revised go-live date", status: "Pending", neededBy: "2026-06-20"));

        findings.Should().Contain(f => f.Area == HealthArea.Decision
            && f.Severity == Severity.Red
            && f.ProducingAgent == "Decision"
            && f.Citation.Locator.Contains("Decisions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Due_soon_decision_is_amber()
    {
        // needed-by 2026-07-18 is within the 14-day upcoming window of 2026-07-10.
        var findings = await Run(Decision("Confirm external auditor", status: "Pending", neededBy: "2026-07-18"));

        findings.Should().Contain(f => f.Area == HealthArea.Decision && f.Severity == Severity.Amber);
    }

    [Fact]
    public async Task Approved_decision_produces_no_finding()
    {
        var findings = await Run(Decision("Approve data-ownership RACI", status: "Approved", neededBy: "2026-06-01"));

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Decision_with_no_needed_by_produces_no_finding()
    {
        var findings = await Run(Decision("Someday decision", status: "Pending", neededBy: null));

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Every_finding_carries_the_decision_area()
    {
        var findings = await Run(
            Decision("Overdue one", status: "Pending", neededBy: "2026-06-20"),
            Decision("Due soon one", status: "Pending", neededBy: "2026-07-18"));

        findings.Should().NotBeEmpty();
        findings.Should().OnlyContain(f => f.Area == HealthArea.Decision && f.Severity != null);
    }

    [Fact]
    public async Task Finding_carries_structured_owner_deadline_consequence_for_the_panel()
    {
        // The L2 "Decisions needed" panel renders columns, not a parsed summary string — so the finding
        // must carry the decision's title/owner/deadline/consequence as structured MetricDetail.
        var findings = await Run(Decision("Approve revised go-live date", status: "Pending", neededBy: "2026-06-20"));

        var detail = findings.Should().ContainSingle().Which.MetricDetail;
        detail.Should().NotBeNull();
        detail!["title"].Should().Be("Approve revised go-live date");
        detail["owner"].Should().Be("Steering Committee");
        detail["deadline"].Should().Be("2026-06-20");
        detail["consequence"].Should().Be("work blocked");
    }

    [Fact]
    public async Task Unassigned_owner_and_missing_consequence_degrade_gracefully()
    {
        var decision = new DecisionRecord
        {
            ProjectKey = "ALPHA",
            Title = "Pick a vendor",
            Status = "Pending",
            Owner = null,
            NeededBy = DateTimeOffset.Parse("2026-06-20"),
            Consequence = null,
            Source = new SourceRef("Decisions!Pick a vendor"),
        };

        var detail = (await Run(decision)).Should().ContainSingle().Which.MetricDetail;
        detail!["owner"].Should().Be("unassigned");
        detail["consequence"].Should().BeEmpty();
    }
}
