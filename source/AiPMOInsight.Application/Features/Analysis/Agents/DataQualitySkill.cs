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
                findings.Add(Flag(slice, "Project name is missing.", project.Source, Severity.Amber,
                    "Enter the project name in the source record."));
            }

            if (project.PercentComplete is null)
            {
                missing++;
                findings.Add(Flag(slice, "Project percent-complete is missing.", project.Source, Severity.Amber,
                    "Enter the project's % complete."));
            }

            if (project.LastUpdated is null)
            {
                missing++;
                findings.Add(Flag(slice, "Project has no last-updated date.", project.Source, Severity.Amber,
                    "Set the project's last-updated date."));
            }
        }

        // Milestones missing a due date can't be assessed for adherence downstream.
        foreach (var milestone in slice.Data.Milestones.Where(m => m.ProjectKey == slice.ProjectKey && m.DueDate is null))
        {
            missing++;
            findings.Add(Flag(slice, $"Milestone '{milestone.Name}' has no due date.", milestone.Source, Severity.Amber,
                "Add a due date to the milestone."));
        }

        // Staleness. The age (days) is carried as a structured metric (L3 #8), not only in the summary.
        double? ageDays = null;
        if (project?.LastUpdated is { } lastUpdated)
        {
            ageDays = (slice.Run.StartedAt - lastUpdated).TotalDays;
            if (ageDays > StaleThresholdDays)
            {
                findings.Add(Flag(slice, $"Project data is stale (last updated {ageDays:F0} days ago).",
                    project.Source, Severity.Amber, "Re-export the latest project data from Orbit.",
                    metricValue: (decimal)Math.Round(ageDays.Value), metricUnit: "days"));
            }
        }

        // Consistency: any child record referencing a project key with no defining Project row.
        var knownKeys = slice.Data.Projects.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphans = OrphanSources(slice.Data, knownKeys).ToList();
        var consistent = orphans.Count == 0;
        foreach (var orphan in orphans)
        {
            // An orphan reference means the data set is internally inconsistent — critical for DQ.
            findings.Add(Flag(slice, "Record references an unknown project id (inconsistent source).", orphan, Severity.Red,
                "Correct the project-id reference, or add the missing project row."));
        }

        // Duplicate-identity candidates (L3 #4, POC heuristic — flagged). Compare this project against the
        // others on name similarity + same customer + shared-resource overlap; above a placeholder threshold
        // emit an Amber candidate. Emitted once per pair (from the canonically-first key). NEVER auto-merges
        // (US-2) — the finding only surfaces the pair for a human to decide.
        if (project is not null)
        {
            foreach (var other in slice.Data.Projects)
            {
                if (string.CompareOrdinal(project.Key, other.Key) >= 0)
                {
                    continue; // skip self and the second key of each pair (one emission per pair)
                }

                var score = DuplicateScore(project, other, slice.Data);
                if (score >= DuplicateScoreThreshold)
                {
                    findings.Add(DuplicateCandidate(slice, project, other, score));
                }
            }
        }

        var signal = new DataQualitySignal
        {
            MissingFieldCount = missing,
            LastUpdateAgeDays = ageDays,
            SourceConsistent = consistent,
        };

        return Task.FromResult(new DataQualityResult(findings, signal));
    }

    // POC duplicate-similarity threshold (0–100 EXAMPLE placeholder — PMO tunes at kickoff).
    private const int DuplicateScoreThreshold = 60;

    /// <summary>
    /// POC duplicate score (0–100, EXAMPLE weights): name-token Jaccard ×50 + same customer ×30 +
    /// any shared resource ×20. Name similarity is the discriminator — same-customer/shared-PM alone
    /// can't cross the threshold.
    /// </summary>
    private static int DuplicateScore(ProjectRecord a, ProjectRecord b, CollectedData data)
    {
        var name = NameSimilarity(a.Name, b.Name);
        var sameCustomer = !string.IsNullOrWhiteSpace(a.Customer)
            && string.Equals(a.Customer, b.Customer, StringComparison.OrdinalIgnoreCase);
        var sharedResource = SharedResourceCount(a.Key, b.Key, data) > 0;
        return (int)Math.Round(name * 50 + (sameCustomer ? 30 : 0) + (sharedResource ? 20 : 0));
    }

    private static double NameSimilarity(string a, string b)
    {
        var ta = NameTokens(a);
        var tb = NameTokens(b);
        if (ta.Count == 0 || tb.Count == 0)
        {
            return 0;
        }

        var union = ta.Union(tb).Count();
        return union == 0 ? 0 : (double)ta.Intersect(tb).Count() / union; // Jaccard over tokens
    }

    private static HashSet<string> NameTokens(string name) =>
        name.Split([' ', '-', '(', ')', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    private static int SharedResourceCount(string keyA, string keyB, CollectedData data)
    {
        var peopleB = data.Assignments
            .Where(x => string.Equals(x.ProjectKey, keyB, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Person)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return data.Assignments
            .Where(x => string.Equals(x.ProjectKey, keyA, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Person)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count(peopleB.Contains);
    }

    private static Finding DuplicateCandidate(ProjectSlice slice, ProjectRecord a, ProjectRecord b, int score)
    {
        var detail = new Dictionary<string, string>
        {
            ["kind"] = "duplicate-candidate",
            ["candidate"] = b.Key,
            ["candidateName"] = b.Name,
            ["score"] = score.ToString(),
            ["remediation"] = "Review the pair and record Merge or Keep-separate — the system never merges automatically.",
        };

        return FindingFactory.Analysis(
            slice, "DataQuality",
            $"Possible duplicate of '{b.Key}' ({b.Name}) — similarity {score}%.",
            a.Source, Confidence.Medium, HealthArea.DataQuality, Severity.Amber,
            metricValue: score, metricUnit: "percent", metricDetail: detail);
    }

    // Attaches a deterministic, per-check-type suggested remediation (static rule-map, no LLM) plus an
    // optional metric (e.g. the staleness age in days) so the L3 view can render Remediation + Age columns.
    private static Finding Flag(ProjectSlice slice, string summary, SourceRef source, Severity severity,
        string remediation, decimal? metricValue = null, string? metricUnit = null) =>
        FindingFactory.Analysis(slice, "DataQuality", summary, source, Confidence.High, HealthArea.DataQuality,
            severity, metricValue: metricValue, metricUnit: metricUnit,
            metricDetail: new Dictionary<string, string> { ["remediation"] = remediation });

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
