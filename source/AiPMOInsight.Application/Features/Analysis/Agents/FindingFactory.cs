using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// Builds <see cref="Finding"/>s with run provenance and a citation threaded from a parsed record's
/// <see cref="SourceRef"/> — the single place the "no finding without a citation" invariant is
/// satisfied for the deterministic agents. Uses the run's start time so a run's findings share a
/// timestamp and need no ambient clock.
/// </summary>
internal static class FindingFactory
{
    /// <summary>An analytic (Kind = Analysis) finding citing the record it derives from.</summary>
    public static Finding Analysis(
        ProjectSlice slice,
        string producingAgent,
        string summary,
        SourceRef source,
        Confidence confidence) =>
        Finding.Create(
            projectKey: slice.ProjectKey,
            summary: summary,
            citation: source.ToCitation(slice.Run.UploadId),
            now: slice.Run.StartedAt,
            runId: slice.Run.RunId,
            producingAgent: producingAgent,
            kind: FindingKind.Analysis,
            confidence: confidence);
}
