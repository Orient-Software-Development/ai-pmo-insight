namespace AiPMOInsight.Domain.Findings;

/// <summary>
/// Identity for one execution of the analysis pipeline over an upload. Every finding a run produces
/// is stamped with the run's <see cref="RunId"/>. Re-analysing the same upload starts a NEW run
/// (new <see cref="RunId"/>) whose findings are <b>appended</b> — prior runs' findings are retained,
/// never silently overwritten (gap §2.3).
/// <para>
/// A run is a transient orchestration concept: it is not its own table. Its identity lives on the
/// findings it produced, so "the latest run" is derivable and history is preserved for free.
/// </para>
/// </summary>
public sealed record AnalysisRun
{
    public required Guid RunId { get; init; }

    public required Guid UploadId { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Begins a new run with a fresh <see cref="RunId"/>.</summary>
    public static AnalysisRun Start(Guid uploadId, DateTimeOffset now)
    {
        if (uploadId == Guid.Empty)
        {
            throw new ArgumentException("UploadId is required.", nameof(uploadId));
        }

        return new AnalysisRun
        {
            RunId = Guid.NewGuid(),
            UploadId = uploadId,
            StartedAt = now,
        };
    }
}
