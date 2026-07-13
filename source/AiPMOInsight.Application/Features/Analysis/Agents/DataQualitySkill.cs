using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// Agent #2 — Data Quality. Pure deterministic checks (no LLM) over one project's records: missing
/// fields, stale updates, and inconsistent (orphan) project references. Emits a DQ finding per
/// issue (each cited, High confidence — we directly observed the gap) and a
/// <see cref="DataQualitySignal"/> that downstream agents turn into their own findings' confidence
/// via <see cref="ConfidencePolicy"/>.
/// </summary>
public sealed class DataQualitySkill : IAgentSkill<ProjectSlice, DataQualityResult>
{
    private const double StaleThresholdDays = 30;

    public string Name => "DataQuality";

    public Task<DataQualityResult> ExecuteAsync(ProjectSlice slice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slice);

        var findings = new List<Finding>();
        var missing = 0;

        var project = slice.Data.Projects.FirstOrDefault(p => p.Key == slice.ProjectKey);

        if (project is not null)
        {
            if (string.IsNullOrWhiteSpace(project.Name))
            {
                missing++;
                findings.Add(Flag(slice, "Project name is missing.", project.Source));
            }

            if (project.PercentComplete is null)
            {
                missing++;
                findings.Add(Flag(slice, "Project percent-complete is missing.", project.Source));
            }

            if (project.LastUpdated is null)
            {
                missing++;
                findings.Add(Flag(slice, "Project has no last-updated date.", project.Source));
            }
        }

        // Milestones missing a due date can't be assessed for adherence downstream.
        foreach (var milestone in slice.Data.Milestones.Where(m => m.ProjectKey == slice.ProjectKey && m.DueDate is null))
        {
            missing++;
            findings.Add(Flag(slice, $"Milestone '{milestone.Name}' has no due date.", milestone.Source));
        }

        // Staleness.
        double? ageDays = null;
        if (project?.LastUpdated is { } lastUpdated)
        {
            ageDays = (slice.Run.StartedAt - lastUpdated).TotalDays;
            if (ageDays > StaleThresholdDays)
            {
                findings.Add(Flag(slice, $"Project data is stale (last updated {ageDays:F0} days ago).", project.Source));
            }
        }

        // Consistency: any child record referencing a project key with no defining Project row.
        var knownKeys = slice.Data.Projects.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphans = OrphanSources(slice.Data, knownKeys).ToList();
        var consistent = orphans.Count == 0;
        foreach (var orphan in orphans)
        {
            findings.Add(Flag(slice, "Record references an unknown project id (inconsistent source).", orphan));
        }

        var signal = new DataQualitySignal
        {
            MissingFieldCount = missing,
            LastUpdateAgeDays = ageDays,
            SourceConsistent = consistent,
        };

        return Task.FromResult(new DataQualityResult(findings, signal));
    }

    private static Finding Flag(ProjectSlice slice, string summary, SourceRef source) =>
        FindingFactory.Analysis(slice, "DataQuality", summary, source, Confidence.High);

    private static IEnumerable<SourceRef> OrphanSources(CollectedData data, HashSet<string> knownKeys)
    {
        foreach (var m in data.Milestones.Where(x => !knownKeys.Contains(x.ProjectKey)))
        {
            yield return m.Source;
        }

        foreach (var b in data.BudgetLines.Where(x => !knownKeys.Contains(x.ProjectKey)))
        {
            yield return b.Source;
        }

        foreach (var a in data.Assignments.Where(x => !knownKeys.Contains(x.ProjectKey)))
        {
            yield return a.Source;
        }
    }
}
