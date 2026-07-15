using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Phase 5 Level-2 dashboard data path (add-project-status-dashboard). The rich project-status view
/// stitches two existing read surfaces for one project key: the findings surface
/// (<c>GET /api/projects/{key}</c>) and the health surface (<c>GET /api/projects/{key}/health</c>).
/// These tests lock the contract the view depends on — a single upload → analyze makes BOTH surfaces
/// populated for the same key — and the defined "unknown project" outcome (health 404 + empty findings).
/// No backend change is introduced; this pins the data path so the client wiring has a stable target.
/// </summary>
public class ProjectStatusDashboardDataTests
{
    [Fact]
    public async Task After_analysis_both_the_findings_and_health_surfaces_are_populated_for_the_same_key()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client);
        await client.PostAsync($"/api/analyze/{uploadId}", content: null);

        // Findings surface: the four Level-2 sections, each cited.
        var findings = await client.GetAsync("/api/projects/ALPHA");
        findings.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await findings.Content.ReadFromJsonAsync<ProjectView>();
        view!.Findings.Should().NotBeEmpty();
        view.Narrative.Should().NotBeEmpty();
        view.Challenge.Should().NotBeEmpty();
        view.Review.Should().NotBeEmpty();

        // Health surface: a scored, auditable result the banner renders.
        var health = await client.GetAsync("/api/projects/ALPHA/health");
        health.StatusCode.Should().Be(HttpStatusCode.OK);
        var score = await health.Content.ReadFromJsonAsync<ScoreResponse>();
        score!.ProjectKey.Should().Be("ALPHA");
        score.Score.Should().NotBeNull();
        score.Score!.FinalBucket.Should().BeOneOf("Green", "Amber", "Red");
        score.Score.Areas.Should().NotBeEmpty();
        // The audit fields the L2 banner surfaces are all present in the payload.
        score.Score.Confidence.Should().BeGreaterThanOrEqualTo(0);
        score.Score.AppliedOverrides.Should().NotBeNull();
    }

    [Fact]
    public async Task Unknown_project_yields_health_not_found_and_empty_findings()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var unknown = $"does-not-exist-{Guid.NewGuid()}";

        var health = await client.GetAsync($"/api/projects/{unknown}/health");
        health.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var findings = await client.GetAsync($"/api/projects/{unknown}");
        findings.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await findings.Content.ReadFromJsonAsync<ProjectView>();
        view!.Findings.Should().BeEmpty();
        view.Narrative.Should().BeEmpty();
        view.Challenge.Should().BeEmpty();
        view.Review.Should().BeEmpty();
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
    private sealed record ProjectView(
        string ProjectKey, List<ReadFinding> Findings, List<ReadFinding> Narrative,
        List<ReadFinding> Challenge, List<ReadFinding> Review);
    private sealed record ReadFinding(Guid Id, string ProjectKey, string Summary, string Kind);
    private sealed record ScoreResponse(string ProjectKey, ScoreView? Score);
    private sealed record ScoreView(
        Guid RunId, double RawScore, string RawBucket, string FinalBucket, bool NeedsPmReview,
        double Confidence, List<AreaView> Areas, List<OverrideView> AppliedOverrides);
    private sealed record AreaView(string Area, string Severity, int Weight, double Contribution);
    private sealed record OverrideView(string RuleId, string Floor, string Reason, Guid FindingId, string CitationLocator);
}
