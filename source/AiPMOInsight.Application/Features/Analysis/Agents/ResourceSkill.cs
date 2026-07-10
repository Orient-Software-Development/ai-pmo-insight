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
                findings.Add(Finding(slice, confidence,
                    $"{assignment.Person} is over-allocated at {assignment.AllocationPercent:F0}% against {assignment.CapacityPercent:F0}% capacity.",
                    assignment.Source));
            }

            if (assignment.OnLeave && assignment.AllocationPercent >= HeavyAllocation)
            {
                findings.Add(Finding(slice, confidence,
                    $"{assignment.Person} is on leave while carrying a heavy allocation ({assignment.AllocationPercent:F0}%) — concentration risk.",
                    assignment.Source));
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
                    person.First().Source));
            }
        }

        // Missing key role: no Project Manager on the project.
        if (assignments.Count > 0 &&
            !assignments.Any(a => a.Role.Contains("Manager", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(Finding(slice, confidence,
                "No Project Manager is assigned to the project (missing key role).",
                assignments[0].Source));
        }

        return Task.FromResult<IReadOnlyList<Finding>>(findings);
    }

    private static Finding Finding(ProjectSlice slice, Confidence confidence, string summary, SourceRef source) =>
        FindingFactory.Analysis(slice, "Resource", summary, source, confidence);
}
