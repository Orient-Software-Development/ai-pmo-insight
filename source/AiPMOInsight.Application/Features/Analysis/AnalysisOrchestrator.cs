using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Domain.Findings;
using AiPMOInsight.Domain.Ingest;

namespace AiPMOInsight.Application.Features.Analysis;

/// <summary>The result of one analysis run: its identity and every finding it produced.</summary>
public sealed record AnalysisResult(Guid RunId, IReadOnlyList<Finding> Findings);

/// <summary>
/// Drives the 9-agent pipeline over one upload with the agreed data flow:
/// <c>#1 Data Collector → #2 Data Quality → parallel(#3 Status, #4 Risk &amp; Issue, #5 Financial,
/// #6 Resource) → merge → #7 Narrative → #8 Challenge → #9 Review → persist</c>. Sequential where a
/// dependency exists (#7→#8→#9), parallel where independent (#3–#6). Each run gets a fresh
/// <see cref="AnalysisRun"/> id; re-analysis appends under a new id. Findings group under a
/// <c>projectKey</c> derived from the parsed source, falling back to <c>upload:{id}</c>. Every
/// finding is rejected before persist if it lacks a citation (defence-in-depth atop
/// <see cref="Finding.Create"/>).
/// </summary>
public sealed class AnalysisOrchestrator(
    DataCollectorSkill dataCollector,
    DataQualitySkill dataQuality,
    StatusSkill status,
    RiskAndIssueSkill riskAndIssue,
    FinancialSkill financial,
    ResourceSkill resource,
    NarrativeSkill narrative,
    ChallengeSkill challenge,
    ReviewSkill review,
    IFindingRepository findings,
    TimeProvider clock)
{
    public async Task<AnalysisResult> RunAsync(Upload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);

        var run = AnalysisRun.Start(upload.Id, clock.GetUtcNow());

        // #1 Data Collector — parse once; shared by every project slice.
        var data = await dataCollector.ExecuteAsync(new UploadPayload(upload.FileName, upload.Content), cancellationToken);

        var projectKeys = data.Projects
            .Select(p => p.Key)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (projectKeys.Count == 0)
        {
            projectKeys = [$"upload:{upload.Id}"]; // deterministic fallback when no project id is present
        }

        // Projects are independent — analyse them in parallel with bounded concurrency so wall-clock
        // doesn't scale linearly with project count. The cap keeps burst pressure off vendor rate
        // limits; ordering of the returned list is preserved because Task.WhenAll returns results
        // in the same order as its inputs.
        const int maxProjectConcurrency = 2;
        using var throttle = new SemaphoreSlim(maxProjectConcurrency, maxProjectConcurrency);
        var perProject = await Task.WhenAll(projectKeys.Select(async projectKey =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                return await AnalyzeProjectAsync(run, projectKey, data, cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        }));

        var produced = new List<Finding>();
        foreach (var findings in perProject)
        {
            produced.AddRange(findings);
        }

        // Reject any uncited finding before persisting (invariant already enforced at creation).
        if (produced.Any(f => f.Citation is null || string.IsNullOrWhiteSpace(f.Citation.Locator)))
        {
            throw new InvalidOperationException("Refusing to persist a finding without a citation.");
        }

        await findings.AddRangeAsync(produced, cancellationToken);
        return new AnalysisResult(run.RunId, produced);
    }

    private async Task<IReadOnlyList<Finding>> AnalyzeProjectAsync(
        AnalysisRun run, string projectKey, Model.CollectedData data, CancellationToken cancellationToken)
    {
        var slice = new ProjectSlice { Run = run, ProjectKey = projectKey, Data = data };

        // #2 Data Quality first — its signal feeds the analysis agents' confidence.
        var quality = await dataQuality.ExecuteAsync(slice, cancellationToken);
        var input = new AnalysisInput(slice, quality.Signal);

        // #3–#6 are independent — fan out.
        var analysisResults = await Task.WhenAll(
            status.ExecuteAsync(input, cancellationToken),
            riskAndIssue.ExecuteAsync(input, cancellationToken),
            financial.ExecuteAsync(input, cancellationToken),
            resource.ExecuteAsync(input, cancellationToken));

        var merged = new List<Finding>(quality.Findings);
        foreach (var result in analysisResults)
        {
            merged.AddRange(result);
        }

        // #7 → #8 → #9 are dependent — sequential.
        var narrativeFinding = await narrative.ExecuteAsync(new NarrativeInput(slice, quality.Signal, merged), cancellationToken);
        var challengeFinding = await challenge.ExecuteAsync(new ChallengeInput(slice, quality.Signal, merged, narrativeFinding), cancellationToken);
        var reviewFinding = await review.ExecuteAsync(new ReviewInput(slice, merged, narrativeFinding, challengeFinding), cancellationToken);

        return [.. merged, narrativeFinding, challengeFinding, reviewFinding];
    }
}
