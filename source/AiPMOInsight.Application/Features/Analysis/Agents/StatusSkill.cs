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
    // The doc's "Upcoming milestones — next 2–4 weeks": look 4 weeks ahead for due-soon heads-ups.
    private const int UpcomingWindowDays = 28;

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

            // A recorded adverse status (e.g. "Missed", "At Risk") is authoritative regardless of the date
            // window — a missed milestone that has been re-baselined to a future date must not be rendered as
            // a benign green "due soon". Missed → Red, At Risk → Amber; it floors any date-derived severity.
            var statusSeverity = AdverseStatusSeverity(milestone.Status);

            if (milestone.CompletedDate is { } completed)
            {
                if (completed > due)
                {
                    var days = (int)(completed - due).TotalDays;
                    findings.Add(Finding(slice, confidence,
                        $"Milestone '{milestone.Name}' completed {days} days late ({Severity(days)} schedule variance).",
                        milestone.Source, Worst(Band(days), statusSeverity), Detail(milestone, "late")));
                }

                continue;
            }

            if (due < asOf)
            {
                var days = (int)(asOf - due).TotalDays;
                findings.Add(Finding(slice, confidence,
                    $"Milestone '{milestone.Name}' is overdue by {days} days ({Severity(days)} delay).",
                    milestone.Source, Worst(Band(days), statusSeverity), Detail(milestone, "overdue")));
            }
            else if (statusSeverity is { } adverse)
            {
                // Not yet due, but recorded as missed/at-risk — an upcoming milestone flagged as a risk (so it
                // still belongs in the Upcoming-milestones view, coloured by its status rather than green).
                findings.Add(Finding(slice, confidence,
                    $"Milestone '{milestone.Name}' is marked '{milestone.Status}' (schedule risk).",
                    milestone.Source, adverse, Detail(milestone, "upcoming")));
            }
            else if (due <= asOf.AddDays(UpcomingWindowDays))
            {
                var days = (int)(due - asOf).TotalDays;
                findings.Add(Finding(slice, confidence,
                    $"Milestone '{milestone.Name}' is due soon (in {days} days).",
                    milestone.Source, Domain.Findings.Severity.Green, Detail(milestone, "upcoming"))); // heads-up, not yet a variance
            }

            if (milestone.DependsOn is { } dependsOn && IsAtRisk(dependsOn, milestones))
            {
                findings.Add(Finding(slice, confidence,
                    $"Milestone '{milestone.Name}' is at dependency risk: it depends on '{dependsOn}', which is not yet complete.",
                    milestone.Source, Domain.Findings.Severity.Amber, Detail(milestone, "dependency")));
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

    /// <summary>
    /// The RAG severity implied by a milestone's recorded status word, or null when the status is
    /// absent/benign. "Missed" is a Red schedule signal; "At Risk" an Amber one. Case-insensitive.
    /// </summary>
    private static Severity? AdverseStatusSeverity(string? status)
    {
        var text = status?.Trim().ToLowerInvariant();
        return text switch
        {
            "missed" => Domain.Findings.Severity.Red,
            "at risk" or "atrisk" or "at-risk" => Domain.Findings.Severity.Amber,
            _ => null,
        };
    }

    /// <summary>The worse (higher-ordinal) of a date-derived severity and an optional status severity.</summary>
    private static Severity Worst(Severity band, Severity? status) =>
        status is { } s && (int)s > (int)band ? s : band;

    /// <summary>
    /// Structured detail for the L2 milestone views: the milestone name, its (adjusted) due date, and a
    /// <paramref name="kind"/> tag — "upcoming" for forward-looking heads-ups (the Upcoming-milestones
    /// panel) vs "overdue"/"late"/"dependency" deviations (Key deviations &gt; Time). Columns, not prose.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Detail(MilestoneRecord milestone, string kind) =>
        new Dictionary<string, string>
        {
            ["milestone"] = milestone.Name,
            ["dueDate"] = milestone.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ["kind"] = kind,
        };

    private static Finding Finding(
        ProjectSlice slice, Confidence confidence, string summary, SourceRef source, Severity severity,
        IReadOnlyDictionary<string, string>? metricDetail = null) =>
        FindingFactory.Analysis(
            slice, "Status", summary, source, confidence, HealthArea.Schedule, severity, metricDetail: metricDetail);
}
