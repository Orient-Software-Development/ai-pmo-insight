using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.HealthScoring;
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

        // Health scoring configuration (Phase 4). Bind the swappable weights/thresholds/overrides and
        // validate at startup — fail fast (naming the offending key) rather than serve a bad score.
        // The registered instance is the sole source the scoring service reads; the shipped default
        // carries the PRD EXAMPLE placeholders until the PMO agrees real numbers (see appsettings).
        var healthScoringOptions = new HealthScoringOptions();
        configuration.GetSection(HealthScoringOptions.SectionName).Bind(healthScoringOptions);
        healthScoringOptions.Validate();
        services.AddSingleton(healthScoringOptions);

        // Data Quality agent configuration (L3 gaps): the per-risk staleness window and the
        // duplicate-identity score threshold/weights are POC EXAMPLE values (see appsettings) until
        // the PMO agrees real numbers at kickoff — same externalised-config pattern as health scoring.
        var dataQualityOptions = new DataQualityOptions();
        configuration.GetSection(DataQualityOptions.SectionName).Bind(dataQualityOptions);
        dataQualityOptions.Validate();
        services.AddSingleton(dataQualityOptions);

        // Data Collector (#1) file parsing adapter (ClosedXML / OpenXml / System.Xml).
        services.AddScoped<IUploadParser, UploadParser>();

        // LLM port — routing adapter dispatches per-agent by LlmRequest.SkillName.
        // Bind + fold once, validate the agent keys, then build the inner clients eagerly via the
        // LlmClientFactory so that a bad key or unknown provider fails startup (loud), never at
        // request time (silent). Agent code depends only on ILlmClient and has no awareness of
        // routing or per-agent config.
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));

        var boundOptions = BindLlmOptions(configuration);
        var promotedLegacyKeys = LegacyKeysPromotedByFold(boundOptions);
        var llmOptions = FoldLegacyFlatKeys(boundOptions);
        ValidateAgentKeys(llmOptions);
        var routingClient = BuildRoutingClient(llmOptions);

        services.AddSingleton<ILlmClient>(serviceProvider =>
        {
            // Emit the wiring diagnostics once, when the router is first materialized: the per-agent
            // resolution (R4) and — when the legacy flat shape was folded — a migration warning
            // naming the promoted keys (#25). Never logs any ApiKey value (R3).
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            if (loggerFactory is not null)
            {
                LogRoutingDiagnostics(
                    loggerFactory.CreateLogger("AiPMOInsight.Infrastructure.Analysis.Llm"),
                    llmOptions, promotedLegacyKeys);
            }

            return routingClient;
        });

        return services;
    }

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
            if (!LlmAgentSkills.All.Contains(key))
            {
                throw new InvalidOperationException(
                    $"Unknown agent key '{key}' in 'Llm.Agents'. Recognised LLM-backed agents: " +
                    $"{string.Join(", ", LlmAgentSkills.All)}. The key must match the agent's SkillName.");
            }
        }
    }

    /// <summary>
    /// Reports which legacy flat keys the <see cref="FoldLegacyFlatKeys"/> step will promote into
    /// <c>Llm.Default</c> — used to warn ops at startup. Returns the offending config key names
    /// (never their values, R3), or an empty list when no fold fires (explicit <c>Default</c> present,
    /// or no legacy keys set). Kept in sync with the fold predicate below.
    /// </summary>
    private static IReadOnlyList<string> LegacyKeysPromotedByFold(LlmOptions options)
    {
        if (!string.IsNullOrEmpty(options.Default.Provider))
        {
            return Array.Empty<string>();
        }

        var promoted = new List<string>();
        if (!string.IsNullOrEmpty(options.Provider)) promoted.Add("Llm:Provider");
        if (!string.IsNullOrEmpty(options.ModelId)) promoted.Add("Llm:ModelId");
        if (!string.IsNullOrEmpty(options.ApiKey)) promoted.Add("Llm:ApiKey");
        if (options.PerAnalysisTokenBudget != 0) promoted.Add("Llm:PerAnalysisTokenBudget");
        return promoted;
    }

    /// <summary>
    /// Logs the routing wiring: an info line with the resolved provider per agent (R4; lists
    /// <c>Default</c> plus each known agent's effective provider, never the <c>ApiKey</c>, R3), and —
    /// when <paramref name="promotedLegacyKeys"/> is non-empty — a warning that the deprecated flat
    /// shape was folded, naming the promoted config keys so ops can migrate deliberately (#25).
    /// </summary>
    private static void LogRoutingDiagnostics(
        ILogger logger, LlmOptions options, IReadOnlyList<string> promotedLegacyKeys)
    {
        var lines = new List<string> { $"Default={ProviderLabel(options.Default.Provider)}" };
        foreach (var skill in LlmAgentSkills.All.OrderBy(name => name, StringComparer.Ordinal))
        {
            lines.Add($"{skill}={ProviderLabel(options.ResolvedFor(skill).Provider)}");
        }

        logger.LogInformation("LLM routing resolved (provider per agent): {Routing}", string.Join(", ", lines));

        if (promotedLegacyKeys.Count > 0)
        {
            logger.LogWarning(
                "LLM config used the deprecated flat shape; promoted {LegacyKeys} into 'Llm.Default'. " +
                "Migrate to the 'Llm.Default' block — the legacy flat keys are honoured for one release only.",
                string.Join(", ", promotedLegacyKeys));
        }

        static string ProviderLabel(string provider) => string.IsNullOrEmpty(provider) ? "(unset)" : provider;
    }

    /// <summary>
    /// One-release back-compat with the pre-routing flat shape: if <c>Llm.Default.Provider</c> was
    /// not supplied but a legacy <c>Llm.Provider</c> (etc.) is set, promote the legacy keys —
    /// including <c>Llm.PerAnalysisTokenBudget</c> — to the <c>Default</c> block. An explicit
    /// <c>Default</c> block always wins over the legacy keys. <c>internal</c> so the composition
    /// root helper is unit-testable (the folded budget is otherwise unobservable).
    /// </summary>
    internal static LlmOptions FoldLegacyFlatKeys(LlmOptions options)
    {
        var legacyPresent = !string.IsNullOrEmpty(options.Provider)
            || !string.IsNullOrEmpty(options.ModelId)
            || !string.IsNullOrEmpty(options.ApiKey)
            || options.PerAnalysisTokenBudget != 0;

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
                // Explicit Default budget wins; else the legacy flat budget; else the ship default.
                PerAnalysisTokenBudget =
                    options.Default.PerAnalysisTokenBudget != 0 ? options.Default.PerAnalysisTokenBudget
                    : options.PerAnalysisTokenBudget != 0 ? options.PerAnalysisTokenBudget
                    : LlmProviderOptions.DefaultPerAnalysisTokenBudget,
            },
            Agents = options.Agents,
            // Legacy fields intentionally not copied forward — Default is now authoritative.
        };
    }

    private const string DefaultDiagnosticsName = "__default__";
}
