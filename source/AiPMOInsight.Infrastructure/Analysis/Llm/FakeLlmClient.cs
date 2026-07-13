using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// The only <see cref="ILlmClient"/> implementation this slice registers: returns canned, typed
/// fixture responses instead of calling a real model, so the orchestrator demos end-to-end with no
/// API key. Fixtures are keyed by the requested output type; the request (incl. its
/// <see cref="LlmRequest.SkillName"/>) is passed to the factory so a fixture can vary by caller.
/// The real vendor adapter is a one-file swap in a later change (selected via <c>LlmOptions.Provider</c>).
/// </summary>
public sealed class FakeLlmClient(IReadOnlyDictionary<Type, Func<LlmRequest, object>> fixtures) : ILlmClient
{
    public Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
        where TOutput : notnull
    {
        ArgumentNullException.ThrowIfNull(request);

        if (fixtures.TryGetValue(typeof(TOutput), out var factory))
        {
            return Task.FromResult((TOutput)factory(request));
        }

        throw new NotSupportedException(
            $"FakeLlmClient has no fixture for '{typeof(TOutput).Name}' (skill '{request.SkillName}').");
    }
}
