using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Infrastructure;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// End-to-end coverage of the routing seam through <see cref="DependencyInjection.AddInfrastructure"/>:
/// the singleton <see cref="ILlmClient"/> is <see cref="RoutingLlmClient"/>, unknown-provider
/// configuration fails at registration (not at request time), and the legacy flat-key shape is
/// still bound as <see cref="LlmOptions.Default"/>.
/// </summary>
public class LlmRoutingDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_registers_RoutingLlmClient_as_the_ILlmClient()
    {
        var provider = BuildProvider(new()
        {
            ["Llm:Default:Provider"] = "fake",
        });

        var client = provider.GetRequiredService<ILlmClient>();

        client.Should().BeOfType<RoutingLlmClient>();
    }

    [Fact]
    public void AddInfrastructure_throws_when_default_provider_is_unknown()
    {
        var act = () => BuildProvider(new()
        {
            ["Llm:Default:Provider"] = "grok",
        });

        act.Should().Throw<InvalidOperationException>()
           .Where(ex => ex.Message.Contains("grok"));
    }

    [Fact]
    public void AddInfrastructure_throws_when_an_agent_provider_is_unknown()
    {
        var act = () => BuildProvider(new()
        {
            ["Llm:Default:Provider"] = "fake",
            ["Llm:Agents:Narrative:Provider"] = "grok",
        });

        act.Should().Throw<InvalidOperationException>()
           .Where(ex => ex.Message.Contains("Narrative") && ex.Message.Contains("grok"));
    }

    [Fact]
    public void AddInfrastructure_folds_legacy_flat_keys_into_Default()
    {
        // A pre-routing config that only sets the top-level Llm.Provider should still boot and
        // resolve to a working router — this keeps existing deployments working with zero config
        // edit after the change lands.
        var provider = BuildProvider(new()
        {
            ["Llm:Provider"] = "fake",
        });

        provider.GetRequiredService<ILlmClient>().Should().BeOfType<RoutingLlmClient>();
    }

    [Fact]
    public void AddInfrastructure_prefers_explicit_Default_over_legacy_keys_when_both_present()
    {
        // Legacy keys say the provider is 'grok', Default explicitly says 'fake'. Default must
        // win — otherwise AddInfrastructure would fail with an unknown-provider error.
        var act = () => BuildProvider(new()
        {
            ["Llm:Provider"] = "grok",
            ["Llm:Default:Provider"] = "fake",
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void AddInfrastructure_throws_when_an_agent_key_is_not_a_known_skill()
    {
        // R1 casing/naming-drift guard: a typo'd agent key would otherwise silently fall back to
        // Default. Startup must fail loudly, naming the offending key.
        var act = () => BuildProvider(new()
        {
            ["Llm:Default:Provider"] = "fake",
            ["Llm:Agents:Narrater:Provider"] = "fake", // typo — not one of the four SkillNames
        });

        act.Should().Throw<InvalidOperationException>()
           .Where(ex => ex.Message.Contains("Narrater"));
    }

    [Fact]
    public void AddInfrastructure_logs_the_resolved_provider_per_agent_without_the_api_key()
    {
        // R4: ops sees the wiring. The startup line lists name → provider for every agent, and R3
        // requires it never carries the ApiKey.
        var captured = new List<string>();
        var provider = BuildProvider(new()
        {
            ["Llm:Default:Provider"] = "fake",
            ["Llm:Agents:Narrative:Provider"] = "fake",
            ["Llm:Agents:Narrative:ApiKey"] = "sk-should-not-be-logged",
        }, captured);

        _ = provider.GetRequiredService<ILlmClient>(); // materialize the routing client → emits the line

        captured.Should().Contain(line => line.Contains("Narrative") && line.Contains("fake"));
        captured.Should().NotContain(line => line.Contains("sk-should-not-be-logged"));
    }

    [Fact]
    public void FoldLegacyFlatKeys_honors_the_legacy_token_budget()
    {
        // Spec "Backwards-compatible options binding": a legacy flat Llm.PerAnalysisTokenBudget must
        // survive the fold into Default, not be discarded for the ship-default.
        var folded = DependencyInjection.FoldLegacyFlatKeys(
            new LlmOptions { Provider = "fake", PerAnalysisTokenBudget = 50_000 });

        folded.Default.Provider.Should().Be("fake");
        folded.Default.PerAnalysisTokenBudget.Should().Be(50_000);
    }

    [Fact]
    public void FoldLegacyFlatKeys_uses_the_ship_default_budget_when_no_budget_is_configured()
    {
        var folded = DependencyInjection.FoldLegacyFlatKeys(new LlmOptions { Provider = "fake" });

        folded.Default.PerAnalysisTokenBudget.Should().Be(LlmProviderOptions.DefaultPerAnalysisTokenBudget);
    }

    [Fact]
    public void FoldLegacyFlatKeys_prefers_an_explicit_Default_budget_over_the_legacy_budget()
    {
        // Default.Provider empty → fold happens; an explicitly-set Default budget still wins.
        var folded = DependencyInjection.FoldLegacyFlatKeys(new LlmOptions
        {
            Default = new LlmProviderOptions { PerAnalysisTokenBudget = 250_000 },
            Provider = "fake",
            PerAnalysisTokenBudget = 50_000,
        });

        folded.Default.Provider.Should().Be("fake");
        folded.Default.PerAnalysisTokenBudget.Should().Be(250_000);
    }

    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> llmSettings, List<string>? capturedLogs = null)
    {
        // AddInfrastructure requires a connection-string entry to satisfy its DbContext guard;
        // the value is irrelevant here — none of these tests touch the DB.
        var settings = new Dictionary<string, string?>(llmSettings)
        {
            ["ConnectionStrings:AppDb"] = "Host=stub;Database=stub;Username=stub;Password=stub",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        if (capturedLogs is not null)
        {
            services.AddLogging(b => b.AddProvider(new CapturingLoggerProvider(capturedLogs)));
        }

        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class CapturingLoggerProvider(List<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(sink);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => sink.Add(formatter(state, exception));
        }
    }
}
