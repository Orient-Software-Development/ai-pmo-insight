using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// The single <see cref="ILlmClient"/> registered in the DI container — an adapter that dispatches
/// each call to the per-agent inner client selected by <see cref="LlmRequest.SkillName"/>, or to
/// the <c>default</c> inner client when no override is registered. Skill lookup is
/// case-insensitive. Inner clients are constructed once at DI-registration time (by
/// <see cref="ILlmClientFactory"/>) and reused for every call — routing itself is a single
/// dictionary lookup, deliberately kept off the hot path of a request.
/// <para>
/// Agent code depends on <see cref="ILlmClient"/> only and has no awareness of routing or
/// per-agent config — this keeps the Application layer provider-agnostic (design decision §1).
/// </para>
/// </summary>
public sealed class RoutingLlmClient : ILlmClient
{
    private readonly ILlmClient _default;
    private readonly IReadOnlyDictionary<string, ILlmClient> _perSkill;

    public RoutingLlmClient(ILlmClient @default, IReadOnlyDictionary<string, ILlmClient> perSkill)
    {
        ArgumentNullException.ThrowIfNull(@default);
        ArgumentNullException.ThrowIfNull(perSkill);

        _default = @default;
        _perSkill = perSkill;
    }

    public Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
        where TOutput : notnull
    {
        ArgumentNullException.ThrowIfNull(request);

        var inner = _perSkill.TryGetValue(request.SkillName, out var picked) ? picked : _default;
        return inner.CompleteAsync<TOutput>(request, cancellationToken);
    }
}
