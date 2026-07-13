using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        // LLM port — this slice registers ONLY the fake (fixture responses, no API key). The real
        // vendor adapter is a later change, selected via LlmOptions.Provider without touching Application.
        services.AddSingleton<ILlmClient>(_ => new FakeLlmClient(FakeLlmFixtures.Default()));

        return services;
    }
}
