using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class ReviewAgentTests
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
        new(new Dictionary<string, string> { ["review"] = "Anticipate stakeholder questions." });

    private static readonly ReviewResult FakeReview = new(new Dictionary<string, IReadOnlyList<string>>
    {
        ["executive"] = ["When will the vendor slip be resolved?"],
        ["sponsor"] = ["What is the cost impact of the overrun?"],
    });

    private static Finding Make(FindingKind kind, string agent, string summary)
    {
        var run = AnalysisRun.Start(Guid.NewGuid(), AnalysisFixtures.RunTime);
        return Finding.Create("ALPHA", summary, new SourceRef("x").ToCitation(run.UploadId),
            run.StartedAt, run.RunId, agent, kind, Confidence.Medium,
            area: HealthArea.Schedule, severity: Severity.Amber);
    }

    private static Task<Finding> Run(StubLlm llm, IReadOnlyList<Finding> findings)
    {
        var slice = AnalysisFixtures.Slice();
        var narrative = Make(FindingKind.Narrative, "Narrative", "[red] Something is wrong.");
        var challenge = Make(FindingKind.Challenge, "Challenge", "[medium] Verify the variance.");
        return new ReviewSkill(llm, Prompts)
            .ExecuteAsync(new ReviewInput(slice, findings, narrative, challenge), CancellationToken.None);
    }

    [Fact]
    public async Task Produces_audience_grouped_questions_via_the_llm()
    {
        var llm = new StubLlm(FakeReview);
        var findings = new[] { Make(FindingKind.Analysis, "Financial", "Forecast exceeds budget.") };

        var review = await Run(llm, findings);

        llm.Calls.Should().Be(1);
        review.Kind.Should().Be(FindingKind.Review);
        review.ProducingAgent.Should().Be("Review");
        review.PromptVersion.Should().StartWith("sha256:");
        review.Summary.Should().Contain("executive");
        review.Summary.Should().Contain("cost impact");
    }

    [Fact]
    public async Task With_no_findings_it_does_not_call_the_llm()
    {
        var llm = new StubLlm(FakeReview);

        var review = await Run(llm, []);

        llm.Calls.Should().Be(0);
        review.Kind.Should().Be(FindingKind.Review);
        review.PromptVersion.Should().BeNull();
    }
}
