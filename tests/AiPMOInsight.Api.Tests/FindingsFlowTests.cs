using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// End-to-end flow through the real HTTP + orchestrator + agents (fake LLM) + EF (in-memory):
/// upload a dummy Orbit-shaped workbook → analyze → read the project's cited findings plus the
/// narrative / challenge / review trust layer (the four Level-2 sections).
/// </summary>
public class FindingsFlowTests
{
    [Fact]
    public async Task Analyze_drives_the_pipeline_and_returns_cited_findings_plus_the_trust_layer()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client);

        var analyze = await client.PostAsync($"/api/analyze/{uploadId}", content: null);
        analyze.StatusCode.Should().Be(HttpStatusCode.OK);
        var analyzed = await analyze.Content.ReadFromJsonAsync<AnalyzeResponse>();

        analyzed!.RunId.Should().NotBeEmpty();
        analyzed.Findings.Should().NotBeEmpty();
        analyzed.Findings.Should().OnlyContain(f => f.Citation.UploadId == uploadId && f.Citation.Locator.Length > 0);
        analyzed.Findings.Should().OnlyContain(f => f.ProjectKey == "ALPHA");
        analyzed.Findings.Should().Contain(f => f.Kind == "Narrative");
        analyzed.Findings.Should().Contain(f => f.Kind == "Challenge");
        analyzed.Findings.Should().Contain(f => f.Kind == "Review");
        analyzed.Findings.Should().Contain(f => f.Kind == "Analysis" && f.ProducingAgent == "Financial");
    }

    [Fact]
    public async Task Read_returns_the_four_sections_each_cited()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client);
        await client.PostAsync($"/api/analyze/{uploadId}", content: null);

        var read = await client.GetAsync("/api/projects/ALPHA");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await read.Content.ReadFromJsonAsync<ProjectView>();

        view!.Findings.Should().NotBeEmpty();
        view.Narrative.Should().ContainSingle();
        view.Challenge.Should().ContainSingle();
        view.Review.Should().ContainSingle();

        var all = view.Findings.Concat(view.Narrative).Concat(view.Challenge).Concat(view.Review);
        all.Should().OnlyContain(f => f.Citation.UploadId == uploadId && f.Citation.Locator.Length > 0);
        view.Narrative[0].PromptVersion.Should().StartWith("sha256:");
    }

    [Fact]
    public async Task Read_exposes_area_severity_and_structured_decision_detail()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client);
        await client.PostAsync($"/api/analyze/{uploadId}", content: null);

        var view = await (await client.GetAsync("/api/projects/ALPHA")).Content.ReadFromJsonAsync<ProjectView>();

        // Every analytic finding is self-describing: the read API exposes its health area + severity so the
        // L2 view can group by area and render the decisions/milestones panels.
        view!.Findings.Should().OnlyContain(f => f.Area != null && f.Severity != null);

        // The overdue decision surfaces structured title/owner/deadline/consequence for the Decisions panel.
        var decision = view.Findings.Should().ContainSingle(f => f.ProducingAgent == "Decision").Which;
        decision.Severity.Should().Be("Red");
        decision.MetricDetail.Should().NotBeNull();
        decision.MetricDetail!["title"].Should().Be("Approve revised go-live date");
        decision.MetricDetail["owner"].Should().Be("Steering Committee");
        decision.MetricDetail["deadline"].Should().Be("2026-06-20");
        decision.MetricDetail["consequence"].Should().Be("Cutover cannot be scheduled; team idle");
    }

    [Fact]
    public async Task Re_analysis_appends_a_new_run_and_the_read_shows_the_latest()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var uploadId = await UploadWorkbookAsync(client);

        var first = await (await client.PostAsync($"/api/analyze/{uploadId}", null)).Content.ReadFromJsonAsync<AnalyzeResponse>();
        var second = await (await client.PostAsync($"/api/analyze/{uploadId}", null)).Content.ReadFromJsonAsync<AnalyzeResponse>();
        second!.RunId.Should().NotBe(first!.RunId);

        var view = await (await client.GetAsync("/api/projects/ALPHA")).Content.ReadFromJsonAsync<ProjectView>();
        // The Level-2 view surfaces the latest run.
        view!.Narrative[0].RunId.Should().Be(second.RunId);
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
        var read = await client.GetAsync("/api/projects/ALPHA");

        upload.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        analyze.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        read.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Read_unanalyzed_project_returns_empty_sections()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        var response = await client.GetAsync($"/api/projects/does-not-exist-{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<ProjectView>();
        view!.Findings.Should().BeEmpty();
        view.Narrative.Should().BeEmpty();
        view.Challenge.Should().BeEmpty();
        view.Review.Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_upload_is_rejected()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("pmo-user");

        using var content = new MultipartFormDataContent();
        using var empty = new ByteArrayContent([]);
        content.Add(empty, "file", "empty.xlsx");

        var response = await client.PostAsync("/api/ingest/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
    private sealed record AnalyzeResponse(Guid RunId, List<AnalyzeFinding> Findings);
    private sealed record AnalyzeFinding(string ProjectKey, string Kind, string ProducingAgent, CitationResponse Citation);
    private sealed record ProjectView(
        string ProjectKey, List<ReadFinding> Findings, List<ReadFinding> Narrative,
        List<ReadFinding> Challenge, List<ReadFinding> Review);
    private sealed record ReadFinding(
        Guid Id, string ProjectKey, string Summary, string Kind, string Confidence,
        string ProducingAgent, string? PromptVersion, Guid RunId, CitationResponse Citation,
        string? Area, string? Severity, decimal? MetricValue, string? MetricUnit,
        Dictionary<string, string>? MetricDetail);
    private sealed record CitationResponse(Guid UploadId, string Locator);
}
