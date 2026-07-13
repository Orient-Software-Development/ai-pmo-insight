using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// The single-file switch that maps a <see cref="LlmProviderOptions"/> to a concrete
/// <see cref="ILlmClient"/>. This slice (#23 / Phase 3.9) wires only the <c>fake</c> selector and
/// the unknown-provider startup guard; the <c>anthropic</c> / <c>openai</c> stub-adapter cases
/// arrive in #24 / Phase 3.10.
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
    public void Create_openai_returns_the_stub_adapter(string provider)
    {
        var client = Factory.Create("Narrative", new LlmProviderOptions { Provider = provider });

        client.Should().BeOfType<OpenAiLlmClient>();
    }

    [Theory]
    [InlineData("openai")]
    public async Task OpenAi_stub_construction_succeeds_but_CompleteAsync_throws_naming_provider_and_skill(string provider)
    {
        // Design §4: prod-shape config boots; the "not yet wired" failure surfaces only when an
        // agent actually calls the model — never at construction / DI resolution. Anthropic is now
        // wired (see #27); only OpenAI remains a deliberate stub pending its own follow-up.
        var client = Factory.Create("Narrative", new LlmProviderOptions { Provider = provider });
        var request = new LlmRequest { SkillName = "Narrative", Prompt = "p", PromptVersion = "sha256:x" };

        var act = async () => await client.CompleteAsync<string>(request, CancellationToken.None);

        (await act.Should().ThrowAsync<NotImplementedException>())
            .Where(ex => ex.Message.Contains(provider) && ex.Message.Contains("Narrative"));
    }

    [Theory]
    [InlineData("openai")]
    public async Task OpenAi_stub_exception_does_not_leak_the_api_key(string provider)
    {
        // R3 secret-leak guard for the OpenAI stub. Anthropic's live-path guard is in AnthropicLlmClientTests.
        const string secret = "sk-super-secret-value-do-not-log-123";
        var client = Factory.Create("Narrative", new LlmProviderOptions { Provider = provider, ApiKey = secret });
        var request = new LlmRequest { SkillName = "Narrative", Prompt = "p", PromptVersion = "sha256:x" };

        var act = async () => await client.CompleteAsync<string>(request, CancellationToken.None);

        (await act.Should().ThrowAsync<NotImplementedException>())
            .Where(ex => !ex.Message.Contains(secret));
    }
}
