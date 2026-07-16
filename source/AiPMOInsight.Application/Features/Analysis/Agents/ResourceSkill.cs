using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// Agent #6 — Resource. Pure deterministic math over assignments (no LLM): over-allocation beyond
/// capacity, per-person capacity pressure (total allocation across roles), a missing key role
/// (no Project Manager), and concentration × absence (a heavily allocated person on leave). Each
/// finding cites its assignment; confidence comes from the DQ signal via <see cref="ConfidencePolicy"/>.
/// </summary>
public sealed class ResourceSkill : IAgentSkill<AnalysisInput, IReadOnlyList<Finding>>
{
    private const double HeavyAllocation = 50;

    public string Name => "Resource";

    public Task<IReadOnlyList<Finding>> ExecuteAsync(AnalysisInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var slice = input.Slice;
        var confidence = ConfidencePolicy.FromSignals(input.Quality);
        var assignments = slice.Data.Assignments.Where(a => a.ProjectKey == slice.ProjectKey).ToList();
        var findings = new List<Finding>();

        foreach (var assignment in assignments)
        {
            if (assignment.AllocationPercent > assignment.CapacityPercent)
            {
                var overBy = assignment.AllocationPercent - assignment.CapacityPercent;
                findings.Add(Finding(slice, confidence,
                    $"{assignment.Person} is over-allocated at {assignment.AllocationPercent:F0}% against {assignment.CapacityPercent:F0}% capacity.",
                    assignment.Source, OverAllocationBand(overBy)));
            }

            if (assignment.OnLeave && assignment.AllocationPercent >= HeavyAllocation)
            {
                // A heavily-allocated person being absent is a concentration risk — critical.
                findings.Add(Finding(slice, confidence,
                    $"{assignment.Person} is on leave while carrying a heavy allocation ({assignment.AllocationPercent:F0}%) — concentration risk.",
                    assignment.Source, Severity.Red));
            }
        }

        // Per-person capacity pressure across multiple roles/assignments.
        foreach (var person in assignments.GroupBy(a => a.Person))
        {
            var total = person.Sum(a => a.AllocationPercent);
            var capacity = person.Max(a => a.CapacityPercent);
            if (person.Count() > 1 && total > capacity)
            {
                findings.Add(Finding(slice, confidence,
                    $"{person.Key} is spread across {person.Count()} assignments totalling {total:F0}% — capacity pressure.",
                    person.First().Source, Severity.Amber));
            }
        }

        // Missing key role: no Project Manager on the project.
        if (assignments.Count > 0 && !assignments.Any(a => IsProjectManagerRole(a.Role)))
        {
            findings.Add(Finding(slice, confidence,
                "No Project Manager is assigned to the project (missing key role).",
                assignments[0].Source, Severity.Amber));
        }

        // Cross-project key-person concentration (plan-doc line 148 / US-7, concentration half): count each
        // person's distinct projects across the WHOLE portfolio — slice.Data is the full CollectedData, not
        // just this project — and flag people on the current project who are spread thin. 5+ projects → Red,
        // 3–4 → Amber, <3 → not flagged. Emitted per project the person is on (so the L2 view keeps it, and
        // the L1 roll-up dedupes to distinct people). The × absence half of US-7 is out of scope (no parsed
        // absence signal); nothing is fabricated here.
        var allAssignments = slice.Data.Assignments;
        foreach (var person in assignments.Select(a => a.Person).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var projectCount = allAssignments
                .Where(a => string.Equals(a.Person, person, StringComparison.OrdinalIgnoreCase))
                .Select(a => a.ProjectKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (ConcentrationBand(projectCount) is { } severity)
            {
                var source = assignments.First(a => string.Equals(a.Person, person, StringComparison.OrdinalIgnoreCase)).Source;
                // Carry the person + project count as structured data so the L1 roll-up can dedupe to
                // distinct people without parsing the summary.
                findings.Add(FindingFactory.Analysis(slice, "Resource",
                    $"{person} is allocated across {projectCount} projects — key-person concentration risk.",
                    source, confidence, HealthArea.Resource, severity,
                    metricValue: projectCount, metricDetail: new Dictionary<string, string> { ["person"] = person }));
            }
        }

        return Task.FromResult<IReadOnlyList<Finding>>(findings);
    }

    /// <summary>
    /// Key-person concentration band by the number of distinct projects a person is allocated to
    /// (plan-doc: 5+ Red, 3–4 Amber, fewer than 3 not flagged). Returns null when not flagged.
    /// </summary>
    private static Severity? ConcentrationBand(int projectCount) => projectCount switch
    {
        >= 5 => Severity.Red,
        >= 3 => Severity.Amber,
        _ => null,
    };

    /// <summary>
    /// Recognises the project-manager role across the spellings the data actually uses — "Project
    /// Manager", "Project Management" (the fixture's <c>professional_group</c> value), a bare "Manager",
    /// or "PM". A brittle <c>Contains("Manager")</c> test failed on "Project Management" and produced a
    /// false "no project manager" finding.
    /// </summary>
    private static bool IsProjectManagerRole(string role)
    {
        var r = role.Trim();
        return r.Contains("Manager", StringComparison.OrdinalIgnoreCase)
               || r.Contains("Management", StringComparison.OrdinalIgnoreCase)
               || r.Equals("PM", StringComparison.OrdinalIgnoreCase);
    }

    // Over-allocation beyond this many percentage points over capacity is treated as critical (Red).
    private const double CriticalOverAllocationPoints = 20;

    /// <summary>RAG severity for an over-allocation margin — the Resource-area signal for scoring.</summary>
    private static Severity OverAllocationBand(double overByPoints) =>
        overByPoints > CriticalOverAllocationPoints ? Severity.Red : Severity.Amber;

    private static Finding Finding(
        ProjectSlice slice, Confidence confidence, string summary, SourceRef source, Severity severity) =>
        FindingFactory.Analysis(slice, "Resource", summary, source, confidence, HealthArea.Resource, severity);
}
