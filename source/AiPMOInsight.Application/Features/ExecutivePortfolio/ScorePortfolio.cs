using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Application.Messaging;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.ExecutivePortfolio;

/// <summary>
/// Vertical slice: the Level-1 executive portfolio roll-up (a read-side query, decoupled from analysis).
/// Enumerates every project on record (<see cref="IFindingRepository.DistinctProjectKeysAsync"/>), scores
/// each via the existing pure <see cref="HealthScoringService"/> (its latest run — no re-analysis, no LLM),
/// and aggregates: the count of projects in each RAG bucket, the count flagged "Needs PM Review", an
/// aggregate (mean) confidence, and a worst-first list of the projects needing intervention (Red/Amber),
/// each with a cited reason. Enums surface as strings, matching the other read surfaces.
/// </summary>
public static class ScorePortfolio
{
    public sealed record Query : IRequest<Result>;

    public sealed record Result(
        int Red,
        int Amber,
        int Green,
        int NeedsPmReview,
        double AverageConfidence,
        IReadOnlyList<InterventionView> Intervention);

    /// <summary>One project needing intervention: its status colour, confidence, and a cited reason.</summary>
    public sealed record InterventionView(
        string ProjectKey,
        string Status,
        double Confidence,
        string Reason,
        string CitationLocator);

    internal sealed class Handler(IFindingRepository findings, HealthScoringService scoring)
        : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            var keys = await findings.DistinctProjectKeysAsync(cancellationToken);

            // Score each project via the same call ScoreProject makes, so the portfolio can never
            // disagree with the per-project view. Keep the findings alongside the score to derive a
            // cited reason for the raw-score path.
            var scored = new List<(HealthScore Score, IReadOnlyList<Finding> Findings)>();
            foreach (var key in keys)
            {
                var projectFindings = await findings.GetByProjectKeyAsync(key, cancellationToken);
                var score = scoring.Score(key, projectFindings);
                if (score is not null)
                {
                    scored.Add((score, projectFindings));
                }
            }

            var red = scored.Count(s => s.Score.FinalBucket == Severity.Red);
            var amber = scored.Count(s => s.Score.FinalBucket == Severity.Amber);
            var green = scored.Count(s => s.Score.FinalBucket == Severity.Green);
            var needsReview = scored.Count(s => s.Score.NeedsPmReview);
            var avgConfidence = scored.Count == 0 ? 0d : scored.Average(s => s.Score.Confidence);

            var intervention = scored
                .Where(s => s.Score.FinalBucket is Severity.Red or Severity.Amber)
                // Worst-first: Red before Amber (higher severity ordinal first), then lowest raw score first.
                .OrderByDescending(s => (int)s.Score.FinalBucket)
                .ThenBy(s => s.Score.RawScore)
                .Select(s => ToIntervention(s.Score, s.Findings))
                .ToList();

            return new Result(red, amber, green, needsReview, avgConfidence, intervention);
        }

        /// <summary>
        /// Derives the intervention reason + citation (design Decision 4): prefer the worst-floor applied
        /// override (most specific, already cited); otherwise the worst-severity area, cited to the worst
        /// Analysis finding in that area — so every entry is cited even when no override fired.
        /// </summary>
        private static InterventionView ToIntervention(HealthScore score, IReadOnlyList<Finding> projectFindings)
        {
            var status = score.FinalBucket.ToString();

            var worstOverride = score.AppliedOverrides
                .OrderByDescending(o => (int)o.Floor)
                .FirstOrDefault();
            if (worstOverride is not null)
            {
                return new InterventionView(
                    score.ProjectKey, status, score.Confidence, worstOverride.Reason, worstOverride.CitationLocator);
            }

            // Raw-score path: the worst area (highest severity, weight as tiebreak) and its worst finding.
            var worstArea = score.Areas
                .OrderByDescending(a => (int)a.Severity)
                .ThenByDescending(a => a.Weight)
                .First();

            var citingFinding = projectFindings
                .Where(f => f.RunId == score.RunId
                            && f.Kind == FindingKind.Analysis
                            && f.Area == worstArea.Area)
                .OrderByDescending(f => (int)(f.Severity ?? Severity.Green))
                .FirstOrDefault();

            var reason = $"{worstArea.Area} at {worstArea.Severity}";
            var locator = citingFinding?.Citation.Locator ?? string.Empty;
            return new InterventionView(score.ProjectKey, status, score.Confidence, reason, locator);
        }
    }
}
