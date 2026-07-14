using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Application.Features.HealthScoring;

/// <summary>
/// Vertical slice: compute a project's current health score on demand (a read-side query, decoupled
/// from analysis). Loads the project's findings, resolves the latest run, and delegates to
/// <see cref="HealthScoringService"/>. Returns <c>null</c> when the project has no findings at all
/// (the endpoint maps that to 404 — the project is unknown to the findings store); returns a
/// <see cref="Result"/> with a null <see cref="Result.Score"/> when the project has findings but none
/// are scoreable (a defined "no score yet" response, 200). Enums are surfaced as strings so the
/// audit result reads naturally (matching the findings read surface).
/// </summary>
public static class ScoreProject
{
    public sealed record Query(string ProjectKey) : IRequest<Result?>;

    public sealed record Result(string ProjectKey, ScoreView? Score);

    public sealed record ScoreView(
        Guid RunId,
        double RawScore,
        string RawBucket,
        string FinalBucket,
        bool NeedsPmReview,
        double Confidence,
        IReadOnlyList<AreaView> Areas,
        IReadOnlyList<OverrideView> AppliedOverrides);

    public sealed record AreaView(string Area, string Severity, int Weight, double Contribution);

    public sealed record OverrideView(string RuleId, string Floor, string Reason, Guid FindingId, string CitationLocator);

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

            var score = scoring.Score(request.ProjectKey, all);
            return new Result(request.ProjectKey, score is null ? null : ToView(score));
        }

        private static ScoreView ToView(HealthScore s) =>
            new(s.RunId,
                s.RawScore,
                s.RawBucket.ToString(),
                s.FinalBucket.ToString(),
                s.NeedsPmReview,
                s.Confidence,
                s.Areas.Select(a => new AreaView(a.Area.ToString(), a.Severity.ToString(), a.Weight, a.Contribution)).ToList(),
                s.AppliedOverrides.Select(o =>
                    new OverrideView(o.RuleId, o.Floor.ToString(), o.Reason, o.FindingId, o.CitationLocator)).ToList());
    }
}
