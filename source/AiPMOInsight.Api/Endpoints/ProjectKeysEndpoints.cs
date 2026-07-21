using AiPMOInsight.Application.Features.Projects;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Api.Endpoints;

/// <summary>
/// Read-only project-keys enumeration under <c>/api/projects</c>. Feeds the L2 project switcher
/// dropdown (and any future caller that needs the list) — the per-project findings/health/progress
/// reads still live under <c>/api/projects/{projectKey}/…</c> and are wired separately.
/// Any authenticated caller sees every project (shared workspace).
/// </summary>
public static class ProjectKeysEndpoints
{
    public static IEndpointRouteBuilder MapProjectKeysEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListProjectKeys.Query(), ct);
            return Results.Ok(result);
        })
        .WithTags("Projects")
        .WithName("ListProjectKeys")
        .RequireAuthorization()
        .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK);

        return app;
    }
}
