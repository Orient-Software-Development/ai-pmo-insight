using System.Text;
using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using AiPMOInsight.Domain.Findings;
using AiPMOInsight.Domain.Ingest;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using AiPMOInsight.Infrastructure.Analysis.Parsing;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Control-flow + provenance tests for the orchestrator against the real deterministic agents and
/// the <see cref="FakeLlmClient"/> — no host, no network.
/// </summary>
public class AnalysisOrchestratorTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryFindings : IFindingRepository
    {
        public List<Finding> Saved { get; } = [];

        public Task AddAsync(Finding finding, CancellationToken cancellationToken)
        {
            Saved.Add(finding);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Finding> findings, CancellationToken cancellationToken)
        {
            Saved.AddRange(findings);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Finding>> GetByProjectKeyAsync(string projectKey, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Finding>>(Saved.Where(f => f.ProjectKey == projectKey).ToList());

        public Task<IReadOnlyList<Finding>> GetByUploadIdAsync(Guid uploadId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Finding>>(Saved.Where(f => f.Citation.UploadId == uploadId).ToList());

        public Task<IReadOnlyList<string>> DistinctProjectKeysAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(Saved.Select(f => f.ProjectKey).Distinct().ToList());
    }

    /// <summary>Wraps the fake client to record the order agents invoke the LLM, so control flow is observable.</summary>
    private sealed class RecordingLlmClient(ILlmClient inner) : ILlmClient
    {
        public List<string> Calls { get; } = [];

        public Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
            where TOutput : notnull
        {
            Calls.Add(request.SkillName);
            return inner.CompleteAsync<TOutput>(request, cancellationToken);
        }
    }

    private static AnalysisOrchestrator Build(IFindingRepository repo, ILlmClient? llm = null)
    {
        llm ??= new FakeLlmClient(FakeLlmFixtures.Default());
        var prompts = PromptRegistry.FromEmbeddedResources();
        return new AnalysisOrchestrator(
            new DataCollectorSkill(new UploadParser()),
            new DataQualitySkill(new DataQualityOptions()),
            new StatusSkill(),
            new RiskAndIssueSkill(llm, prompts),
            new FinancialSkill(),
            new ResourceSkill(),
            new DecisionSkill(),
            new ScopeSkill(),
            new NarrativeSkill(llm, prompts),
            new ChallengeSkill(llm, prompts),
            new ReviewSkill(llm, prompts),
            repo,
            new FixedClock(new DateTimeOffset(2026, 07, 10, 0, 0, 0, TimeSpan.Zero)));
    }

    private static Upload Workbook() =>
        Upload.Create("orbit.xlsx", OrbitFixtureBuilder.Workbook(), DateTimeOffset.UtcNow);

    [Fact]
    public async Task Runs_the_full_pipeline_and_persists_cited_findings_under_one_run()
    {
        var repo = new InMemoryFindings();

        var result = await Build(repo).RunAsync(Workbook(), CancellationToken.None);

        repo.Saved.Should().NotBeEmpty();
        repo.Saved.Should().OnlyContain(f => f.Citation.Locator.Length > 0);
        repo.Saved.Should().OnlyContain(f => f.RunId == result.RunId);
        repo.Saved.Should().OnlyContain(f => f.ProjectKey == "ALPHA");

        // The trust layer is present.
        repo.Saved.Should().Contain(f => f.Kind == FindingKind.Narrative);
        repo.Saved.Should().Contain(f => f.Kind == FindingKind.Challenge);
        repo.Saved.Should().Contain(f => f.Kind == FindingKind.Review);
        // A deterministic analysis finding the fixture is designed to trigger.
        repo.Saved.Should().Contain(f => f.ProducingAgent == "Financial");
    }

    [Fact]
    public async Task Falls_back_to_an_upload_key_when_the_source_has_no_project_id()
    {
        var repo = new InMemoryFindings();
        var upload = Upload.Create("notes.txt", Encoding.UTF8.GetBytes("no structured data here"), DateTimeOffset.UtcNow);

        await Build(repo).RunAsync(upload, CancellationToken.None);

        repo.Saved.Should().NotBeEmpty();
        repo.Saved.Should().OnlyContain(f => f.ProjectKey == $"upload:{upload.Id}");
        repo.Saved.Should().Contain(f => f.Kind == FindingKind.Narrative);
    }

    [Fact]
    public async Task Re_analysis_appends_under_a_new_run_and_retains_the_prior_run()
    {
        var repo = new InMemoryFindings();
        var orchestrator = Build(repo);
        var upload = Workbook();

        var first = await orchestrator.RunAsync(upload, CancellationToken.None);
        var second = await orchestrator.RunAsync(upload, CancellationToken.None);

        second.RunId.Should().NotBe(first.RunId);
        repo.Saved.Select(f => f.RunId).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Runs_the_dependent_llm_stages_in_narrative_then_challenge_then_review_order()
    {
        var repo = new InMemoryFindings();
        var recording = new RecordingLlmClient(new FakeLlmClient(FakeLlmFixtures.Default()));

        await Build(repo, recording).RunAsync(Workbook(), CancellationToken.None);

        // The workbook carries no meeting minutes, so #4 stays fully deterministic and never
        // touches the LLM (the hybrid gate).
        recording.Calls.Should().NotContain("RiskAndIssue");

        // #7 → #8 → #9 are dependent and must invoke the LLM in that order.
        recording.Calls.Should().ContainInOrder("Narrative", "Challenge", "Review");
    }

    [Fact]
    public async Task Fans_out_across_all_four_analysis_agents_over_the_shared_records()
    {
        var repo = new InMemoryFindings();

        await Build(repo).RunAsync(Workbook(), CancellationToken.None);

        var analysisAgents = repo.Saved
            .Where(f => f.Kind == FindingKind.Analysis)
            .Select(f => f.ProducingAgent)
            .Distinct();

        // #2 Data Quality feeds the four independent analysis agents (#3 Status, #4 Risk & Issue,
        // #5 Financial, #6 Resource); each produces findings over the same shared record set in one run.
        analysisAgents.Should().Contain(["Status", "Financial", "Resource", "RiskAndIssue"]);
    }
}
