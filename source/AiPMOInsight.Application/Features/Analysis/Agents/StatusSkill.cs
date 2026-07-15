using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// Agent #3 — Status. Pure deterministic math over milestone dates (no LLM): schedule variance
/// (completed late), delay severity (overdue), upcoming due dates, and dependency risk (a milestone
/// whose prerequisite is not yet done). Each finding cites its milestone; confidence comes from the
/// DQ signal via <see cref="ConfidencePolicy"/>.
/// </summary>
public sealed class StatusSkill : IAgentSkill<AnalysisInput, IReadOnlyList<Finding>>
{
    private const int UpcomingWindowDays = 14;

    public string Name => "Status";

    public Task<IReadOnlyList<Finding>> ExecuteAsync(AnalysisInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var slice = input.Slice;
        var confidence = ConfidencePolicy.FromSignals(input.Quality);
        var asOf = slice.Run.StartedAt;

        var milestones = slice.Data.Milestones.Where(m => m.ProjectKey == slice.ProjectKey).ToList();
        var findings = new List<Finding>();

        foreach (var milestone in milestones)
        {
            if (milestone.DueDate is not { } due)
            {
                continue; // Data Quality already flagged the missing due date.
            }

            if (milestone.CompletedDate is { } completed)
            {
                if (completed > due)
                {
                    var days = (int)(completed - due).TotalDays;
                    findings.Add(Finding(slice, confidence,
                        $"Milestone '{milestone.Name}' completed {days} days late ({Severity(days)} schedule variance).",
                        milestone.Source, Band(days)));
                }

                continue;
            }

            if (due < asOf)
            {
                var days = (int)(asOf - due).TotalDays;
                findings.Add(Finding(slice, confidence,
                    $"Milestone '{milestone.Name}' is overdue by {days} days ({Severity(days)} delay).",
                    milestone.Source, Band(days)));
            }
            else if (due <= asOf.AddDays(UpcomingWindowDays))
            {
                var days = (int)(due - asOf).TotalDays;
                findings.Add(Finding(slice, confidence,
                    $"Milestone '{milestone.Name}' is due soon (in {days} days).",
                    milestone.Source, Domain.Findings.Severity.Green)); // informational heads-up, not yet a variance
            }

            if (milestone.DependsOn is { } dependsOn && IsAtRisk(dependsOn, milestones))
            {
                findings.Add(Finding(slice, confidence,
                    $"Milestone '{milestone.Name}' is at dependency risk: it depends on '{dependsOn}', which is not yet complete.",
                    milestone.Source, Domain.Findings.Severity.Amber));
            }
        }

        return Task.FromResult<IReadOnlyList<Finding>>(findings);
    }

    private static bool IsAtRisk(string name, IEnumerable<MilestoneRecord> milestones)
    {
        var target = milestones.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return true; // depends on something not in the plan
        }

        // At risk if the prerequisite is incomplete, or was completed after its due date.
        return target.CompletedDate is null
            || (target.DueDate is { } d && target.CompletedDate > d);
    }

    private static string Severity(int days) => days switch
    {
        >= 30 => "major",
        >= 7 => "moderate",
        _ => "minor",
    };

    /// <summary>
    /// The RAG severity for a days-late/overdue band, reusing the same thresholds as the free-text
    /// <see cref="Severity(int)"/> word (major → Red, moderate → Amber, minor → Green). This is the
    /// signal the health scorer reads for the Schedule area.
    /// </summary>
    private static Severity Band(int days) => days switch
    {
        >= 30 => Domain.Findings.Severity.Red,
        >= 7 => Domain.Findings.Severity.Amber,
        _ => Domain.Findings.Severity.Green,
    };

    private static Finding Finding(
        ProjectSlice slice, Confidence confidence, string summary, SourceRef source, Severity severity) =>
        FindingFactory.Analysis(slice, "Status", summary, source, confidence, HealthArea.Schedule, severity);
}
