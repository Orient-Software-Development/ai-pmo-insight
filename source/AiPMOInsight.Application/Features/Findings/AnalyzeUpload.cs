using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Messaging;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Findings;

/// <summary>
/// Vertical slice: analyze a stored upload into findings by driving the <see cref="AnalysisOrchestrator"/>
/// (the 9-agent pipeline). No longer a stub. Runs synchronously and stays a separate step from
/// upload (its own endpoint) so the asynchronous seam exists without queue infrastructure. Returns
/// <c>null</c> when the upload is unknown (endpoint maps that to 404).
/// </summary>
public static class AnalyzeUpload
{
    public sealed record Command(Guid UploadId) : IRequest<Result?>;

    public sealed record Result(Guid RunId, IReadOnlyList<FindingView> Findings);

    public sealed record FindingView(
        Guid Id,
        string ProjectKey,
        string Summary,
        string Kind,
        string Confidence,
        string ProducingAgent,
        string? PromptVersion,
        CitationView Citation,
        DateTimeOffset CreatedAt);

    public sealed record CitationView(Guid UploadId, string Locator, string? StructuredExcerpt, string? TextSnippet);

    internal sealed class Handler(IUploadRepository uploads, AnalysisOrchestrator orchestrator)
        : IRequestHandler<Command, Result?>
    {
        public async Task<Result?> Handle(Command request, CancellationToken cancellationToken)
        {
            var upload = await uploads.GetAsync(request.UploadId, cancellationToken);
            if (upload is null)
            {
                return null;
            }

            var result = await orchestrator.RunAsync(upload, cancellationToken);
            return new Result(result.RunId, result.Findings.Select(ToView).ToList());
        }

        private static FindingView ToView(Finding f) =>
            new(f.Id,
                f.ProjectKey,
                f.Summary,
                f.Kind.ToString(),
                f.Confidence.ToString(),
                f.ProducingAgent,
                f.PromptVersion,
                new CitationView(f.Citation.UploadId, f.Citation.Locator, f.Citation.StructuredExcerpt, f.Citation.TextSnippet),
                f.CreatedAt);
    }
}
