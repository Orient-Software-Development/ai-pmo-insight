using AiPMOInsight.Application.Features.DataQuality;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Api.Endpoints;

/// <summary>
/// Read surface for the Level-3 Data Quality roll-up (Phase 5). A single authorized GET returns the
/// portfolio confidence block (mean confidence, the configured publish threshold, and a below-target
/// flag), a worst-first list of missing/inconsistent items (each cited), and the counts. Consistent with
/// the other read surfaces: authenticated, shared-workspace, view-only. It re-computes on demand from the
/// latest run per project and never triggers a new (paid) analysis. An empty findings store yields a
/// zeroed 200, never 404.
/// </summary>
public static class DataQualityEndpoints
{
    public static IEndpointRouteBuilder MapDataQualityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/data-quality/summary", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new SummarizeDataQuality.Query(), ct)))
            .WithTags("DataQuality")
            .RequireAuthorization()
            .WithName("GetDataQualitySummary");

        return app;
    }
}
