using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Application.Messaging;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.DataQuality;

/// <summary>
/// Vertical slice: the Level-3 data-quality roll-up (a read-side query, decoupled from analysis).
/// Enumerates every project on record (<see cref="IFindingRepository.DistinctProjectKeysAsync"/>) and,
/// per project, collects its <b>latest run's</b> <see cref="HealthArea.DataQuality"/> findings (the same
/// latest-run resolution the scorer uses). It rolls those up into a confidence block — the mean of each
/// scored project's aggregate <see cref="HealthScore.Confidence"/> (reusing the pure
/// <see cref="HealthScoringService"/>, so it can never disagree with the L1/L2 scores) together with the
/// configured publish threshold (<see cref="HealthScoringOptions.ConfidenceFloor"/>) and a below-target
/// flag — plus a worst-first, cited items list and per-project/total counts. No re-analysis, no LLM.
/// Enums surface as strings, matching the other read surfaces.
/// </summary>
public static class SummarizeDataQuality
{
    public sealed record Query : IRequest<Result>;

    public sealed record Result(
        ConfidenceView Confidence,
        IReadOnlyList<ItemView> Items,
        int TotalItems,
        IReadOnlyList<ProjectCountView> PerProject);

    /// <summary>Portfolio confidence against the publish threshold: mean %, the threshold, and below-target.</summary>
    public sealed record ConfidenceView(double Mean, int Threshold, bool BelowTarget);

    /// <summary>One missing/inconsistent item: its project, the issue text, severity, and a cited source.</summary>
    public sealed record ItemView(string ProjectKey, string Issue, string Severity, string CitationLocator);

    /// <summary>How many data-quality items a project has (where the gaps cluster).</summary>
    public sealed record ProjectCountView(string ProjectKey, int Count);

    internal sealed class Handler(
        IFindingRepository findings,
        HealthScoringService scoring,
        HealthScoringOptions options)
        : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            var keys = await findings.DistinctProjectKeysAsync(cancellationToken);

            var scoredConfidences = new List<double>();
            // Keep the finding alongside its key so the projection can order by the real severity enum.
            var collected = new List<(string Key, Finding Finding)>();

            foreach (var key in keys)
            {
                var projectFindings = await findings.GetByProjectKeyAsync(key, cancellationToken);

                // Confidence figure reuses the scorer, so L3 never disagrees with L1/L2 (design Decision 2);
                // unscoreable projects (null score) never widen the denominator.
                var score = scoring.Score(key, projectFindings);
                if (score is not null)
                {
                    scoredConfidences.Add(score.Confidence);
                }

                if (projectFindings.Count == 0)
                {
                    continue;
                }

                // Same latest-run resolution as the scorer (design Decision 1): older runs stay for history.
                var latestRunId = projectFindings.MaxBy(f => f.CreatedAt)!.RunId;
                foreach (var f in projectFindings.Where(f =>
                    f.RunId == latestRunId
                    && f.Kind == FindingKind.Analysis
                    && f.Area == HealthArea.DataQuality))
                {
                    collected.Add((key, f));
                }
            }

            var mean = scoredConfidences.Count == 0 ? 0d : scoredConfidences.Average();
            var confidence = new ConfidenceView(mean, options.ConfidenceFloor, mean < options.ConfidenceFloor);

            // Worst-first by severity (Red > Amber > Green), then key + locator as a deterministic tiebreak.
            var items = collected
                .OrderByDescending(x => (int)x.Finding.Severity!.Value)
                .ThenBy(x => x.Key, StringComparer.Ordinal)
                .ThenBy(x => x.Finding.Citation.Locator, StringComparer.Ordinal)
                .Select(x => new ItemView(
                    x.Key, x.Finding.Summary, x.Finding.Severity!.Value.ToString(), x.Finding.Citation.Locator))
                .ToList();

            var perProject = items
                .GroupBy(i => i.ProjectKey, StringComparer.Ordinal)
                .Select(g => new ProjectCountView(g.Key, g.Count()))
                .ToList();

            return new Result(confidence, items, items.Count, perProject);
        }
    }
}
