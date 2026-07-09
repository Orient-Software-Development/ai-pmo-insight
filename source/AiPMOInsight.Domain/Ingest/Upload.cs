namespace AiPMOInsight.Domain.Ingest;

/// <summary>
/// A raw uploaded file in the ingest landing area. In this slice the content is opaque bytes —
/// nothing parses it. It exists so a <see cref="AiPMOInsight.Domain.Findings.Finding"/> can cite a
/// real source. For the POC raw uploads and findings may share a store; the split is deferred.
/// </summary>
public sealed class Upload
{
    public required Guid Id { get; init; }

    public required string FileName { get; init; }

    public required byte[] Content { get; init; }

    public required DateTimeOffset UploadedAt { get; init; }

    public static Upload Create(string fileName, byte[] content, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("FileName is required.", nameof(fileName));
        }

        if (content is null || content.Length == 0)
        {
            throw new ArgumentException("Content is required.", nameof(content));
        }

        return new Upload
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            Content = content,
            UploadedAt = now,
        };
    }
}
