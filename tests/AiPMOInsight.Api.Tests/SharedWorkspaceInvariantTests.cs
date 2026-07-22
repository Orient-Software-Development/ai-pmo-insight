using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Encodes the <b>shared-workspace, no per-user scoping</b> invariant from CLAUDE.md §5 Never
/// and CLAUDE-decisions.md ("Shared workspace, no per-user scoping"): any authenticated caller
/// sees every finding / upload / project, regardless of who created it. Adding
/// <c>.Where(x =&gt; x.UserId == currentUser)</c> anywhere on the read path breaks these tests.
///
/// This file exists to lift the invariant out of prose (advisory) and into a CI-enforced check
/// (blocking). If a future refactor accidentally re-scopes a read to the caller, these tests
/// fail loudly with a pointer back at the rule so the reviewer can decide: intentional spec
/// change (update the rule + tests), or accidental drift (fix the code).
/// </summary>
public class SharedWorkspaceInvariantTests
{
    [Fact]
    public async Task Uploads_created_by_one_user_are_visible_to_another_user()
    {
        using var factory = new TestWebAppFactory();

        // Alice uploads.
        using var alice = factory.CreateClientAs("alice");
        var aliceUploadId = await UploadWorkbookAsync(alice, "alice.xlsx");

        // Bob (a different user) lists uploads.
        using var bob = factory.CreateClientAs("bob");
        var response = await bob.GetAsync("/api/uploads");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var listAsBob = await response.Content.ReadFromJsonAsync<List<UploadListItem>>();

        listAsBob!.Select(u => u.Id).Should().Contain(aliceUploadId,
            "shared-workspace invariant (CLAUDE.md §5 Never): any authenticated caller sees " +
            "every upload, regardless of who uploaded it. If this test fails, a per-user scope " +
            "was likely added to the upload read path. See CLAUDE-decisions.md for the rule.");
    }

    [Fact]
    public async Task Findings_from_one_users_analysis_are_visible_to_another_user()
    {
        using var factory = new TestWebAppFactory();

        // Alice uploads and analyzes.
        using var alice = factory.CreateClientAs("alice");
        var uploadId = await UploadWorkbookAsync(alice, "alice.xlsx");
        var analyzeResponse = await alice.PostAsync($"/api/analyze/{uploadId}", content: null);
        analyzeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Bob reads findings for Alice's upload.
        using var bob = factory.CreateClientAs("bob");
        var response = await bob.GetAsync($"/api/uploads/{uploadId}/findings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<UploadFindingsView>();

        view!.Findings.Should().NotBeEmpty(
            "shared-workspace invariant (CLAUDE.md §5 Never): findings from any user's analysis " +
            "are visible to any other authenticated user. If this test fails, a per-user scope " +
            "was likely added to the findings read path. See CLAUDE-decisions.md for the rule.");
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
    private sealed record UploadFindingsView(
        Guid UploadId, List<ReadFinding> Findings, List<ReadFinding> Narrative,
        List<ReadFinding> Challenge, List<ReadFinding> Review);
    private sealed record ReadFinding(Guid Id);
}
