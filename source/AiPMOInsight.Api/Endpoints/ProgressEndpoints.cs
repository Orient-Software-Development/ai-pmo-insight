using AiPMOInsight.Application.Features.Progress;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Api.Endpoints;

/// <summary>
/// Read surface for a project's "this-period progress" (Level-2 panel #2): a run-over-run comparison
/// of the two most recent runs. Authenticated, shared-workspace, view-only — a query, never a new
/// (paid) analysis. 404 when the project has no findings on record; 200 with HasPrevious=false when
/// only one run exists.
/// </summary>
public static class ProgressEndpoints
{
    public static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Progress").RequireAuthorization();

        group.MapGet("/{projectKey}/progress", async (string projectKey, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SummarizeProgress.Query(projectKey), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetProjectProgress")
        .Produces<SummarizeProgress.Result>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
