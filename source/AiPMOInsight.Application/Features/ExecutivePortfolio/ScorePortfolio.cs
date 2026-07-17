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
        IReadOnlyList<InterventionView> Intervention,
        FinancialExposureView FinancialExposure,
        int DecisionBacklog,
        IReadOnlyList<KeyPersonView> KeyPersons,
        IReadOnlyList<CustomerExposureView> CustomerExposure,
        IReadOnlyList<RecommendedActionView> RecommendedActions);

    /// <summary>One project needing intervention: its status colour, confidence, and a cited reason.</summary>
    public sealed record InterventionView(
        string ProjectKey,
        string Status,
        double Confidence,
        string Reason,
        string CitationLocator);

    /// <summary>Portfolio financial exposure — the summed forecast-overrun amount + its currency.</summary>
    public sealed record FinancialExposureView(decimal Amount, string? Currency);

    /// <summary>A person concentrated across many projects: the count and the worst RAG band seen.</summary>
    public sealed record KeyPersonView(string Person, int ProjectCount, string Status);

    /// <summary>Relationship exposure: a customer and how many of its projects are at risk (Red/Amber).</summary>
    public sealed record CustomerExposureView(string Customer, int AtRiskCount);

    /// <summary>Leadership to-do: an at-risk project's recommended next action, with owner + deadline.</summary>
    public sealed record RecommendedActionView(
        string ProjectKey, string Status, string Action, string Owner, string Deadline);

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

            // Additional backed L1 signals (slices D/E). Each reads the latest run's findings — the same
            // run the score used — so nothing double-counts across historical runs.
            var latest = scored
                .Select(s => s.Findings.Where(f => f.RunId == s.Score.RunId).ToList())
                .ToList();
            var latestFindings = latest.SelectMany(f => f).ToList();

            // Financial exposure: sum the amount metric the Financial agent stamps on its exposure finding.
            var exposureFindings = latestFindings
                .Where(f => f.ProducingAgent == "Financial" && f.MetricValue is not null)
                .ToList();
            var exposure = new FinancialExposureView(
                exposureFindings.Sum(f => f.MetricValue!.Value),
                exposureFindings.Select(f => f.MetricUnit).FirstOrDefault(u => u is not null));

            // Decision backlog: count of Decision-area findings (overdue / due-soon) across latest runs.
            var decisionBacklog = latestFindings.Count(f => f.Area == HealthArea.Decision);

            // Key-person concentration: distinct people from the Resource agent's concentration findings
            // (person on MetricDetail, project count on MetricValue), worst band per person.
            var keyPersons = latestFindings
                .Where(f => f.ProducingAgent == "Resource" && f.MetricDetail is not null
                            && f.MetricDetail.ContainsKey("person"))
                .GroupBy(f => f.MetricDetail!["person"], StringComparer.OrdinalIgnoreCase)
                .Select(g => new KeyPersonView(
                    g.Key,
                    (int)g.Max(f => f.MetricValue ?? 0),
                    g.Max(f => f.Severity ?? Severity.Green).ToString()))
                .OrderByDescending(k => k.ProjectCount)
                .ToList();

            // Customer exposure: at-risk (Red/Amber) projects grouped by customer, read from each project's
            // Narrative finding (the one finding guaranteed per analyzed project). A labelled relationship
            // proxy — NOT true commercial risk (which needs contract/margin/SLA data the findings lack).
            var customerExposure = scored
                .Where(s => s.Score.FinalBucket is Severity.Red or Severity.Amber)
                .Select(s => s.Findings.FirstOrDefault(f =>
                    f.RunId == s.Score.RunId && f.Kind == FindingKind.Narrative
                    && f.MetricDetail is not null && f.MetricDetail.ContainsKey("customer"))?.MetricDetail?["customer"])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .GroupBy(c => c!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new CustomerExposureView(g.Key, g.Count()))
                .OrderByDescending(c => c.AtRiskCount)
                .ToList();

            // Recommended actions (#7): for each at-risk project, surface the recommendation the Narrative
            // agent stamped on its narrative finding (owner/deadline/action, #48) — the leadership to-do
            // list. Worst-first, matching the intervention ordering. Green projects contribute nothing.
            var recommendedActions = scored
                .Where(s => s.Score.FinalBucket is Severity.Red or Severity.Amber)
                .OrderByDescending(s => (int)s.Score.FinalBucket)
                .ThenBy(s => s.Score.RawScore)
                .Select(s =>
                {
                    var rec = s.Findings.FirstOrDefault(f =>
                        f.RunId == s.Score.RunId && f.Kind == FindingKind.Narrative
                        && f.MetricDetail is not null && f.MetricDetail.ContainsKey("action"))?.MetricDetail;
                    return rec is null
                        ? null
                        : new RecommendedActionView(
                            s.Score.ProjectKey, s.Score.FinalBucket.ToString(),
                            rec.GetValueOrDefault("action", ""), rec.GetValueOrDefault("owner", ""),
                            rec.GetValueOrDefault("deadline", ""));
                })
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList();

            return new Result(red, amber, green, needsReview, avgConfidence, intervention,
                exposure, decisionBacklog, keyPersons, customerExposure, recommendedActions);
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
