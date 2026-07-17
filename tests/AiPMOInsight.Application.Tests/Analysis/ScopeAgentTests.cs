using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

/// <summary>
/// Scope agent — the POC "unapproved creep" RAG rule (client-agreed rule is a kickoff follow-on):
/// an unapproved scope-increase is Red creep; an approved change is Amber (controlled); rejected
/// changes and no changes produce nothing (Green by absence). Deterministic, cites its scope row.
/// </summary>
public class ScopeAgentTests
{
    private static ScopeChangeRecord Change(string title, string? type, string? status, decimal? impact = null) => new()
    {
        ProjectKey = "ALPHA",
        Title = title,
        Type = type,
        Status = status,
        EffortImpactPct = impact,
        DateRaised = DateTimeOffset.Parse("2026-07-01"),
        Source = new SourceRef($"Scope!{title}"),
    };

    private static Task<IReadOnlyList<Finding>> Run(params ScopeChangeRecord[] changes)
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project()],
            scopeChanges: changes));
        return new ScopeSkill().ExecuteAsync(new AnalysisInput(slice, DataQualitySignal.Clean()), CancellationToken.None);
    }

    [Fact]
    public async Task An_unapproved_scope_increase_is_a_cited_red_finding()
    {
        var findings = await Run(Change("Add mobile app", type: "Add", status: "Requested", impact: 15));

        findings.Should().Contain(f => f.Area == HealthArea.Scope
            && f.Severity == Severity.Red
            && f.ProducingAgent == "Scope"
            && f.Citation.Locator.Contains("Scope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task An_approved_scope_change_is_amber()
    {
        var findings = await Run(Change("Extend reporting", type: "Modify", status: "Approved", impact: 8));

        findings.Should().Contain(f => f.Area == HealthArea.Scope && f.Severity == Severity.Amber);
    }

    [Fact]
    public async Task A_rejected_scope_change_produces_no_finding()
    {
        var findings = await Run(Change("Gold plating", type: "Add", status: "Rejected", impact: 20));

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task No_scope_changes_produce_no_findings()
    {
        var findings = await Run();

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Finding_carries_structured_scope_detail_for_the_panel()
    {
        var findings = await Run(Change("Add mobile app", type: "Add", status: "Requested", impact: 15));

        var detail = findings.Should().ContainSingle().Which.MetricDetail;
        detail.Should().NotBeNull();
        detail!["title"].Should().Be("Add mobile app");
        detail["type"].Should().Be("Add");
        detail["status"].Should().Be("Requested");
        detail["impactPct"].Should().Be("15");
    }
}
