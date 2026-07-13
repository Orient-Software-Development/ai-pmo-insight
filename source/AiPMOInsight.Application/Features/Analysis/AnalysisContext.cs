using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis;

/// <summary>
/// One project's data plus the run identity, the shared input to the analysis agents. Agents filter
/// <see cref="Data"/> to <see cref="ProjectKey"/>; the run supplies the upload id (for citations)
/// and the run id (stamped on every finding).
/// </summary>
public sealed record ProjectSlice
{
    public required AnalysisRun Run { get; init; }
    public required string ProjectKey { get; init; }
    public required CollectedData Data { get; init; }
}

/// <summary>
/// Input to the analysis agents that run after Data Quality (#3 Status, #5 Financial, #6 Resource,
/// and #4): the project slice plus the DQ confidence signal used to set finding confidence.
/// </summary>
public sealed record AnalysisInput(ProjectSlice Slice, DataQualitySignal Quality);

/// <summary>Data Quality's output: its own findings plus the confidence signal for downstream agents.</summary>
public sealed record DataQualityResult(IReadOnlyList<Finding> Findings, DataQualitySignal Signal);
