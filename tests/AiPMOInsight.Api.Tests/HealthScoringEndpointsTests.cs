using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// End-to-end tests for the health-score read endpoint: upload → analyze → GET the project's score
/// returns the full auditable result; an unknown project → 404; unauthenticated → 401.
/// </summary>
public class HealthScoringEndpointsTests
{
    [Fact]
    public async Task Returns_the_full_audit_result_after_analysis()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client);
        await client.PostAsync($"/api/analyze/{uploadId}", content: null);

        var response = await client.GetAsync("/api/projects/ALPHA/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ScoreResponse>();
        result!.ProjectKey.Should().Be("ALPHA");
        result.Score.Should().NotBeNull();
        result.Score!.RawBucket.Should().BeOneOf("Green", "Amber", "Red");
        result.Score.FinalBucket.Should().BeOneOf("Green", "Amber", "Red");
        result.Score.Areas.Should().NotBeEmpty();
        result.Score.Areas.Should().OnlyContain(a => a.Weight >= 0 && a.Severity.Length > 0);
    }

    [Fact]
    public async Task Unknown_project_returns_not_found()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var response = await client.GetAsync($"/api/projects/does-not-exist-{Guid.NewGuid()}/health");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoint_requires_authentication()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient(); // no auth headers

        var response = await client.GetAsync("/api/projects/ALPHA/health");

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
    private sealed record ScoreResponse(string ProjectKey, ScoreView? Score);
    private sealed record ScoreView(
        Guid RunId, double RawScore, string RawBucket, string FinalBucket, bool NeedsPmReview,
        double Confidence, List<AreaView> Areas, List<OverrideView> AppliedOverrides);
    private sealed record AreaView(string Area, string Severity, int Weight, double Contribution);
    private sealed record OverrideView(string RuleId, string Floor, string Reason, Guid FindingId, string CitationLocator);
}
