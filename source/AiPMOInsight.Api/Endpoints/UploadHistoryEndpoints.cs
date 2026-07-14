using AiPMOInsight.Application.Features.History;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Api.Endpoints;

/// <summary>
/// Read-only history surface under <c>/api/uploads</c>: list past uploads and read an upload's
/// latest analysis. Distinct from the ingest-write surface (<c>POST /api/ingest/upload</c>). Both
/// reads require authentication; any authenticated caller sees every upload (shared workspace).
/// </summary>
public static class UploadHistoryEndpoints
{
    public static IEndpointRouteBuilder MapUploadHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/uploads").WithTags("History").RequireAuthorization();

        // List uploads, newest first.
        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUploads.Query(), ct);
            return Results.Ok(result);
        })
        .WithName("ListUploads");

        // An upload's latest analysis run, four sections. 404 when the upload id is unknown.
        group.MapGet("/{uploadId:guid}/findings", async (Guid uploadId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUploadFindings.Query(uploadId), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetUploadFindings");

        return app;
    }
}
