using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// Scope agent — pure deterministic checks (no LLM) over a project's scope-change records, applying the
/// <b>POC "unapproved creep" RAG rule</b> (a real, client-agreed rule is a kickoff follow-on): an
/// <i>unapproved scope increase</i> is Red creep; an <i>approved</i> change is Amber (scope moved but
/// controlled); an unapproved non-increase (e.g. a removal) is Amber (an open change, not creep);
/// <i>rejected</i> changes and no changes produce nothing (Green by absence). Each finding cites its
/// scope row and carries structured detail for the L2 key-deviations panel. <b>Display-only</b> — the
/// health scorer excludes the Scope area, so these findings never move the RAG score. Mirrors the
/// deterministic Decision → RAID patterns.
/// </summary>
public sealed class ScopeSkill : IAgentSkill<AnalysisInput, IReadOnlyList<Finding>>
{
    public string Name => "Scope";

    public Task<IReadOnlyList<Finding>> ExecuteAsync(AnalysisInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var slice = input.Slice;
        var confidence = ConfidencePolicy.FromSignals(input.Quality);
        var findings = new List<Finding>();

        foreach (var change in slice.Data.ScopeChanges.Where(c => c.ProjectKey == slice.ProjectKey))
        {
            if (IsRejected(change.Status))
            {
                continue; // scope did not move — nothing to flag.
            }

            var detail = Detail(change);

            if (IsApproved(change.Status))
            {
                findings.Add(Finding(slice, confidence,
                    $"Approved scope change '{change.Title}' ({Impact(change)}) — scope moved, controlled.",
                    change.Source, Severity.Amber, detail));
            }
            else if (IsIncrease(change))
            {
                findings.Add(Finding(slice, confidence,
                    $"Unapproved scope increase '{change.Title}' ({Impact(change)}) — scope creep.",
                    change.Source, Severity.Red, detail));
            }
            else
            {
                findings.Add(Finding(slice, confidence,
                    $"Open scope change '{change.Title}' ({Impact(change)}) awaiting a decision.",
                    change.Source, Severity.Amber, detail));
            }
        }

        return Task.FromResult<IReadOnlyList<Finding>>(findings);
    }

    private static bool IsApproved(string? status) =>
        string.Equals(status?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase);

    private static bool IsRejected(string? status) =>
        string.Equals(status?.Trim(), "Rejected", StringComparison.OrdinalIgnoreCase);

    private static bool IsIncrease(ScopeChangeRecord change) =>
        string.Equals(change.Type?.Trim(), "Add", StringComparison.OrdinalIgnoreCase)
        || (change.EffortImpactPct ?? 0) > 0;

    private static string Impact(ScopeChangeRecord change) =>
        change.EffortImpactPct is { } pct ? $"{pct:+0.#;-0.#;0}% effort" : (change.Type ?? "change");

    private static IReadOnlyDictionary<string, string> Detail(ScopeChangeRecord change) =>
        new Dictionary<string, string>
        {
            ["title"] = change.Title,
            ["type"] = change.Type ?? string.Empty,
            ["status"] = change.Status ?? string.Empty,
            ["impactPct"] = change.EffortImpactPct?.ToString() ?? string.Empty,
        };

    private static Finding Finding(
        ProjectSlice slice, Confidence confidence, string summary, SourceRef source, Severity severity,
        IReadOnlyDictionary<string, string> detail) =>
        FindingFactory.Analysis(
            slice, "Scope", summary, source, confidence, HealthArea.Scope, severity, metricDetail: detail);
}
