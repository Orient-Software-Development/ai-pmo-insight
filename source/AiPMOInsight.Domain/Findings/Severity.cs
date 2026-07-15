namespace AiPMOInsight.Domain.Findings;

/// <summary>
/// The Red/Amber/Green (RAG) health severity a deterministic analysis agent already computes and now
/// stamps onto the finding, instead of melting it into the free-text summary. Distinct from
/// <see cref="Confidence"/> — severity is <i>how bad</i> the signal is, confidence is <i>how much to
/// trust it</i>. Ordinals ascend with badness (<see cref="Green"/> best, <see cref="Red"/> worst) so
/// the scorer can take the worst severity per area with a simple max. Persisted as a string so the
/// DB stays readable and stable if ordinals shift. "RAG" here always means the health colour, never
/// retrieval-augmented generation.
/// </summary>
public enum Severity
{
    /// <summary>Healthy — no material concern.</summary>
    Green = 0,

    /// <summary>Caution — a concern worth watching.</summary>
    Amber = 1,

    /// <summary>Critical — a material problem.</summary>
    Red = 2,
}
