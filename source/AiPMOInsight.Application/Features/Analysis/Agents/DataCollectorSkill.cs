using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Model;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>The upload bytes handed to the Data Collector (#1).</summary>
public sealed record UploadPayload(string FileName, byte[] Content);

/// <summary>
/// Agent #1 — Data Collector. Pure deterministic parsing (no LLM): delegates to the
/// <see cref="IUploadParser"/> port to turn the upload into typed records. Thin by design — the
/// format-specific parsing lives in Infrastructure so vendor libraries stay out of Application.
/// </summary>
public sealed class DataCollectorSkill(IUploadParser parser) : IAgentSkill<UploadPayload, CollectedData>
{
    public string Name => "DataCollector";

    public Task<CollectedData> ExecuteAsync(UploadPayload input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Task.FromResult(parser.Parse(input.FileName, input.Content));
    }
}
