using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Messaging;
using AiPMOInsight.Domain.Ingest;

namespace AiPMOInsight.Application.Features.Ingest;

/// <summary>Vertical slice: store an uploaded (dummy Orbit-shaped) file. Command + handler colocated.</summary>
public static class UploadFixture
{
    public sealed record Command(string FileName, byte[] Content) : IRequest<Result>;

    public sealed record Result(Guid UploadId, string FileName);

    internal sealed class Handler(IUploadRepository repository, TimeProvider timeProvider)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Content is null || request.Content.Length == 0)
            {
                throw new ArgumentException("File content is required.", nameof(request));
            }

            var upload = Upload.Create(request.FileName, request.Content, timeProvider.GetUtcNow());
            await repository.AddAsync(upload, cancellationToken);

            return new Result(upload.Id, upload.FileName);
        }
    }
}
