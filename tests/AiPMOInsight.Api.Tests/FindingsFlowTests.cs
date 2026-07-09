using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// End-to-end skeleton flow through the real HTTP + Application + EF pipeline (in-memory store):
/// upload a dummy Orbit-shaped fixture -> analyze -> read the project's cited findings.
/// </summary>
public class FindingsFlowTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "orbit-projects-sample.csv");

    [Fact]
    public async Task Upload_then_analyze_then_read_returns_cited_finding()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        // Upload the golden-file fixture as multipart form data.
        var uploadId = await UploadFixtureAsync(client);

        // Analyze (separate, synchronous step).
        var analyze = await client.PostAsync($"/api/analyze/{uploadId}", content: null);
        analyze.StatusCode.Should().Be(HttpStatusCode.OK);
        var analyzed = await analyze.Content.ReadFromJsonAsync<AnalyzeResponse>();
        analyzed!.Findings.Should().ContainSingle();
        var projectKey = analyzed.Findings[0].ProjectKey;

        // Read the Level-2 view for that project key.
        var read = await client.GetAsync($"/api/projects/{projectKey}");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        var findings = await read.Content.ReadFromJsonAsync<List<FindingResponse>>();

        findings.Should().ContainSingle();
        var finding = findings![0];
        finding.ProjectKey.Should().Be(projectKey);
        // The finding cites the very upload we analyzed — the trust link the skeleton must prove.
        finding.Citation.Should().NotBeNull();
        finding.Citation.UploadId.Should().Be(uploadId);
        finding.Citation.Locator.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Analyze_unknown_upload_returns_not_found()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var response = await client.PostAsync($"/api/analyze/{Guid.NewGuid()}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient(); // no auth headers

        var upload = await client.PostAsync("/api/ingest/upload", content: null);
        var analyze = await client.PostAsync($"/api/analyze/{Guid.NewGuid()}", content: null);
        var read = await client.GetAsync("/api/projects/DUMMY-001");

        upload.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        analyze.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        read.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Read_unknown_project_key_returns_empty_list()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var response = await client.GetAsync($"/api/projects/does-not-exist-{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var findings = await response.Content.ReadFromJsonAsync<List<FindingResponse>>();
        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_upload_is_rejected()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        using var content = new MultipartFormDataContent();
        using var empty = new ByteArrayContent([]);
        content.Add(empty, "file", "empty.csv");

        var response = await client.PostAsync("/api/ingest/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> UploadFixtureAsync(HttpClient client)
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath);
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        content.Add(file, "file", "orbit-projects-sample.csv");

        var response = await client.PostAsync("/api/ingest/upload", content);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        return result!.UploadId;
    }

    private sealed record UploadResponse(Guid UploadId, string FileName);
    private sealed record AnalyzeResponse(List<FindingResponse> Findings);
    private sealed record FindingResponse(Guid Id, string ProjectKey, string Summary, CitationResponse Citation, DateTimeOffset CreatedAt);
    private sealed record CitationResponse(Guid UploadId, string Locator);
}
