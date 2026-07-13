namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Domain-appropriate failure raised when a vendor LLM adapter's call fails. Wraps the underlying
/// provider/SDK exception so callers never see a raw vendor type, and its message names the provider
/// and skill but <b>never</b> the configured API key (R3 secret-leak guard). The original SDK
/// exception is preserved as <see cref="Exception.InnerException"/> for diagnostics — vendor error
/// bodies carry a request id and server message, not the key (which travels only as a request header).
/// </summary>
public sealed class LlmProviderException : Exception
{
    public LlmProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
