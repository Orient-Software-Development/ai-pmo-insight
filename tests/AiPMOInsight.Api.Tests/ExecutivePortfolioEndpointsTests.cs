using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// End-to-end tests for the Level-1 portfolio read endpoint (add-executive-portfolio-dashboard):
/// upload → analyze → GET /api/portfolio returns the roll-up (RAG counts, needs-PM-review, aggregate
/// confidence, and a worst-first intervention list with cited reasons). Empty store → zeroed 200;
/// unauthenticated → 401. Shared-workspace, view-only — consistent with the other read surfaces.
/// </summary>
public class ExecutivePortfolioEndpointsTests
{
    [Fact]
    public async Task Returns_the_rollup_after_analysis_with_cited_intervention_entries()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client);
        await client.PostAsync($"/api/analyze/{uploadId}", content: null);

        var response = await client.GetAsync("/api/portfolio");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var portfolio = await response.Content.ReadFromJsonAsync<PortfolioResponse>();
        portfolio.Should().NotBeNull();
        // The fixture analyzes a single project (ALPHA); it lands in exactly one bucket.
        (portfolio!.Red + portfolio.Amber + portfolio.Green).Should().Be(1);

        // If ALPHA needs intervention (red/amber) the entry is present and cited; if green, the list is
        // empty. Either way every intervention entry must carry a reason + citation locator.
        portfolio.Intervention.Should().OnlyContain(i =>
            i.Reason.Length > 0 && i.CitationLocator.Length > 0 && (i.Status == "Red" || i.Status == "Amber"));
    }

    [Fact]
    public async Task Empty_store_returns_a_zeroed_two_hundred()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var response = await client.GetAsync("/api/portfolio");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var portfolio = await response.Content.ReadFromJsonAsync<PortfolioResponse>();
        portfolio!.Red.Should().Be(0);
        portfolio.Amber.Should().Be(0);
        portfolio.Green.Should().Be(0);
        portfolio.NeedsPmReview.Should().Be(0);
        portfolio.Intervention.Should().BeEmpty();
    }

    [Fact]
    public async Task Endpoint_requires_authentication()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient(); // no auth headers

        var response = await client.GetAsync("/api/portfolio");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<Guid> UploadWorkbookAsync(HttpClient client)
    {
        var bytes = OrbitFixtureBuilder.Workbook();
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        content.Add(file, "file", "orbit.xlsx");

        var response = await client.PostAsync("/api/ingest/upload", content);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        return result!.UploadId;
    }

    private sealed record UploadResponse(Guid UploadId, string FileName);
    private sealed record PortfolioResponse(
        int Red, int Amber, int Green, int NeedsPmReview, double AverageConfidence,
        List<InterventionView> Intervention);
    private sealed record InterventionView(
        string ProjectKey, string Status, double Confidence, string Reason, string CitationLocator);
}
