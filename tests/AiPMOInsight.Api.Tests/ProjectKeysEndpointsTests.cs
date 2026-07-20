using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Integration tests for the project-keys enumeration endpoint (<c>GET /api/projects</c>) that feeds
/// the L2 project switcher dropdown. Shared-workspace visibility: any authenticated caller sees all
/// project keys.
/// </summary>
public class ProjectKeysEndpointsTests
{
    [Fact]
    public async Task Returns_distinct_keys_after_an_analysis()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client, "orbit.xlsx");
        await client.PostAsync($"/api/analyze/{uploadId}", content: null);

        var response = await client.GetAsync("/api/projects");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<string>>();
        list!.Should().NotBeEmpty();
        list.Should().OnlyHaveUniqueItems();
        list.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Returns_empty_when_nothing_analyzed()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var response = await client.GetAsync("/api/projects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<string>>();
        list!.Should().BeEmpty();
    }

    [Fact]
    public async Task Requires_authentication()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient(); // no auth headers

        var response = await client.GetAsync("/api/projects");

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
}
