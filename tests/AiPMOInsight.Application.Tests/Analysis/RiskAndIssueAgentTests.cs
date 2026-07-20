using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class RiskAndIssueAgentTests
{
    /// <summary>Returns a fixed extraction; <paramref name="risks"/> lets a test control each risk's SourceLocator.</summary>
    private sealed class SpyLlm(params ExtractedRisk[] risks) : ILlmClient
    {
        public int Calls { get; private set; }
        public string? LastPrompt { get; private set; }

        public Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
            where TOutput : notnull
        {
            Calls++;
            LastPrompt = request.Prompt;
            object response = new MinuteRiskExtraction(risks);
            return Task.FromResult((TOutput)response);
        }
    }

    private static readonly PromptRegistry Prompts =
        new(new Dictionary<string, string> { ["risk-and-issue"] = "Extract risks from minutes." });

    private static RaidItemRecord Raid => new()
    {
        ProjectKey = "ALPHA",
        Type = RaidType.Risk,
        Description = "Vendor API may slip",
        Severity = "High",
        Status = "Open",
        Source = new SourceRef("RAID!row2"),
    };

    private static MinuteEntryRecord Minute => new()
    {
        ProjectKey = "ALPHA",
        Date = null,
        Text = "Vendor flagged a two-week slip.",
        Source = new SourceRef("minutes.docx:para3"),
    };

    private static MinuteEntryRecord SecondMinute => new()
    {
        ProjectKey = "ALPHA",
        Date = null,
        Text = "Security review flagged an outstanding access-control gap.",
        Source = new SourceRef("minutes.docx:para7"),
    };

    [Fact]
    public async Task With_no_minutes_it_is_fully_deterministic_and_makes_no_llm_call()
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project()], raidItems: [Raid]));
        var spy = new SpyLlm();

        var findings = await new RiskAndIssueSkill(spy, Prompts).ExecuteAsync(AnalysisFixtures.Input(slice), CancellationToken.None);

        spy.Calls.Should().Be(0);
        findings.Should().OnlyContain(f => f.ProducingAgent == "RiskAndIssue");
        findings.Should().Contain(f => f.Summary.Contains("Vendor API may slip"));
        // Deterministic RAID findings carry no prompt version.
        findings.Should().OnlyContain(f => f.PromptVersion == null);
    }

    [Fact]
    public async Task With_minutes_it_extracts_via_the_llm_and_cites_the_minutes_locator()
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project()], raidItems: [Raid], minutes: [Minute]));
        var spy = new SpyLlm(new ExtractedRisk("Vendor slip", "risk", "high", "Vendor flagged a slip.", "minutes.docx:para3"));

        var findings = await new RiskAndIssueSkill(spy, Prompts).ExecuteAsync(AnalysisFixtures.Input(slice), CancellationToken.None);

        spy.Calls.Should().Be(1);
        // A deterministic RAID finding AND an LLM-extracted finding.
        findings.Should().Contain(f => f.PromptVersion == null && f.Summary.Contains("Vendor API may slip"));
        findings.Should().Contain(f => f.PromptVersion != null && f.Citation.Locator == "minutes.docx:para3");
        // LLM findings carry the prompt registry's content-hash version.
        findings.First(f => f.PromptVersion != null).PromptVersion.Should().StartWith("sha256:");
    }

    [Fact]
    public async Task With_multiple_minutes_entries_each_risk_cites_its_own_locator()
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project()], raidItems: [], minutes: [Minute, SecondMinute]));
        var spy = new SpyLlm(
            new ExtractedRisk("Vendor slip", "risk", "high", "Vendor flagged a slip.", "minutes.docx:para3"),
            new ExtractedRisk("Access-control gap", "issue", "medium", "Security review flagged a gap.", "minutes.docx:para7"));

        var findings = await new RiskAndIssueSkill(spy, Prompts).ExecuteAsync(AnalysisFixtures.Input(slice), CancellationToken.None);

        // Each extracted risk cites the minutes block it actually named, not always the first one.
        findings.Should().Contain(f => f.Summary.Contains("Vendor slip") && f.Citation.Locator == "minutes.docx:para3");
        findings.Should().Contain(f => f.Summary.Contains("Access-control gap") && f.Citation.Locator == "minutes.docx:para7");

        // Both blocks are sent to the model, each tagged with its own locator.
        spy.LastPrompt.Should().Contain("[LOCATOR: minutes.docx:para3]");
        spy.LastPrompt.Should().Contain("[LOCATOR: minutes.docx:para7]");
    }

    [Fact]
    public async Task An_unrecognized_source_locator_falls_back_to_the_first_minutes_entry()
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(
            projects: [AnalysisFixtures.Project()], raidItems: [], minutes: [Minute, SecondMinute]));
        // The model didn't copy a real locator tag (hallucinated / paraphrased) — must not throw,
        // and must not silently mis-cite the second block either.
        var spy = new SpyLlm(new ExtractedRisk("Vendor slip", "risk", "high", "Vendor flagged a slip.", "not-a-real-locator"));

        var findings = await new RiskAndIssueSkill(spy, Prompts).ExecuteAsync(AnalysisFixtures.Input(slice), CancellationToken.None);

        findings.Should().Contain(f => f.Summary.Contains("Vendor slip") && f.Citation.Locator == "minutes.docx:para3");
    }
}
