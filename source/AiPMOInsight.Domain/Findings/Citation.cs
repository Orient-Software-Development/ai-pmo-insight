namespace AiPMOInsight.Domain.Findings;

/// <summary>
/// Provenance for a <see cref="Finding"/>: the source it was derived from. Every finding MUST
/// carry a citation — it is the POC's trust story and the one thing the skeleton never stubs.
/// Value object (record) so two citations with the same fields are equal.
/// <para>
/// <see cref="UploadId"/> and <see cref="Locator"/> are mandatory. The optional
/// <see cref="StructuredExcerpt"/> (e.g. <c>Budget!B4</c> or a field path) and
/// <see cref="TextSnippet"/> (a passage of the source text) let a reader see the exact evidence a
/// finding rests on — both nullable so a coarse citation (upload + locator) stays valid.
/// </para>
/// </summary>
public sealed record Citation
{
    public required Guid UploadId { get; init; }

    /// <summary>Where inside the source the finding came from (e.g. a sheet/row or meeting date).</summary>
    public required string Locator { get; init; }

    /// <summary>Optional structured pointer at the exact evidence (sheet/row/column or field path).</summary>
    public string? StructuredExcerpt { get; init; }

    /// <summary>Optional verbatim snippet of the source text the finding rests on.</summary>
    public string? TextSnippet { get; init; }

    public static Citation Create(
        Guid uploadId,
        string locator,
        string? structuredExcerpt = null,
        string? textSnippet = null)
    {
        if (uploadId == Guid.Empty)
        {
            throw new ArgumentException("UploadId is required.", nameof(uploadId));
        }

        if (string.IsNullOrWhiteSpace(locator))
        {
            throw new ArgumentException("Locator is required.", nameof(locator));
        }

        return new Citation
        {
            UploadId = uploadId,
            Locator = locator,
            // Normalise blank optionals to null so equality and persistence stay clean.
            StructuredExcerpt = string.IsNullOrWhiteSpace(structuredExcerpt) ? null : structuredExcerpt,
            TextSnippet = string.IsNullOrWhiteSpace(textSnippet) ? null : textSnippet,
        };
    }
}
