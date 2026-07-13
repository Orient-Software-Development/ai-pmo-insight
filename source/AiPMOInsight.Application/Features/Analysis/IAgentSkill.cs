namespace AiPMOInsight.Application.Features.Analysis;

/// <summary>
/// A single pipeline agent, expressed as a skill with a typed input and output contract. All nine
/// agents are skills driven by one <c>AnalysisOrchestrator</c> — not independent services (PRD
/// decision) — so the orchestrator can sequence and fan them out uniformly. Deterministic agents
/// implement this directly; LLM-backed agents implement it too and reach the model via
/// <see cref="AiPMOInsight.Application.Abstractions.ILlmClient"/> inside <see cref="ExecuteAsync"/>.
/// </summary>
/// <typeparam name="TInput">What the agent needs to run (typed records, prior findings, etc.).</typeparam>
/// <typeparam name="TOutput">What the agent produces (typically findings or a synthesised output).</typeparam>
public interface IAgentSkill<in TInput, TOutput>
{
    /// <summary>Stable agent name, stamped onto findings as their producing agent.</summary>
    string Name { get; }

    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken);
}
