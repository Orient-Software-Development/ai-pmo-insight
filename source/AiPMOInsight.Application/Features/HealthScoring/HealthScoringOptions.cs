using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.HealthScoring;

/// <summary>
/// The externalised, swappable scoring configuration bound from the <c>HealthScoring</c> section.
/// Every number here is the client's, not ours — the shipped default carries the PRD's <b>EXAMPLE</b>
/// placeholders (<see cref="IsPlaceholder"/> = <c>true</c>) until the PMO agrees real values at
/// kickoff. Dictionary keys are enum <i>names</i> (case-insensitive) so the JSON reads naturally
/// (<c>"Schedule": 20</c>); the typed accessors below convert. Call <see cref="Validate"/> at startup
/// to fail fast (weights must sum to <see cref="WeightTotal"/>, thresholds must be ordered) with a
/// message naming the offending key. "RAG" here is the Red/Amber/Green health colour.
///
/// <para><b>Config format:</b> plain appsettings JSON (no YAML dependency) — chosen over YAML to
/// avoid adding <c>YamlDotNet</c> and to match the existing <c>LlmOptions</c>/<c>JwtOptions</c>
/// binding pattern. A PMO edits the <c>HealthScoring</c> block in appsettings or an env override.</para>
/// </summary>
public sealed class HealthScoringOptions
{
    public const string SectionName = "HealthScoring";

    /// <summary>True while the values are the PRD EXAMPLE placeholders (drives the startup warning).</summary>
    public bool IsPlaceholder { get; set; } = true;

    /// <summary>Per-area weight, keyed by <see cref="HealthArea"/> name. Must sum to <see cref="WeightTotal"/>.</summary>
    public Dictionary<string, int> Weights { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The total the weights must sum to (usually 100).</summary>
    public int WeightTotal { get; set; } = 100;

    /// <summary>Severity → numeric area score, keyed by <see cref="Severity"/> name (e.g. Green=100).</summary>
    public Dictionary<string, int> SeverityScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Inclusive RAG lower-bound thresholds for the weighted score.</summary>
    public RagThresholds Thresholds { get; set; } = new();

    /// <summary>Confidence → numeric, keyed by <see cref="Confidence"/> name (e.g. High=100).</summary>
    public Dictionary<string, int> ConfidenceScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Aggregate confidence strictly below this floor → "Needs PM Review".</summary>
    public int ConfidenceFloor { get; set; }

    /// <summary>Override rules that set a worst-case floor on the bucket. Evaluated in list order.</summary>
    public List<OverrideRuleOptions> Overrides { get; set; } = new();

    /// <summary>Configured weight for an area (0 if the area has no weight — it then cannot contribute).</summary>
    public int WeightFor(HealthArea area) => Weights.TryGetValue(area.ToString(), out var w) ? w : 0;

    /// <summary>Numeric area score for a severity, from the configured mapping.</summary>
    public int ScoreFor(Severity severity) => SeverityScores[severity.ToString()];

    /// <summary>Numeric confidence score, from the configured mapping.</summary>
    public int ScoreFor(Confidence confidence) => ConfidenceScores[confidence.ToString()];

    /// <summary>
    /// Fails fast on invalid configuration (weights don't sum, thresholds out of order, missing
    /// severity/confidence mappings, unparseable keys), always naming the offending config key so ops
    /// can fix it. Never silently normalises. Called once at startup.
    /// </summary>
    public void Validate()
    {
        // Weight keys must be real areas, and the weights must sum to the configured total.
        foreach (var key in Weights.Keys)
        {
            if (!Enum.TryParse<HealthArea>(key, ignoreCase: true, out _))
            {
                throw new InvalidOperationException(
                    $"'{SectionName}:Weights' has key '{key}', which is not a known health area " +
                    $"({string.Join(", ", Enum.GetNames<HealthArea>())}).");
            }
        }

        var weightSum = Weights.Values.Sum();
        if (weightSum != WeightTotal)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:Weights' must sum to '{SectionName}:WeightTotal' ({WeightTotal}); got {weightSum}.");
        }

        // Thresholds must be ordered Green > Amber and bounded within 0..100.
        if (Thresholds.Amber < 0 || Thresholds.Green > 100 || Thresholds.Green <= Thresholds.Amber)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:Thresholds' must be ordered 0 <= Amber < Green <= 100; got " +
                $"Amber={Thresholds.Amber}, Green={Thresholds.Green}.");
        }

        // Every severity and confidence value must have a numeric mapping (the scorer indexes them).
        foreach (var severity in Enum.GetValues<Severity>())
        {
            if (!SeverityScores.ContainsKey(severity.ToString()))
            {
                throw new InvalidOperationException(
                    $"'{SectionName}:SeverityScores' is missing a value for '{severity}'.");
            }
        }

        foreach (var confidence in Enum.GetValues<Confidence>())
        {
            if (!ConfidenceScores.ContainsKey(confidence.ToString()))
            {
                throw new InvalidOperationException(
                    $"'{SectionName}:ConfidenceScores' is missing a value for '{confidence}'.");
            }
        }

        // Override rules must parse to real areas/severities so they can never fail mid-request.
        foreach (var rule in Overrides)
        {
            rule.Validate(SectionName);
        }
    }
}

/// <summary>Inclusive RAG lower-bound thresholds: score ≥ <see cref="Green"/> is Green, ≥ <see cref="Amber"/> is Amber, else Red.</summary>
public sealed class RagThresholds
{
    public int Green { get; set; }
    public int Amber { get; set; }
}

/// <summary>
/// A single override rule: when any scored finding in <see cref="Area"/> has a severity at least
/// <see cref="WhenSeverityAtLeast"/>, the project bucket is floored at <see cref="Floor"/>. Keys are
/// enum names for readable JSON; the <c>*Enum</c> accessors parse them (validated at startup).
/// </summary>
public sealed class OverrideRuleOptions
{
    public string Id { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string WhenSeverityAtLeast { get; set; } = string.Empty;
    public string Floor { get; set; } = string.Empty;
    public string? Description { get; set; }

    public HealthArea AreaEnum => Enum.Parse<HealthArea>(Area, ignoreCase: true);
    public Severity WhenSeverityAtLeastEnum => Enum.Parse<Severity>(WhenSeverityAtLeast, ignoreCase: true);
    public Severity FloorEnum => Enum.Parse<Severity>(Floor, ignoreCase: true);

    internal void Validate(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException($"'{sectionName}:Overrides' has a rule with no Id.");
        }

        if (!Enum.TryParse<HealthArea>(Area, ignoreCase: true, out _)
            || !Enum.TryParse<Severity>(WhenSeverityAtLeast, ignoreCase: true, out _)
            || !Enum.TryParse<Severity>(Floor, ignoreCase: true, out _))
        {
            throw new InvalidOperationException(
                $"'{sectionName}:Overrides' rule '{Id}' has an unparseable Area/WhenSeverityAtLeast/Floor " +
                $"(Area='{Area}', WhenSeverityAtLeast='{WhenSeverityAtLeast}', Floor='{Floor}').");
        }
    }
}
