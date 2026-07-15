using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class NarrativeAgentTests
{
    private sealed class StubLlm(object response) : ILlmClient
    {
        public int Calls { get; private set; }

        public Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
            where TOutput : notnull
        {
            Calls++;
            return Task.FromResult((TOutput)response);
        }
    }

    private static readonly PromptRegistry Prompts =
        new(new Dictionary<string, string> { ["narrative"] = "Synthesise a narrative." });

    private static readonly NarrativeResult FakeNarrative =
        new("red", "Multiple signals converge on schedule and cost risk.",
            new Recommendation("PMO Lead", "2026-07-31", "Escalate vendor risk", "Two RED signals cross-reference."));

    private static Finding AnalysisFinding(string agent, string? promptVersion = null)
    {
        var run = AnalysisRun.Start(Guid.NewGuid(), AnalysisFixtures.RunTime);
        return Finding.Create("ALPHA", $"{agent} finding", new SourceRef("x").ToCitation(run.UploadId),
            run.StartedAt, run.RunId, agent, FindingKind.Analysis, Confidence.Medium, promptVersion,
            area: HealthArea.Schedule, severity: Severity.Amber);
    }

    private static Task<Finding> Run(StubLlm llm, params Finding[] findings)
    {
        var slice = AnalysisFixtures.Slice(data: AnalysisFixtures.Data(projects: [AnalysisFixtures.Project()]));
        return new NarrativeSkill(llm, Prompts)
            .ExecuteAsync(new NarrativeInput(slice, DataQualitySignal.Clean(), findings), CancellationToken.None);
    }

    [Fact]
    public async Task No_findings_renders_a_green_narrative_from_a_template_without_the_llm()
    {
        var llm = new StubLlm(FakeNarrative);

        var narrative = await Run(llm);

        llm.Calls.Should().Be(0);
        narrative.Kind.Should().Be(FindingKind.Narrative);
        narrative.ProducingAgent.Should().Be("Narrative");
        narrative.PromptVersion.Should().BeNull();
        narrative.Summary.Should().Contain("green");
    }

    [Fact]
    public async Task A_single_signal_renders_from_a_template_without_the_llm()
    {
        var llm = new StubLlm(FakeNarrative);

        var narrative = await Run(llm, AnalysisFinding("Status"));

        llm.Calls.Should().Be(0);
        narrative.Kind.Should().Be(FindingKind.Narrative);
        narrative.PromptVersion.Should().BeNull();
    }

    [Fact]
    public async Task Three_or_more_signals_fall_back_to_the_llm()
    {
        var llm = new StubLlm(FakeNarrative);

        var narrative = await Run(llm, AnalysisFinding("Status"), AnalysisFinding("Financial"), AnalysisFinding("Resource"));

        llm.Calls.Should().Be(1);
        narrative.Kind.Should().Be(FindingKind.Narrative);
        narrative.PromptVersion.Should().StartWith("sha256:");
        narrative.Summary.Should().Contain("Escalate vendor risk");
    }

    [Fact]
    public async Task A_minute_extracted_signal_forces_the_llm_path()
    {
        var llm = new StubLlm(FakeNarrative);

        var narrative = await Run(llm, AnalysisFinding("RiskAndIssue", promptVersion: "sha256:abc"));

        llm.Calls.Should().Be(1);
        narrative.PromptVersion.Should().NotBeNull();
    }
}
