using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Integration tests for the read-only history surface: <c>GET /api/uploads</c> (list, newest-first)
/// and <c>GET /api/uploads/{id}/findings</c> (an upload's latest analysis, four sections).
/// </summary>
public class UploadHistoryEndpointsTests
{
    [Fact]
    public async Task List_returns_uploads_newest_first_without_content()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var first = await UploadWorkbookAsync(client, "first.xlsx");
        var second = await UploadWorkbookAsync(client, "second.xlsx");

        var response = await client.GetAsync("/api/uploads");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<UploadListItem>>();
        list!.Select(u => u.Id).Should().ContainInOrder(second, first); // newest first
        list.Should().OnlyContain(u => u.FileName.Length > 0 && u.UploadedAt != default);

        // Raw file content must not be serialized into the list payload.
        var raw = await response.Content.ReadAsStringAsync();
        raw.ToLowerInvariant().Should().NotContain("\"content\"");
    }

    [Fact]
    public async Task List_is_empty_when_nothing_uploaded()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var response = await client.GetAsync("/api/uploads");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<UploadListItem>>();
        list!.Should().BeEmpty();
    }

    [Fact]
    public async Task List_requires_authentication()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient(); // no auth headers

        var response = await client.GetAsync("/api/uploads");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Findings_returns_the_latest_run_in_four_sections()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client, "orbit.xlsx");
        await client.PostAsync($"/api/analyze/{uploadId}", content: null);

        var response = await client.GetAsync($"/api/uploads/{uploadId}/findings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<UploadFindingsView>();

        view!.Findings.Should().NotBeEmpty();
        view.Narrative.Should().ContainSingle();
        view.Challenge.Should().ContainSingle();
        view.Review.Should().ContainSingle();
        var all = view.Findings.Concat(view.Narrative).Concat(view.Challenge).Concat(view.Review);
        all.Should().OnlyContain(f => f.Citation.UploadId == uploadId);
    }

    [Fact]
    public async Task Findings_shows_only_the_latest_run_when_reanalyzed()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client, "orbit.xlsx");
        await client.PostAsync($"/api/analyze/{uploadId}", content: null);
        var second = await (await client.PostAsync($"/api/analyze/{uploadId}", null))
            .Content.ReadFromJsonAsync<AnalyzeResponse>();

        var view = await (await client.GetAsync($"/api/uploads/{uploadId}/findings"))
            .Content.ReadFromJsonAsync<UploadFindingsView>();

        var all = view!.Findings.Concat(view.Narrative).Concat(view.Challenge).Concat(view.Review);
        all.Should().OnlyContain(f => f.RunId == second!.RunId);
    }

    [Fact]
    public async Task Findings_for_known_upload_without_findings_returns_empty_sections()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        // Uploaded but never analyzed → known upload, zero findings.
        var uploadId = await UploadWorkbookAsync(client, "orbit.xlsx");

        var response = await client.GetAsync($"/api/uploads/{uploadId}/findings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<UploadFindingsView>();
        view!.Findings.Should().BeEmpty();
        view.Narrative.Should().BeEmpty();
        view.Challenge.Should().BeEmpty();
        view.Review.Should().BeEmpty();
    }

    [Fact]
    public async Task Findings_for_unknown_upload_returns_not_found()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var response = await client.GetAsync($"/api/uploads/{Guid.NewGuid()}/findings");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Findings_requires_authentication()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient(); // no auth headers

        var response = await client.GetAsync($"/api/uploads/{Guid.NewGuid()}/findings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<Guid> UploadWorkbookAsync(HttpClient client, string fileName)
    {
        var bytes = OrbitFixtureBuilder.Workbook();
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        content.Add(file, "file", fileName);

        var response = await client.PostAsync("/api/ingest/upload", content);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        return result!.UploadId;
    }

    private sealed record UploadResponse(Guid UploadId, string FileName);
    private sealed record UploadListItem(Guid Id, string FileName, DateTimeOffset UploadedAt);
    private sealed record AnalyzeResponse(Guid RunId);
    private sealed record UploadFindingsView(
        Guid UploadId, List<ReadFinding> Findings, List<ReadFinding> Narrative,
        List<ReadFinding> Challenge, List<ReadFinding> Review);
    private sealed record ReadFinding(Guid Id, string Kind, Guid RunId, CitationResponse Citation);
    private sealed record CitationResponse(Guid UploadId, string Locator);
}
