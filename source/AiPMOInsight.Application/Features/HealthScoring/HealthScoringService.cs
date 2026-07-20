using AiPMOInsight.Application.Features.DataQuality;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.HealthScoring;

/// <summary>
/// Computes a project's auditable RAG health score from its persisted findings — pure and
/// deterministic (no LLM, no clock, no randomness): the same findings + configuration always yield
/// the same score. It runs as an on-demand query (see <c>ScoreProject</c>), decoupled from the
/// analysis pipeline, so re-tuning the <see cref="HealthScoringOptions"/> re-scores the whole
/// portfolio without re-running (re-paying for) analysis.
///
/// <para>Model: resolve the <b>latest run</b> per project (newest <c>CreatedAt</c>), keep its
/// <see cref="FindingKind.Analysis"/> findings that carry an <see cref="HealthArea"/> +
/// <see cref="Severity"/>, group by area, reduce each area to its <b>worst</b> severity, map to a
/// numeric via the configured Severity→number table, and take a weight-normalised weighted average
/// over the areas actually present (absent areas don't dilute). Bucket by the configured inclusive
/// lower-bound thresholds. Then apply override rules as a <b>worst-case floor</b> and flag
/// "Needs PM Review" when aggregate confidence is below the configured floor.</para>
/// </summary>
public sealed class HealthScoringService(HealthScoringOptions options)
{
    /// <summary>
    /// Scores a project from the full set of its findings (all runs). Resolves the latest run and
    /// scores only its scoreable Analysis findings. Returns <c>null</c> when the latest run has no
    /// scoreable Analysis finding (nothing to score) — the caller decides how to surface that.
    /// </summary>
    public HealthScore? Score(string projectKey, IReadOnlyList<Finding> projectFindings)
    {
        ArgumentNullException.ThrowIfNull(projectFindings);

        if (projectFindings.Count == 0)
        {
            return null;
        }

        // Latest run only (re-analysis appends under a new RunId; older runs stay for history).
        // Resolve within THIS project's findings, so a run spanning multiple projects still keys per
        // project (design R4).
        var latestRunId = projectFindings.MaxBy(f => f.CreatedAt)!.RunId;
        var scoreable = projectFindings
            .Where(f => f.RunId == latestRunId
                        && f.Kind == FindingKind.Analysis
                        && f is { Area: not null, Severity: not null }
                        // Scope is display-only (POC): its findings render in the L2 key-deviations view
                        // but must not move the score or the confidence average until the PMO agrees a
                        // real Scope weight + RAG rule at kickoff.
                        && f.Area != HealthArea.Scope
                        // The areas-completeness grid (L3 #7) is an informational summary, not a scored gap.
                        && f.MetricDetail?.GetValueOrDefault(DataQualityFindingKeys.Kind) != DataQualityFindingKeys.Kinds.CompletenessGrid)
            .ToList();

        if (scoreable.Count == 0)
        {
            return null;
        }

        // Per-area worst severity → weighted, weight-normalised average.
        var areas = scoreable
            .GroupBy(f => f.Area!.Value)
            .Select(g =>
            {
                var worst = g.Min(f => f.Severity!.Value);
                var weight = options.WeightFor(g.Key);
                var areaScore = options.ScoreFor(worst);
                return (Area: g.Key, Severity: worst, Weight: weight, AreaScore: areaScore);
            })
            .OrderBy(a => a.Area)
            .ToList();

        var totalWeight = areas.Sum(a => a.Weight);
        var rawScore = totalWeight == 0
            ? 0d
            : areas.Sum(a => (double)a.AreaScore * a.Weight) / totalWeight;

        var contributions = areas
            .Select(a => new AreaContribution(
                a.Area,
                a.Severity,
                a.Weight,
                totalWeight == 0 ? 0d : (double)a.AreaScore * a.Weight / totalWeight))
            .ToList();

        var rawBucket = Bucket(rawScore);

        // Overrides — worst-case floor. Each rule fires when a finding in its area is at least the
        // configured severity; the most severe floor wins. A floor never lowers the bucket.
        var applied = ApplyOverrides(scoreable);
        var finalBucket = rawBucket;
        foreach (var o in applied)
        {
            finalBucket = Worst(finalBucket, o.Floor);
        }

        // Aggregate confidence over the scored findings → "Needs PM Review" when below the floor.
        var confidence = scoreable.Average(f => (double)options.ScoreFor(f.Confidence));
        var needsPmReview = confidence < options.ConfidenceFloor;

        return new HealthScore(
            projectKey,
            latestRunId,
            rawScore,
            rawBucket,
            finalBucket,
            needsPmReview,
            confidence,
            contributions,
            applied);
    }

    /// <summary>Maps a 0–100 score to a RAG bucket by the configured inclusive lower-bound thresholds.</summary>
    private Severity Bucket(double score)
    {
        if (score >= options.Thresholds.Green)
        {
            return Severity.Green;
        }

        return score >= options.Thresholds.Amber ? Severity.Amber : Severity.Red;
    }

    /// <summary>
    /// Evaluates the configured override rules in list order (deterministic). A rule fires when any
    /// scored finding in its area has a severity at least the rule's threshold; the tripping finding
    /// is the worst-severity such finding. Rules whose signal is absent do not fire (no synthetic
    /// warning).
    /// </summary>
    private List<AppliedOverride> ApplyOverrides(IReadOnlyList<Finding> scoreable)
    {
        var applied = new List<AppliedOverride>();

        foreach (var rule in options.Overrides)
        {
            var area = rule.AreaEnum;
            var atLeast = rule.WhenSeverityAtLeastEnum;

            var tripping = scoreable
                .Where(f => f.Area == area && (int)f.Severity!.Value >= (int)atLeast)
                .OrderByDescending(f => (int)f.Severity!.Value)
                .FirstOrDefault();

            if (tripping is null)
            {
                continue; // absent signal — the override cannot fire.
            }

            var reason = rule.Description
                ?? $"{rule.Id}: {area} finding at severity {tripping.Severity} → floor {rule.FloorEnum}.";

            applied.Add(new AppliedOverride(
                rule.Id,
                rule.FloorEnum,
                reason,
                tripping.Id,
                tripping.Citation.Locator));
        }

        return applied;
    }

    /// <summary>The worse (higher-ordinal) of two severities — the worst-case-floor combinator.</summary>
    private static Severity Worst(Severity a, Severity b) => (int)a >= (int)b ? a : b;
}
