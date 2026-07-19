namespace AiPMOInsight.Application.Features.DataQuality;

/// <summary>
/// The shared vocabulary of keys/values a <c>DataQuality</c>-area <see cref="Domain.Findings.Finding"/>
/// carries in its <c>MetricDetail</c> bag. <see cref="Analysis.Agents.DataQualitySkill"/> (producer) and
/// <see cref="SummarizeDataQuality"/> / <see cref="HealthScoring.HealthScoringService"/> (consumers) all
/// reference these constants instead of duplicating the magic strings, so a typo in one place can't
/// silently break scoring exclusion or item grouping in another.
/// </summary>
public static class DataQualityFindingKeys
{
    /// <summary>Discriminates a finding's structured <c>MetricDetail</c> payload shape.</summary>
    public const string Kind = "kind";

    /// <summary>The DQ signal component a plain missing/stale/orphan finding represents.</summary>
    public const string SignalKind = "signalKind";

    public const string Remediation = "remediation";
    public const string Candidate = "candidate";
    public const string CandidateName = "candidateName";
    public const string Score = "score";

    /// <summary>Values for <see cref="Kind"/>.</summary>
    public static class Kinds
    {
        public const string DuplicateCandidate = "duplicate-candidate";
        public const string CompletenessGrid = "completeness-grid";
    }

    /// <summary>Values for <see cref="SignalKind"/>.</summary>
    public static class SignalKinds
    {
        public const string Missing = "missing";
        public const string Stale = "stale";
        public const string Orphan = "orphan";
        public const string None = "none";
    }
}
