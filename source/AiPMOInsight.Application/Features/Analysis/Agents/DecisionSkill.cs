using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// Decision agent — pure deterministic checks (no LLM) over the project's decision records: a decision
/// past its needed-by date and not yet approved is <b>overdue</b> (Red); one whose needed-by falls within
/// the near window and is not approved is <b>due soon</b> (Amber). Approved decisions, and decisions with
/// no needed-by date, produce nothing. Each finding cites its decision row; confidence comes from the DQ
/// signal via <see cref="ConfidencePolicy"/>. Mirrors the deterministic RAID → Risk pattern.
/// </summary>
public sealed class DecisionSkill : IAgentSkill<AnalysisInput, IReadOnlyList<Finding>>
{
    private const int DueSoonWindowDays = 14;

    public string Name => "Decision";

    public Task<IReadOnlyList<Finding>> ExecuteAsync(AnalysisInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var slice = input.Slice;
        var confidence = ConfidencePolicy.FromSignals(input.Quality);
        var asOf = slice.Run.StartedAt;
        var findings = new List<Finding>();

        foreach (var decision in slice.Data.Decisions.Where(d => d.ProjectKey == slice.ProjectKey))
        {
            if (decision.NeededBy is not { } neededBy || IsApproved(decision.Status))
            {
                continue; // can't judge timing, or already decided.
            }

            // Carry the decision's fields as structured data (not only in the prose) so the L2
            // "Decisions needed" panel renders columns — title/owner/deadline/consequence — instead of
            // parsing the summary string. Keys are a stable contract shared with the read API.
            var detail = Detail(decision, neededBy);

            if (neededBy < asOf)
            {
                var days = (int)(asOf - neededBy).TotalDays;
                findings.Add(Finding(slice, confidence,
                    $"Decision '{decision.Title}' is overdue by {days} days (needed by {neededBy:yyyy-MM-dd}, owner {decision.Owner ?? "unassigned"}).",
                    decision.Source, Severity.Red, detail));
            }
            else if (neededBy <= asOf.AddDays(DueSoonWindowDays))
            {
                var days = (int)(neededBy - asOf).TotalDays;
                findings.Add(Finding(slice, confidence,
                    $"Decision '{decision.Title}' is due soon (in {days} days, owner {decision.Owner ?? "unassigned"}).",
                    decision.Source, Severity.Amber, detail));
            }
        }

        return Task.FromResult<IReadOnlyList<Finding>>(findings);
    }

    private static bool IsApproved(string? status) =>
        string.Equals(status?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> Detail(DecisionRecord decision, DateTimeOffset neededBy) =>
        new Dictionary<string, string>
        {
            ["title"] = decision.Title,
            ["owner"] = decision.Owner ?? "unassigned",
            ["deadline"] = neededBy.ToString("yyyy-MM-dd"),
            ["consequence"] = decision.Consequence ?? string.Empty,
        };

    private static Finding Finding(
        ProjectSlice slice, Confidence confidence, string summary, SourceRef source, Severity severity,
        IReadOnlyDictionary<string, string> detail) =>
        FindingFactory.Analysis(
            slice, "Decision", summary, source, confidence, HealthArea.Decision, severity, metricDetail: detail);
}
