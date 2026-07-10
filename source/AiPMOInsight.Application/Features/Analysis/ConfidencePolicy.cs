using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis;

/// <summary>
/// Data Quality's assessment of a single project's source data, consumed downstream to set finding
/// confidence. Produced by agent #2; the fields mirror the DQ checks (missing data, staleness,
/// cross-source consistency).
/// </summary>
public sealed record DataQualitySignal
{
    /// <summary>Number of expected fields found missing/blank across the project's records.</summary>
    public required int MissingFieldCount { get; init; }

    /// <summary>Age in days of the most recent update to the project's records (null if unknown).</summary>
    public required double? LastUpdateAgeDays { get; init; }

    /// <summary>Whether identifiers/values are consistent across the parsed sources (e.g. IDs match).</summary>
    public required bool SourceConsistent { get; init; }

    /// <summary>A DQ signal for data that raised no quality concerns (used when nothing is wrong).</summary>
    public static DataQualitySignal Clean(double? lastUpdateAgeDays = 0) => new()
    {
        MissingFieldCount = 0,
        LastUpdateAgeDays = lastUpdateAgeDays,
        SourceConsistent = true,
    };
}

/// <summary>
/// Shared, deterministic mapping from Data Quality signals to a <see cref="Confidence"/> level.
/// <para>
/// <b>POC default (gap §2.1):</b> confidence starts High and is knocked down by each quality
/// problem — missing fields, stale updates, and cross-source inconsistency each cost a level; the
/// result is the floor of those knock-downs. LLM agents may self-report a level but it is
/// <see cref="Cap"/>ped by the underlying data's DQ confidence, so the scale is comparable across
/// deterministic and LLM findings. The exact weights are a kick-off number and swappable here
/// without a schema change.
/// </para>
/// </summary>
public static class ConfidencePolicy
{
    // POC thresholds — documented, deliberately simple, and tuned at kick-off.
    private const double StaleDays = 30;
    private const double VeryStaleDays = 90;

    /// <summary>Derives confidence for a deterministic finding from the project's DQ signal.</summary>
    public static Confidence FromSignals(DataQualitySignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        var level = 2; // start High

        if (signal.MissingFieldCount > 2)
        {
            level -= 2;
        }
        else if (signal.MissingFieldCount > 0)
        {
            level -= 1;
        }

        if (signal.LastUpdateAgeDays is { } age)
        {
            if (age > VeryStaleDays)
            {
                level -= 2;
            }
            else if (age > StaleDays)
            {
                level -= 1;
            }
        }

        if (!signal.SourceConsistent)
        {
            level -= 1;
        }

        return ToConfidence(level);
    }

    /// <summary>
    /// Caps an LLM agent's self-reported confidence by the underlying data's DQ confidence: a
    /// finding can never be more trustworthy than the data it rests on.
    /// </summary>
    public static Confidence Cap(Confidence selfReported, Confidence dataQuality) =>
        (Confidence)Math.Min((int)selfReported, (int)dataQuality);

    private static Confidence ToConfidence(int level) =>
        level >= 2 ? Confidence.High
        : level == 1 ? Confidence.Medium
        : Confidence.Low;
}
