using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.HealthScoring;

/// <summary>
/// The auditable per-project health score (PRD user story #10). Carries the raw weighted score and
/// its RAG bucket, the ordered overrides that fired (each naming the rule and the finding/citation
/// that tripped it), the final bucket after the worst-case floor, an aggregate confidence, the
/// "Needs PM Review" status (distinct from and taking precedence over the colour), and a per-area
/// breakdown. When an override changed the bucket, both <see cref="RawBucket"/> and
/// <see cref="FinalBucket"/> are visible. "RAG"/bucket = the Red/Amber/Green health colour.
/// </summary>
public sealed record HealthScore(
    string ProjectKey,
    Guid RunId,
    double RawScore,
    Severity RawBucket,
    Severity FinalBucket,
    bool NeedsPmReview,
    double Confidence,
    IReadOnlyList<AreaContribution> Areas,
    IReadOnlyList<AppliedOverride> AppliedOverrides);

/// <summary>One area's contribution to the score: its worst severity, weight, and weighted contribution.</summary>
public sealed record AreaContribution(HealthArea Area, Severity Severity, int Weight, double Contribution);

/// <summary>
/// A fired override rule, recorded for the audit trail: the rule id, the floor it imposed, a
/// human-readable reason, and the finding + citation locator that tripped it.
/// </summary>
public sealed record AppliedOverride(string RuleId, Severity Floor, string Reason, Guid FindingId, string CitationLocator);
