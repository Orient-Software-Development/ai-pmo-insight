using AiPMOInsight.Application.Features.Findings;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Api.Endpoints;

/// <summary>
/// Thin HTTP layer over the findings slices: trigger a (stub) analysis of an upload, and read a
/// project's findings (Level 2). Analysis is a separate step from upload and runs synchronously.
/// </summary>
public static class FindingsEndpoints
{
    public static IEndpointRouteBuilder MapFindingsEndpoints(this IEndpointRouteBuilder app)
    {
        var analyze = app.MapGroup("/api/analyze").WithTags("Analysis").RequireAuthorization();

        analyze.MapPost("/{uploadId:guid}", async (Guid uploadId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AnalyzeUpload.Command(uploadId), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("AnalyzeUpload");

        var projects = app.MapGroup("/api/projects").WithTags("Projects").RequireAuthorization();

        projects.MapGet("/{projectKey}", async (string projectKey, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetProjectFindings.Query(projectKey), ct);
            return Results.Ok(result);
        })
        .WithName("GetProjectFindings");

        return app;
    }
}
