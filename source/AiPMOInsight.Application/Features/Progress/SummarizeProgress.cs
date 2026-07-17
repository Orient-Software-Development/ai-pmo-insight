using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Application.Messaging;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Progress;

/// <summary>
/// Vertical slice: "this-period progress" — a read-side run-over-run comparison for one project
/// (Level-2 panel #2). Resolves the project's two most recent runs, scores each via the pure
/// <see cref="HealthScoringService"/> (no re-analysis, no LLM), and reports the health-score movement
/// plus a concrete list of what changed: findings that cleared or improved (moved forward) and new /
/// worsened findings (moved backward). Returns <c>null</c> for an unknown project (→ 404); returns a
/// result with <see cref="Result.HasPrevious"/> = <c>false</c> when only one run exists (nothing to
/// compare yet). The qualitative <see cref="Result.Pace"/> label uses <b>POC placeholder thresholds</b>
/// (client-agreed thresholds are a kickoff follow-on) — flagged by <see cref="Result.PaceIsPlaceholder"/>.
/// </summary>
public static class SummarizeProgress
{
    // POC placeholder thresholds on the raw-score delta (points). Replace with PMO-agreed values.
    private const double DeclinedBelow = -2d;
    private const double NoMovementBelow = 2d;
    private const double SlowBelow = 8d;
    private const double MediumBelow = 15d;

    public sealed record Query(string ProjectKey) : IRequest<Result?>;

    public sealed record Result(
        string ProjectKey,
        bool HasPrevious,
        DateTimeOffset? PreviousRunAt,
        DateTimeOffset? LatestRunAt,
        double? ScoreBefore,
        double? ScoreAfter,
        double? ScoreDelta,
        string Pace,
        bool PaceIsPlaceholder,
        IReadOnlyList<ChangeView> MovedForward,
        IReadOnlyList<ChangeView> MovedBackward);

    /// <summary>One item that changed between runs: its area, what happened, the severity move, and citation.</summary>
    public sealed record ChangeView(
        string Area, string Change, string? FromSeverity, string? ToSeverity, string Summary, string CitationLocator);

    internal sealed class Handler(IFindingRepository findings, HealthScoringService scoring)
        : IRequestHandler<Query, Result?>
    {
        public async Task<Result?> Handle(Query request, CancellationToken cancellationToken)
        {
            var all = await findings.GetByProjectKeyAsync(request.ProjectKey, cancellationToken);
            if (all.Count == 0)
            {
                return null; // unknown project — nothing on record → 404
            }

            // Runs newest-first by their start time (findings in a run share the run's timestamp).
            var runs = all
                .GroupBy(f => f.RunId)
                .Select(g => new { RunId = g.Key, At = g.Min(f => f.CreatedAt), Findings = g.ToList() })
                .OrderByDescending(r => r.At)
                .ToList();

            var latest = runs[0];
            if (runs.Count < 2)
            {
                // Only one run — no prior period to compare against yet.
                return new Result(request.ProjectKey, HasPrevious: false, PreviousRunAt: null, LatestRunAt: latest.At,
                    ScoreBefore: null, ScoreAfter: null, ScoreDelta: null, Pace: "No prior run",
                    PaceIsPlaceholder: true, MovedForward: [], MovedBackward: []);
            }

            var previous = runs[1];

            // Score each run independently (Score resolves the latest run within the set it is given).
            var after = scoring.Score(request.ProjectKey, latest.Findings)?.RawScore;
            var before = scoring.Score(request.ProjectKey, previous.Findings)?.RawScore;
            var delta = after is not null && before is not null ? after - before : null;

            var (forward, backward) = Diff(previous.Findings, latest.Findings);

            return new Result(
                request.ProjectKey,
                HasPrevious: true,
                PreviousRunAt: previous.At,
                LatestRunAt: latest.At,
                ScoreBefore: before,
                ScoreAfter: after,
                ScoreDelta: delta,
                Pace: PaceLabel(delta),
                PaceIsPlaceholder: true,
                MovedForward: forward,
                MovedBackward: backward);
        }

        /// <summary>
        /// Diffs two runs' analytic findings, matched by (agent + citation locator) and reduced to the
        /// worst severity per key (one agent can emit several findings for the same source row). A key
        /// present before but not after "cleared"; a new key "appeared"; a shared key that dropped
        /// severity "improved", one that rose "worsened".
        /// </summary>
        private static (List<ChangeView> Forward, List<ChangeView> Backward) Diff(
            IReadOnlyList<Finding> previous, IReadOnlyList<Finding> latest)
        {
            var before = WorstByKey(previous);
            var after = WorstByKey(latest);
            var forward = new List<ChangeView>();
            var backward = new List<ChangeView>();

            foreach (var (key, prev) in before)
            {
                if (!after.TryGetValue(key, out var now))
                {
                    forward.Add(Change(prev, "Cleared", prev.Severity, null)); // resolved since last run
                }
                else if ((int)now.Severity!.Value < (int)prev.Severity!.Value)
                {
                    forward.Add(Change(now, "Improved", prev.Severity, now.Severity));
                }
                else if ((int)now.Severity!.Value > (int)prev.Severity!.Value)
                {
                    backward.Add(Change(now, "Worsened", prev.Severity, now.Severity));
                }
            }

            foreach (var (key, now) in after)
            {
                if (!before.ContainsKey(key))
                {
                    backward.Add(Change(now, "New", null, now.Severity)); // appeared this run
                }
            }

            return (forward, backward);
        }

        private static Dictionary<string, Finding> WorstByKey(IReadOnlyList<Finding> findings) =>
            findings
                .Where(f => f.Kind == FindingKind.Analysis && f is { Area: not null, Severity: not null })
                .GroupBy(f => $"{f.ProducingAgent}|{f.Citation.Locator}")
                .ToDictionary(g => g.Key, g => g.OrderByDescending(f => (int)f.Severity!.Value).First());

        private static ChangeView Change(Finding f, string change, Severity? from, Severity? to) =>
            new(f.Area!.Value.ToString(), change, from?.ToString(), to?.ToString(), f.Summary, f.Citation.Locator);

        private static string PaceLabel(double? delta) => delta switch
        {
            null => "Unknown",
            < DeclinedBelow => "Declined",
            < NoMovementBelow => "No movement",
            < SlowBelow => "Slow",
            < MediumBelow => "Medium",
            _ => "On track",
        };
    }
}
