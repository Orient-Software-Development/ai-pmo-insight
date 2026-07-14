using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Messaging;

namespace AiPMOInsight.Application.Features.History;

/// <summary>
/// Vertical slice: the upload-history list. Returns every upload most-recently-uploaded first, as
/// lightweight list items (id, file name, upload time) — never the raw file bytes. Shared-workspace
/// visibility: any authenticated caller sees all uploads.
/// </summary>
public static class GetUploads
{
    public sealed record Query : IRequest<IReadOnlyList<UploadListItem>>;

    public sealed record UploadListItem(Guid Id, string FileName, DateTimeOffset UploadedAt);

    internal sealed class Handler(IUploadRepository uploads) : IRequestHandler<Query, IReadOnlyList<UploadListItem>>
    {
        public async Task<IReadOnlyList<UploadListItem>> Handle(Query request, CancellationToken cancellationToken)
        {
            var all = await uploads.ListAsync(cancellationToken);
            return all.Select(u => new UploadListItem(u.Id, u.FileName, u.UploadedAt)).ToList();
        }
    }
}
