using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// The <c>Llm</c> section is now the model-swap seam: <c>Default</c> plus per-agent overrides in
/// <c>Agents.&lt;SkillName&gt;</c>. Pure-config tests exercise binding without booting the host;
/// one host test proves the wiring is still in place.
/// </summary>
public class LlmOptionsTests
{
    [Fact]
    public void Host_binds_Default_provider_from_appsettings()
    {
        using var factory = new TestWebAppFactory();
        _ = factory.CreateClient(); // force the host to build

        var options = factory.Services.GetRequiredService<IOptions<LlmOptions>>().Value;

        options.Default.Provider.Should().Be("fake");
        options.Default.PerAnalysisTokenBudget.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Default_block_binds_from_config()
    {
        var options = Bind(new()
        {
            ["Llm:Default:Provider"] = "anthropic",
            ["Llm:Default:ModelId"] = "claude-sonnet-5",
            ["Llm:Default:ApiKey"] = "K1",
            ["Llm:Default:PerAnalysisTokenBudget"] = "200000",
        });

        options.Default.Provider.Should().Be("anthropic");
        options.Default.ModelId.Should().Be("claude-sonnet-5");
        options.Default.ApiKey.Should().Be("K1");
        options.Default.PerAnalysisTokenBudget.Should().Be(200000);
    }

    [Fact]
    public void Agents_dictionary_binds_from_config()
    {
        var options = Bind(new()
        {
            ["Llm:Default:Provider"] = "fake",
            ["Llm:Agents:Narrative:Provider"] = "openai",
            ["Llm:Agents:Narrative:ModelId"] = "gpt-4o-mini",
            ["Llm:Agents:Narrative:ApiKey"] = "Kn",
        });

        options.Agents.Should().ContainKey("Narrative");
        options.Agents["Narrative"].Provider.Should().Be("openai");
        options.Agents["Narrative"].ModelId.Should().Be("gpt-4o-mini");
        options.Agents["Narrative"].ApiKey.Should().Be("Kn");
    }

    [Fact]
    public void ResolvedFor_returns_agent_override_when_present()
    {
        var options = new LlmOptions
        {
            Default = new LlmProviderOptions { Provider = "fake", ModelId = "d" },
            Agents = new Dictionary<string, LlmProviderOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["Narrative"] = new LlmProviderOptions { Provider = "openai", ModelId = "gpt-4o" },
            },
        };

        var resolved = options.ResolvedFor("Narrative");

        resolved.Provider.Should().Be("openai");
        resolved.ModelId.Should().Be("gpt-4o");
    }

    [Fact]
    public void ResolvedFor_falls_back_to_Default_when_agent_key_missing()
    {
        var options = new LlmOptions
        {
            Default = new LlmProviderOptions { Provider = "fake", ModelId = "d" },
            Agents = new Dictionary<string, LlmProviderOptions>(StringComparer.OrdinalIgnoreCase),
        };

        var resolved = options.ResolvedFor("Challenge");

        resolved.Provider.Should().Be("fake");
        resolved.ModelId.Should().Be("d");
    }

    [Theory]
    [InlineData("Narrative")]
    [InlineData("narrative")]
    [InlineData("NARRATIVE")]
    public void ResolvedFor_matches_agent_key_case_insensitively(string skillName)
    {
        var options = new LlmOptions
        {
            Default = new LlmProviderOptions { Provider = "fake" },
            Agents = new Dictionary<string, LlmProviderOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["Narrative"] = new LlmProviderOptions { Provider = "openai" },
            },
        };

        options.ResolvedFor(skillName).Provider.Should().Be("openai");
    }

    /// <summary>Spec R1 scenario 3 — a partial override inherits unspecified fields from Default.</summary>
    [Fact]
    public void ResolvedFor_merges_partial_override_with_Default()
    {
        var options = new LlmOptions
        {
            Default = new LlmProviderOptions
            {
                Provider = "anthropic",
                ModelId = "claude-sonnet-5",
                ApiKey = "K1",
                PerAnalysisTokenBudget = 200_000,
            },
            Agents = new Dictionary<string, LlmProviderOptions>(StringComparer.OrdinalIgnoreCase)
            {
                // Only ModelId is overridden — Provider / ApiKey / budget must come from Default.
                ["Review"] = new LlmProviderOptions { ModelId = "claude-opus-4-8" },
            },
        };

        var resolved = options.ResolvedFor("Review");

        resolved.Provider.Should().Be("anthropic");
        resolved.ModelId.Should().Be("claude-opus-4-8");
        resolved.ApiKey.Should().Be("K1");
        resolved.PerAnalysisTokenBudget.Should().Be(200_000);
    }

    private static LlmOptions Bind(Dictionary<string, string?> settings)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return cfg.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
    }
}
