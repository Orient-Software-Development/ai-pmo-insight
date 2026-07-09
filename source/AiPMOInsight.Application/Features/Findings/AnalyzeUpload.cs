using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Messaging;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Findings;

/// <summary>
/// Vertical slice: analyze a stored upload into findings. This is a STUB — no LLM, no parsing.
/// It emits one hard-coded finding that CITES the analyzed upload, proving the upload → cited
/// finding wiring. Runs synchronously; kept a separate step from upload (its own endpoint) so the
/// asynchronous seam exists without queue infrastructure. Returns <c>null</c> when the upload is
/// unknown (endpoint maps that to 404).
/// </summary>
public static class AnalyzeUpload
{
    public sealed record Command(Guid UploadId) : IRequest<Result?>;

    public sealed record Result(IReadOnlyList<FindingView> Findings);

    public sealed record FindingView(Guid Id, string ProjectKey, string Summary, CitationView Citation, DateTimeOffset CreatedAt);

    public sealed record CitationView(Guid UploadId, string Locator);

    internal sealed class Handler(
        IUploadRepository uploads,
        IFindingRepository findings,
        TimeProvider timeProvider) : IRequestHandler<Command, Result?>
    {
        public async Task<Result?> Handle(Command request, CancellationToken cancellationToken)
        {
            var upload = await uploads.GetAsync(request.UploadId, cancellationToken);
            if (upload is null)
            {
                return null;
            }

            // STUB analysis: real parsing + the agent pipeline come in a later change. Emit a single
            // finding whose citation points back at the analyzed upload — the one part that is real.
            var citation = Citation.Create(upload.Id, $"{upload.FileName}#stub");
            var finding = Finding.Create(
                projectKey: "DUMMY-001",
                summary: "Stub finding: analysis pipeline not yet implemented (skeleton).",
                citation: citation,
                now: timeProvider.GetUtcNow());

            await findings.AddAsync(finding, cancellationToken);

            return new Result([ToView(finding)]);
        }

        private static FindingView ToView(Finding f) =>
            new(f.Id, f.ProjectKey, f.Summary, new CitationView(f.Citation.UploadId, f.Citation.Locator), f.CreatedAt);
    }
}
