using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// End-to-end tests for the Level-3 data-quality read endpoint (add-data-quality-dashboard):
/// upload → analyze → GET /api/data-quality/summary returns the roll-up (confidence block with the
/// configured publish threshold + below-target flag, a worst-first cited items list, and counts).
/// Empty store → zeroed 200; unauthenticated → 401. Shared-workspace, view-only — consistent with the
/// other read surfaces.
/// </summary>
public class DataQualityEndpointsTests
{
    // Matches HealthScoring:ConfidenceFloor in the API appsettings the test host loads.
    private const int ConfiguredFloor = 50;

    [Fact]
    public async Task Returns_the_rollup_with_cited_items_after_analysis()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWithDataQualityGapAsync(client);
        await client.PostAsync($"/api/analyze/{uploadId}", content: null);

        var response = await client.GetAsync("/api/data-quality/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<DqResponse>();
        summary.Should().NotBeNull();

        // Confidence block carries the configured threshold and a consistent below-target flag.
        summary!.Confidence.Threshold.Should().Be(ConfiguredFloor);
        summary.Confidence.BelowTarget.Should().Be(summary.Confidence.Mean < ConfiguredFloor);

        // The seeded milestone-with-no-due-date yields at least one real, cited data-quality item.
        summary.Items.Should().NotBeEmpty();
        summary.Items.Should().OnlyContain(i =>
            i.ProjectKey.Length > 0
            && i.Issue.Length > 0
            && i.CitationLocator.Length > 0
            && (i.Severity == "Red" || i.Severity == "Amber" || i.Severity == "Green"));

        // Counts are internally consistent.
        summary.TotalItems.Should().Be(summary.Items.Count);
        summary.PerProject.Sum(p => p.Count).Should().Be(summary.TotalItems);
    }

    [Fact]
    public async Task Empty_store_returns_a_zeroed_two_hundred()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var response = await client.GetAsync("/api/data-quality/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<DqResponse>();
        summary!.Confidence.Mean.Should().Be(0);
        summary.Items.Should().BeEmpty();
        summary.TotalItems.Should().Be(0);
        summary.PerProject.Should().BeEmpty();
    }

    [Fact]
    public async Task Endpoint_requires_authentication()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient(); // no auth headers

        var response = await client.GetAsync("/api/data-quality/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<Guid> UploadWithDataQualityGapAsync(HttpClient client)
    {
        var bytes = OrbitFixtureBuilder.WorkbookWithDataQualityGap();
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        content.Add(file, "file", "orbit.xlsx");

        var response = await client.PostAsync("/api/ingest/upload", content);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        return result!.UploadId;
    }

    private sealed record UploadResponse(Guid UploadId, string FileName);
    private sealed record DqResponse(
        ConfidenceView Confidence, List<ItemView> Items, int TotalItems, List<ProjectCountView> PerProject);
    private sealed record ConfidenceView(double Mean, int Threshold, bool BelowTarget);
    private sealed record ItemView(string ProjectKey, string Issue, string Severity, string CitationLocator);
    private sealed record ProjectCountView(string ProjectKey, int Count);
}
