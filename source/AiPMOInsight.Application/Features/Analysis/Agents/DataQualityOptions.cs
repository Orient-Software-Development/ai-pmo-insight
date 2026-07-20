namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// The externalised, swappable configuration for the L3 POC checks <see cref="DataQualitySkill"/>
/// added (per-risk staleness, duplicate-identity candidates). Mirrors the
/// <c>HealthScoringOptions</c> pattern: bound from the <c>DataQuality</c> appsettings section, the
/// shipped default carries the register's EXAMPLE placeholders (<see cref="IsPlaceholder"/> =
/// <c>true</c>) until the PMO agrees real values at kickoff. Call <see cref="Validate"/> at startup
/// to fail fast, naming the offending key.
/// </summary>
public sealed class DataQualityOptions
{
    public const string SectionName = "DataQuality";

    /// <summary>True while the values are EXAMPLE placeholders (drives the startup warning).</summary>
    public bool IsPlaceholder { get; set; } = true;

    /// <summary>A RAID item not updated within this many days is flagged stale (L3 #1, doc says 21).</summary>
    public double RiskStaleThresholdDays { get; set; } = 21;

    /// <summary>Duplicate-identity candidate score (0-100) at or above which a pair is flagged (L3 #4).</summary>
    public int DuplicateScoreThreshold { get; set; } = 60;

    /// <summary>Weights the duplicate score gives to each signal. Must sum to 100.</summary>
    public DuplicateWeightOptions DuplicateWeights { get; set; } = new();

    /// <summary>
    /// Fails fast on invalid configuration, naming the offending key. Never silently normalises.
    /// Called once at startup.
    /// </summary>
    public void Validate()
    {
        if (RiskStaleThresholdDays <= 0)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:RiskStaleThresholdDays' must be > 0; got {RiskStaleThresholdDays}.");
        }

        if (DuplicateScoreThreshold is < 0 or > 100)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:DuplicateScoreThreshold' must be between 0 and 100; got {DuplicateScoreThreshold}.");
        }

        var weightSum = DuplicateWeights.NameSimilarity + DuplicateWeights.SameCustomer + DuplicateWeights.SharedResource;
        if (weightSum != 100)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:DuplicateWeights' (NameSimilarity + SameCustomer + SharedResource) must sum to 100; got {weightSum}.");
        }
    }
}

/// <summary>The per-signal weights the duplicate-identity score (L3 #4) combines. Must sum to 100.</summary>
public sealed class DuplicateWeightOptions
{
    public int NameSimilarity { get; set; } = 50;
    public int SameCustomer { get; set; } = 30;
    public int SharedResource { get; set; } = 20;
}
