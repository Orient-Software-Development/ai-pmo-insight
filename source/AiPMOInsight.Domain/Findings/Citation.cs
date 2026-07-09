namespace AiPMOInsight.Domain.Findings;

/// <summary>
/// Provenance for a <see cref="Finding"/>: the source it was derived from. Every finding MUST
/// carry a citation — it is the POC's trust story and the one thing the skeleton never stubs.
/// Value object (record) so two citations with the same upload + locator are equal.
/// </summary>
public sealed record Citation
{
    public required Guid UploadId { get; init; }

    /// <summary>Where inside the source the finding came from (e.g. a sheet/row or meeting date).</summary>
    public required string Locator { get; init; }

    public static Citation Create(Guid uploadId, string locator)
    {
        if (uploadId == Guid.Empty)
        {
            throw new ArgumentException("UploadId is required.", nameof(uploadId));
        }

        if (string.IsNullOrWhiteSpace(locator))
        {
            throw new ArgumentException("Locator is required.", nameof(locator));
        }

        return new Citation { UploadId = uploadId, Locator = locator };
    }
}
