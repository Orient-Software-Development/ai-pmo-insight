using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// The single-file switch that maps a <see cref="LlmProviderOptions"/> to a concrete
/// <see cref="ILlmClient"/>: the <c>fake</c> selector, the unknown-provider startup guard, and the
/// working <c>anthropic</c> / <c>openai</c> vendor adapters (their live call paths are covered by
/// AnthropicLlmClientTests / OpenAiLlmClientTests).
/// </summary>
public class LlmClientFactoryTests
{
    private static readonly ILlmClientFactory Factory = new LlmClientFactory();

    [Fact]
    public void Create_fake_returns_a_working_FakeLlmClient()
    {
        var client = Factory.Create("Narrative", new LlmProviderOptions { Provider = "fake" });

        client.Should().BeOfType<FakeLlmClient>();
    }

    [Theory]
    [InlineData("fake")]
    [InlineData("FAKE")]
    [InlineData("Fake")]
    public void Create_matches_provider_case_insensitively(string provider)
    {
        var client = Factory.Create("Narrative", new LlmProviderOptions { Provider = provider });

        client.Should().BeOfType<FakeLlmClient>();
    }

    [Fact]
    public void Create_throws_InvalidOperationException_for_unknown_provider()
    {
        var act = () => Factory.Create("Narrative", new LlmProviderOptions { Provider = "grok" });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_error_message_names_the_skill_and_provider()
    {
        var act = () => Factory.Create("Narrative", new LlmProviderOptions { Provider = "grok" });

        act.Should().Throw<InvalidOperationException>()
           .Where(ex => ex.Message.Contains("Narrative") && ex.Message.Contains("grok"));
    }

    [Fact]
    public void Create_throws_for_empty_provider()
    {
        // Empty Provider on Default means "not configured" — the DI legacy-fold step is expected
        // to populate it before the factory is invoked. Failing here at startup is the desired
        // loud-fail behaviour.
        var act = () => Factory.Create("Narrative", new LlmProviderOptions { Provider = string.Empty });

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("anthropic")]
    [InlineData("Anthropic")]
    public void Create_anthropic_returns_the_working_adapter(string provider)
    {
        // #27: the anthropic selector now constructs a working Messages API adapter (its live
        // behaviour — structured output, budget, secret-leak guard — is covered by AnthropicLlmClientTests).
        var client = Factory.Create("Narrative", new LlmProviderOptions { Provider = provider });

        client.Should().BeOfType<AnthropicLlmClient>();
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("OpenAI")]
    public void Create_openai_returns_the_working_adapter(string provider)
    {
        // The openai selector now constructs a working Chat Completions adapter (its live behaviour
        // — structured output, budget, secret-leak guard — is covered by OpenAiLlmClientTests).
        var client = Factory.Create("Narrative", new LlmProviderOptions { Provider = provider });

        client.Should().BeOfType<OpenAiLlmClient>();
    }
}
