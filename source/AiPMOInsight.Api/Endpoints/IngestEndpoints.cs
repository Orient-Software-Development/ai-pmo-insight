using AiPMOInsight.Application.Features.Ingest;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Api.Endpoints;

/// <summary>
/// Thin HTTP layer over the ingest slice. Accepts a multipart file upload, stores it as opaque
/// bytes, and returns an upload reference. No parsing happens here — that is deliberately stubbed.
/// </summary>
public static class IngestEndpoints
{
    public static IEndpointRouteBuilder MapIngestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingest").WithTags("Ingest").RequireAuthorization();

        group.MapPost("/upload", async (IFormFile? file, ISender sender, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "A non-empty file is required." });
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);

            var result = await sender.Send(new UploadFixture.Command(file.FileName, stream.ToArray()), ct);
            return Results.Created($"/api/ingest/uploads/{result.UploadId}", result);
        })
        .WithName("UploadFixture")
        // Minimal-API form binding otherwise requires an antiforgery token; this API has no
        // antiforgery middleware and auth is cookie/JWT, so disable the requirement explicitly.
        .DisableAntiforgery()
        .Produces<UploadFixture.Result>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        return app;
    }
}
