using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Api.Endpoints;

/// <summary>
/// Read surface for the per-project RAG health score (Phase 4). A single authorized GET returns the
/// full auditable result (raw score + bucket, applied overrides, final bucket, confidence,
/// "Needs PM Review", per-area breakdown). Consistent with the other read surfaces: authenticated,
/// shared-workspace, view-only. Scoring is a query — it re-computes on demand from the latest run and
/// never triggers a new (paid) analysis.
/// </summary>
public static class HealthScoringEndpoints
{
    public static IEndpointRouteBuilder MapHealthScoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("HealthScoring").RequireAuthorization();

        // A project's current health score. 404 when the project has no findings on record;
        // 200 with a null Score when it has findings but none are scoreable yet.
        group.MapGet("/{projectKey}/health", async (string projectKey, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ScoreProject.Query(projectKey), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetProjectHealthScore")
        .Produces<ScoreProject.Result>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
