using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class LlmClientContractTests
{
    private sealed record RiskExtract(string Title, string Severity);

    // A stub proving the port returns a typed, structured result — never free text to parse.
    private sealed class EchoLlmClient : ILlmClient
    {
        public LlmRequest? LastRequest { get; private set; }

        public Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
            where TOutput : notnull
        {
            LastRequest = request;
            object result = new RiskExtract("Vendor slip", "High");
            return Task.FromResult((TOutput)result);
        }
    }

    [Fact]
    public async Task Port_returns_structured_typed_output_and_carries_prompt_version()
    {
        var client = new EchoLlmClient();
        var request = new LlmRequest
        {
            SkillName = "RiskAndIssue",
            Prompt = "Extract risks from: <minutes>",
            PromptVersion = "sha256:deadbeef",
        };

        var result = await client.CompleteAsync<RiskExtract>(request, CancellationToken.None);

        result.Should().BeEquivalentTo(new RiskExtract("Vendor slip", "High"));
        client.LastRequest!.PromptVersion.Should().Be("sha256:deadbeef");
        client.LastRequest!.SkillName.Should().Be("RiskAndIssue");
    }
}
