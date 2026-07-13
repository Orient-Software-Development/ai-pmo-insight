namespace AiPMOInsight.Domain.Findings;

/// <summary>
/// The durable domain aggregate for the AI PMO POC: a single analytic finding about a project,
/// always tied to the source it came from via <see cref="Citation"/>. Findings are grouped by an
/// opaque <see cref="ProjectKey"/> string (no Project entity yet) — when real Orbit data is fed
/// later that key becomes the Orbit project id, a value change rather than a structural one.
/// <para>
/// Provenance (added in the analysis-pipeline change): every finding records which agent produced
/// it (<see cref="ProducingAgent"/>), how much to trust it (<see cref="Confidence"/>), what role it
/// plays (<see cref="Kind"/>), which analysis run emitted it (<see cref="RunId"/>), and — for
/// LLM-produced findings — the content hash of the prompt used (<see cref="PromptVersion"/>). The
/// narrative, challenge, and review outputs are modelled as findings of the matching
/// <see cref="FindingKind"/> so the whole run reads back from one aggregate.
/// </para>
/// </summary>
public sealed class Finding
{
    public required Guid Id { get; init; }

    /// <summary>Opaque grouping key (later = Orbit project id).</summary>
    public required string ProjectKey { get; init; }

    public required string Summary { get; init; }

    /// <summary>Mandatory provenance back to the source. A finding cannot exist without one.</summary>
    public required Citation Citation { get; init; }

    /// <summary>What role this finding plays in the pipeline output.</summary>
    public required FindingKind Kind { get; init; }

    /// <summary>How much trust to place in the finding.</summary>
    public required Confidence Confidence { get; init; }

    /// <summary>The agent that produced the finding (e.g. <c>Financial</c>, <c>Narrative</c>).</summary>
    public required string ProducingAgent { get; init; }

    /// <summary>Identity of the analysis run that emitted this finding.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Content hash of the prompt used, for LLM-produced findings; null for deterministic agents.</summary>
    public string? PromptVersion { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public static Finding Create(
        string projectKey,
        string summary,
        Citation citation,
        DateTimeOffset now,
        Guid runId,
        string producingAgent,
        FindingKind kind,
        Confidence confidence,
        string? promptVersion = null)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            throw new ArgumentException("ProjectKey is required.", nameof(projectKey));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Summary is required.", nameof(summary));
        }

        if (string.IsNullOrWhiteSpace(producingAgent))
        {
            throw new ArgumentException("ProducingAgent is required.", nameof(producingAgent));
        }

        if (runId == Guid.Empty)
        {
            throw new ArgumentException("RunId is required.", nameof(runId));
        }

        // The invariant that makes findings trustworthy: no citation, no finding.
        ArgumentNullException.ThrowIfNull(citation);

        return new Finding
        {
            Id = Guid.NewGuid(),
            ProjectKey = projectKey,
            Summary = summary,
            Citation = citation,
            Kind = kind,
            Confidence = confidence,
            ProducingAgent = producingAgent,
            RunId = runId,
            PromptVersion = promptVersion,
            CreatedAt = now,
        };
    }
}
