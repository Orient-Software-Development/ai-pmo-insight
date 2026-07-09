namespace AiPMOInsight.Domain.Findings;

/// <summary>
/// The durable domain aggregate for the AI PMO POC: a single analytic finding about a project,
/// always tied to the source it came from via <see cref="Citation"/>. Findings are grouped by an
/// opaque <see cref="ProjectKey"/> string (no Project entity yet) — when real Orbit data is fed
/// later that key becomes the Orbit project id, a value change rather than a structural one.
/// </summary>
public sealed class Finding
{
    public required Guid Id { get; init; }

    /// <summary>Opaque grouping key (later = Orbit project id).</summary>
    public required string ProjectKey { get; init; }

    public required string Summary { get; init; }

    /// <summary>Mandatory provenance back to the source. A finding cannot exist without one.</summary>
    public required Citation Citation { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public static Finding Create(string projectKey, string summary, Citation citation, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            throw new ArgumentException("ProjectKey is required.", nameof(projectKey));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Summary is required.", nameof(summary));
        }

        // The invariant that makes findings trustworthy: no citation, no finding.
        ArgumentNullException.ThrowIfNull(citation);

        return new Finding
        {
            Id = Guid.NewGuid(),
            ProjectKey = projectKey,
            Summary = summary,
            Citation = citation,
            CreatedAt = now,
        };
    }
}
