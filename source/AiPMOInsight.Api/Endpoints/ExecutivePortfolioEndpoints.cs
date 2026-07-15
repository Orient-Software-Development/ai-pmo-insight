using AiPMOInsight.Application.Features.ExecutivePortfolio;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Api.Endpoints;

/// <summary>
/// Read surface for the Level-1 executive portfolio roll-up (Phase 5). A single authorized GET returns
/// the RAG counts, the "Needs PM Review" count, aggregate confidence, and a worst-first list of the
/// projects needing intervention (each with a cited reason). Consistent with the other read surfaces:
/// authenticated, shared-workspace, view-only. It re-computes on demand from the latest run per project
/// and never triggers a new (paid) analysis. An empty findings store yields a zeroed 200, never 404.
/// </summary>
public static class ExecutivePortfolioEndpoints
{
    public static IEndpointRouteBuilder MapExecutivePortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/portfolio", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ScorePortfolio.Query(), ct)))
            .WithTags("ExecutivePortfolio")
            .RequireAuthorization()
            .WithName("GetExecutivePortfolio");

        return app;
    }
}
