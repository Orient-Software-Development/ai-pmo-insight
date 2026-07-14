namespace AiPMOInsight.Application.Abstractions;

/// <summary>
/// A single request to the LLM. Carries the fully rendered <see cref="Prompt"/>, the
/// <see cref="PromptVersion"/> (prompt content hash, stamped onto any findings the response
/// produces), and the <see cref="SkillName"/> of the calling agent (used for routing / fixtures).
/// The <b>output contract is the generic type parameter</b> of <see cref="ILlmClient.CompleteAsync"/>
/// — callers ask for a typed result, never free text.
/// </summary>
public sealed record LlmRequest
{
    /// <summary>The calling agent's stable name (e.g. <c>Narrative</c>), for routing / fixtures.</summary>
    public required string SkillName { get; init; }

    /// <summary>The fully rendered prompt text — the volatile, per-call portion of the input.</summary>
    public required string Prompt { get; init; }

    /// <summary>Content hash of the prompt; stamped onto findings produced from the response.</summary>
    public required string PromptVersion { get; init; }

    /// <summary>
    /// Optional stable prefix (e.g. the instructional prompt from <c>PromptRegistry</c>) sent as a
    /// system prompt. Splitting it from <see cref="Prompt"/> lets the Anthropic adapter mark it with
    /// <c>cache_control</c> so repeat calls within an upload reuse the cached prefix at ~90% input
    /// discount; OpenAI's automatic prefix caching benefits from the same split. Leave <c>null</c>
    /// to send everything as one user message (legacy behaviour).
    /// </summary>
    public string? SystemPrompt { get; init; }
}

/// <summary>
/// Port abstracting the LLM runtime (dependency rule: Application defines the interface, the
/// concrete vendor adapter lives at the Infrastructure boundary and never leaks upward). Every call
/// requests <b>structured JSON output</b> conforming to <typeparamref name="TOutput"/> — the system
/// declares the contract and deserialises into it, and never parses free text. In this slice the
/// only registered implementation is a fake returning fixture responses; the real vendor adapter is
/// a later change and requires no change here.
/// </summary>
public interface ILlmClient
{
    /// <summary>Completes <paramref name="request"/> into a value shaped to the declared contract.</summary>
    Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
        where TOutput : notnull;
}
