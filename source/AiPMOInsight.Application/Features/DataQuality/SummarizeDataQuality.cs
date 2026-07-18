using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis;
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
        IReadOnlyList<ProjectCountView> PerProject,
        IReadOnlyList<DuplicateView> Duplicates,
        IReadOnlyList<CompletenessView> Completeness);

    /// <summary>One project's areas-completeness row (L3 #7): category name → % complete (or "n/a"), for the
    /// 8 input categories. POC mandatory-field set — flagged, not scored.</summary>
    public sealed record CompletenessView(string ProjectKey, IReadOnlyDictionary<string, string> Categories);

    /// <summary>A duplicate-identity candidate pair (L3 #4): the project, its likely twin, a POC similarity
    /// score, and a cited source. The UI records Merge/Keep-separate — it NEVER auto-merges (US-2).</summary>
    public sealed record DuplicateView(
        string ProjectKey, string Candidate, string CandidateName, int Score, string CitationLocator);

    /// <summary>Portfolio confidence against the publish threshold: mean %, the threshold, and below-target.</summary>
    public sealed record ConfidenceView(double Mean, int Threshold, bool BelowTarget);

    /// <summary>
    /// One missing/inconsistent item: its project, the issue text, severity, a cited source, the staleness
    /// age in days (L3 #8 — null unless the finding carries one), and a suggested remediation (L3 #2).
    /// </summary>
    public sealed record ItemView(
        string ProjectKey, string Issue, string Severity, string CitationLocator, int? AgeDays, string? Remediation,
        int Lift);

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
            // Duplicate-identity candidates (L3 #4) are surfaced separately from the missing/inconsistent items.
            var duplicates = new List<(string Key, Finding Finding)>();
            // Areas-completeness grid rows (L3 #7) — one per project, kept off the items list.
            var completeness = new List<(string Key, Finding Finding)>();

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
                    switch (f.MetricDetail?.GetValueOrDefault("kind"))
                    {
                        case "duplicate-candidate": duplicates.Add((key, f)); break;
                        case "completeness-grid": completeness.Add((key, f)); break;
                        default: collected.Add((key, f)); break;
                    }
                }
            }

            var mean = scoredConfidences.Count == 0 ? 0d : scoredConfidences.Average();
            var confidence = new ConfidenceView(mean, options.ConfidenceFloor, mean < options.ConfidenceFloor);

            // Ordered by confidence lift (L3 #5) — fixing the highest-lift item helps confidence most — then
            // worst severity, then a deterministic key/locator tiebreak. Lift is computed per project by
            // reconstructing its DQ signal from the findings' signalKind tags and re-running ConfidencePolicy.
            var items = collected
                .GroupBy(x => x.Key, StringComparer.Ordinal)
                .SelectMany(g =>
                {
                    var signal = ReconstructSignal(g.Select(x => x.Finding));
                    var currentConf = (int)ConfidencePolicy.FromSignals(signal);
                    return g.Select(x => new ItemView(
                        x.Key,
                        x.Finding.Summary,
                        x.Finding.Severity!.Value.ToString(),
                        x.Finding.Citation.Locator,
                        x.Finding.MetricValue is { } age ? (int)age : null,
                        x.Finding.MetricDetail?.GetValueOrDefault("remediation"),
                        ConfidenceLift(signal, currentConf, x.Finding.MetricDetail?.GetValueOrDefault("signalKind"))));
                })
                .OrderByDescending(i => i.Lift)
                .ThenByDescending(i => SeverityRank(i.Severity))
                .ThenBy(i => i.ProjectKey, StringComparer.Ordinal)
                .ThenBy(i => i.CitationLocator, StringComparer.Ordinal)
                .ToList();

            var perProject = items
                .GroupBy(i => i.ProjectKey, StringComparer.Ordinal)
                .Select(g => new ProjectCountView(g.Key, g.Count()))
                .ToList();

            // Duplicate candidates, worst (highest score) first, cited — a separate surface for the
            // Merge/Keep-separate decision (which the UI only records; it never auto-merges, US-2).
            var duplicateViews = duplicates
                .OrderByDescending(x => x.Finding.MetricValue ?? 0)
                .ThenBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new DuplicateView(
                    x.Key,
                    x.Finding.MetricDetail!.GetValueOrDefault("candidate", ""),
                    x.Finding.MetricDetail!.GetValueOrDefault("candidateName", ""),
                    x.Finding.MetricValue is { } s ? (int)s : 0,
                    x.Finding.Citation.Locator))
                .ToList();

            // Areas-completeness grid rows (L3 #7): one per project, the category → % map (minus the marker keys).
            var completenessViews = completeness
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new CompletenessView(
                    x.Key,
                    x.Finding.MetricDetail!
                        .Where(kv => kv.Key is not ("kind" or "remediation"))
                        .ToDictionary(kv => kv.Key, kv => kv.Value)))
                .ToList();

            return new Result(confidence, items, items.Count, perProject, duplicateViews, completenessViews);
        }

        // Reconstructs a project's DQ signal from its findings' signalKind tags (the live signal isn't
        // persisted) so the confidence lift can be computed at read time (L3 #5).
        private static DataQualitySignal ReconstructSignal(IEnumerable<Finding> projectFindings)
        {
            var list = projectFindings.ToList();
            var stale = list.FirstOrDefault(f => Kind(f) == "stale");
            return new DataQualitySignal
            {
                MissingFieldCount = list.Count(f => Kind(f) == "missing"),
                LastUpdateAgeDays = stale?.MetricValue is { } a ? (double)a : 0,
                SourceConsistent = list.All(f => Kind(f) != "orphan"),
            };
        }

        // Confidence a project would gain by fixing one item (its signal component decremented). Always ≥ 0.
        private static int ConfidenceLift(DataQualitySignal current, int currentConf, string? signalKind)
        {
            var fixedSignal = signalKind switch
            {
                "missing" => current with { MissingFieldCount = Math.Max(0, current.MissingFieldCount - 1) },
                "stale" => current with { LastUpdateAgeDays = 0 },
                "orphan" => current with { SourceConsistent = true },
                _ => current,
            };
            return (int)ConfidencePolicy.FromSignals(fixedSignal) - currentConf;
        }

        private static string? Kind(Finding f) => f.MetricDetail?.GetValueOrDefault("signalKind");

        private static int SeverityRank(string severity) => severity switch
        {
            "Red" => 3,
            "Amber" => 2,
            "Green" => 1,
            _ => 0,
        };
    }
}
