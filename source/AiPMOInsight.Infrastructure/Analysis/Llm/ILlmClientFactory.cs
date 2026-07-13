using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Maps a resolved <see cref="LlmProviderOptions"/> to a concrete <see cref="ILlmClient"/> — the
/// single-file switch that decides which vendor adapter serves a given agent. Called once per agent
/// at DI-registration time; the resulting clients are cached inside <see cref="RoutingLlmClient"/>
/// and never rebuilt on the hot path.
/// <para>
/// <paramref name="skillNameForDiagnostics"/> is threaded purely so error messages can name the
/// agent whose config produced the failure — adapters MUST NOT vary behaviour by skill.
/// </para>
/// </summary>
public interface ILlmClientFactory
{
    /// <summary>
    /// Returns the concrete <see cref="ILlmClient"/> for <paramref name="options"/>. Throws
    /// <see cref="InvalidOperationException"/> at call time for an unknown or empty
    /// <see cref="LlmProviderOptions.Provider"/> — startup MUST fail loudly, never at request time.
    /// </summary>
    ILlmClient Create(string skillNameForDiagnostics, LlmProviderOptions options);
}
