namespace AiPMOInsight.Domain.Findings;

/// <summary>
/// How much trust to place in a finding. Derived deterministically from Data Quality signals for
/// the pure-code agents (see <c>ConfidencePolicy</c> in Application); LLM agents may self-report but
/// are capped by the underlying data's confidence, so the scale is comparable across agents.
/// </summary>
public enum Confidence
{
    Low = 0,
    Medium = 1,
    High = 2,
}
