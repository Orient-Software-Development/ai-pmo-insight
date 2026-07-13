using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using AiPMOInsight.Infrastructure.Analysis.Parsing;
using AiPMOInsight.Infrastructure.Findings;
using AiPMOInsight.Infrastructure.Ingest;
using AiPMOInsight.Infrastructure.Persistence;
using AiPMOInsight.Infrastructure.Security;

namespace AiPMOInsight.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure adapters (port implementations) and the EF Core / PostgreSQL
    /// persistence stack. Call from the API composition root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AppDb")
            ?? throw new InvalidOperationException(
                "Missing connection string 'ConnectionStrings:AppDb'. Set it in appsettings or the environment.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUploadRepository, EfUploadRepository>();
        services.AddScoped<IFindingRepository, EfFindingRepository>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        // Data Collector (#1) file parsing adapter (ClosedXML / OpenXml / System.Xml).
        services.AddScoped<IUploadParser, UploadParser>();

        // LLM port — routing adapter dispatches per-agent by LlmRequest.SkillName.
        // Bind + fold once, validate the agent keys, then build the inner clients eagerly via
        // ILlmClientFactory so that a bad key or unknown provider fails startup (loud), never at
        // request time (silent). Agent code depends only on ILlmClient and has no awareness of
        // routing or per-agent config.
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();

        var llmOptions = FoldLegacyFlatKeys(BindLlmOptions(configuration));
        ValidateAgentKeys(llmOptions);
        var routingClient = BuildRoutingClient(llmOptions);

        services.AddSingleton<ILlmClient>(serviceProvider =>
        {
            // Emit the per-agent resolution once, when the router is first materialized, so ops can
            // see which provider each agent got. Never logs the ApiKey (R3/R4).
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            if (loggerFactory is not null)
            {
                LogResolvedProviders(
                    loggerFactory.CreateLogger("AiPMOInsight.Infrastructure.Analysis.Llm"), llmOptions);
            }

            return routingClient;
        });

        return services;
    }

    /// <summary>The four agents whose <c>SkillName</c> may key an <c>Llm.Agents</c> override.</summary>
    private static readonly IReadOnlySet<string> KnownLlmAgentSkills =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "RiskAndIssue", "Narrative", "Challenge", "Review" };

    private static LlmOptions BindLlmOptions(IConfiguration configuration)
    {
        var options = new LlmOptions();
        configuration.GetSection(LlmOptions.SectionName).Bind(options);
        return options;
    }

    private static RoutingLlmClient BuildRoutingClient(LlmOptions options)
    {
        var factory = new LlmClientFactory();

        // Build the default inner client first — this is the fallback for any agent without an
        // explicit override, and its unknown-provider failure short-circuits app startup.
        var defaultClient = factory.Create(DefaultDiagnosticsName, options.Default);

        // Then build one inner client per agent override, keyed case-insensitively so that config
        // authors are not tripped by casing on the SkillName. ResolvedFor merges each override with
        // Default field-by-field so a partial override still boots.
        var perSkill = new Dictionary<string, ILlmClient>(StringComparer.OrdinalIgnoreCase);
        foreach (var skillName in options.Agents.Keys)
        {
            perSkill[skillName] = factory.Create(skillName, options.ResolvedFor(skillName));
        }

        return new RoutingLlmClient(defaultClient, perSkill);
    }

    /// <summary>
    /// R1 casing / naming-drift guard: every key in <c>Llm.Agents</c> must match one of the four
    /// LLM-backed agents' <c>SkillName</c> (case-insensitive). An unknown key would otherwise
    /// silently fall back to <c>Default</c>; instead we fail startup naming the offending key.
    /// </summary>
    private static void ValidateAgentKeys(LlmOptions options)
    {
        foreach (var key in options.Agents.Keys)
        {
            if (!KnownLlmAgentSkills.Contains(key))
            {
                throw new InvalidOperationException(
                    $"Unknown agent key '{key}' in 'Llm.Agents'. Recognised LLM-backed agents: " +
                    $"{string.Join(", ", KnownLlmAgentSkills)}. The key must match the agent's SkillName.");
            }
        }
    }

    /// <summary>
    /// R4: log the resolved provider per agent so ops can see the wiring. Lists <c>Default</c> plus
    /// each known agent's effective provider; never includes the <c>ApiKey</c> (R3).
    /// </summary>
    private static void LogResolvedProviders(ILogger logger, LlmOptions options)
    {
        var lines = new List<string> { $"Default={ProviderLabel(options.Default.Provider)}" };
        foreach (var skill in KnownLlmAgentSkills.OrderBy(name => name, StringComparer.Ordinal))
        {
            lines.Add($"{skill}={ProviderLabel(options.ResolvedFor(skill).Provider)}");
        }

        logger.LogInformation("LLM routing resolved (provider per agent): {Routing}", string.Join(", ", lines));

        static string ProviderLabel(string provider) => string.IsNullOrEmpty(provider) ? "(unset)" : provider;
    }

    /// <summary>
    /// One-release back-compat with the pre-routing flat shape: if <c>Llm.Default.Provider</c> was
    /// not supplied but a legacy <c>Llm.Provider</c> (etc.) is set, promote the legacy keys to the
    /// <c>Default</c> block. An explicit <c>Default</c> block always wins over the legacy keys.
    /// </summary>
    private static LlmOptions FoldLegacyFlatKeys(LlmOptions options)
    {
        var legacyPresent = !string.IsNullOrEmpty(options.Provider)
            || !string.IsNullOrEmpty(options.ModelId)
            || !string.IsNullOrEmpty(options.ApiKey);

        if (!string.IsNullOrEmpty(options.Default.Provider) || !legacyPresent)
        {
            return options;
        }

        return new LlmOptions
        {
            Default = new LlmProviderOptions
            {
                Provider = options.Provider,
                ModelId = options.ModelId,
                ApiKey = options.ApiKey,
                PerAnalysisTokenBudget = options.Default.PerAnalysisTokenBudget != 0
                    ? options.Default.PerAnalysisTokenBudget
                    : 100_000,
            },
            Agents = options.Agents,
            // Legacy fields intentionally not copied forward — Default is now authoritative.
        };
    }

    private const string DefaultDiagnosticsName = "__default__";
}
