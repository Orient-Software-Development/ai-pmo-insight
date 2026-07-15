using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the mediator, every <c>IRequestHandler&lt;,&gt;</c> in this assembly, and the
    /// pipeline behaviors. Call from the API composition root.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISender, Mediator>();

        var assembly = Assembly.GetExecutingAssembly();
        var openHandler = typeof(IRequestHandler<,>);

        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandler);

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddTransient(handlerInterface, type);
            }
        }

        // Pipeline behaviors run outermost-first in registration order.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        // Analysis pipeline: the prompt registry (content-hash versioned), the 9 agent skills, and
        // the orchestrator. The LLM port (ILlmClient) and parser port (IUploadParser) are supplied
        // by Infrastructure.
        services.AddSingleton(PromptRegistry.FromEmbeddedResources());
        services.AddScoped<DataCollectorSkill>();
        services.AddScoped<DataQualitySkill>();
        services.AddScoped<StatusSkill>();
        services.AddScoped<RiskAndIssueSkill>();
        services.AddScoped<FinancialSkill>();
        services.AddScoped<ResourceSkill>();
        services.AddScoped<NarrativeSkill>();
        services.AddScoped<ChallengeSkill>();
        services.AddScoped<ReviewSkill>();
        services.AddScoped<AnalysisOrchestrator>();

        // Health scoring (Phase 4): a stateless, pure computation over persisted findings. Singleton —
        // it reads the validated HealthScoringOptions (registered by AddInfrastructure) and holds no
        // per-request state. Resolution happens at request time, after both DI modules have run.
        services.AddSingleton<Features.HealthScoring.HealthScoringService>();

        return services;
    }
}
