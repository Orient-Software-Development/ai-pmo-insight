using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class ChallengeAgentTests
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
        new(new Dictionary<string, string> { ["challenge"] = "Critique the findings." });

    private static readonly ChallengeResult FakeCritique =
        new([new Critique("Financial variance", "Cites a single budget line; unverified against actuals.", "medium", "Cross-check with the actuals column.")]);

    private static Finding Make(FindingKind kind, string agent, string summary, Confidence confidence = Confidence.Medium)
    {
        var run = AnalysisRun.Start(Guid.NewGuid(), AnalysisFixtures.RunTime);
        return Finding.Create("ALPHA", summary, new SourceRef("x").ToCitation(run.UploadId),
            run.StartedAt, run.RunId, agent, kind, confidence);
    }

    private static Task<Finding> Run(StubLlm llm, DataQualitySignal quality, IReadOnlyList<Finding> findings)
    {
        var slice = AnalysisFixtures.Slice();
        var narrative = Make(FindingKind.Narrative, "Narrative", "[red] Something is wrong.");
        return new ChallengeSkill(llm, Prompts)
            .ExecuteAsync(new ChallengeInput(slice, quality, findings, narrative), CancellationToken.None);
    }

    [Fact]
    public async Task Critiques_findings_via_the_llm_and_persists_a_challenge_finding()
    {
        var llm = new StubLlm(FakeCritique);
        var findings = new[] { Make(FindingKind.Analysis, "Financial", "Forecast exceeds budget by 18%.") };

        var challenge = await Run(llm, DataQualitySignal.Clean(), findings);

        llm.Calls.Should().Be(1);
        challenge.Kind.Should().Be(FindingKind.Challenge);
        challenge.ProducingAgent.Should().Be("Challenge");
        challenge.PromptVersion.Should().StartWith("sha256:");
        challenge.Summary.Should().Contain("unverified against actuals");
    }

    [Fact]
    public async Task With_nothing_to_challenge_it_does_not_call_the_llm()
    {
        var llm = new StubLlm(FakeCritique);

        var challenge = await Run(llm, DataQualitySignal.Clean(), []);

        llm.Calls.Should().Be(0);
        challenge.Kind.Should().Be(FindingKind.Challenge);
        challenge.PromptVersion.Should().BeNull();
    }

    [Fact]
    public async Task Adds_a_deterministic_stale_data_caveat_when_confidence_is_low()
    {
        var llm = new StubLlm(FakeCritique);
        var lowQuality = new DataQualitySignal { MissingFieldCount = 3, LastUpdateAgeDays = 200, SourceConsistent = false };
        var findings = new[] { Make(FindingKind.Analysis, "Status", "Milestone overdue.", Confidence.Low) };

        var challenge = await Run(llm, lowQuality, findings);

        challenge.Summary.Should().Contain("data quality");
    }
}
