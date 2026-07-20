using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Application.Features.DataQuality;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// Agent #2 — Data Quality. Pure deterministic checks (no LLM) over one project's records: missing
/// fields, stale updates, and inconsistent (orphan) project references. Emits a DQ finding per
/// issue (each cited, High confidence — we directly observed the gap) and a
/// <see cref="DataQualitySignal"/> that downstream agents turn into their own findings' confidence
/// via <see cref="ConfidencePolicy"/>.
/// </summary>
public sealed class DataQualitySkill(DataQualityOptions options) : IAgentSkill<ProjectSlice, DataQualityResult>
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
                    "Enter the project name in the source record.", signalKind: DataQualityFindingKeys.SignalKinds.Missing));
            }

            if (project.PercentComplete is null)
            {
                missing++;
                findings.Add(Flag(slice, "Project percent-complete is missing.", project.Source, Severity.Amber,
                    "Enter the project's % complete.", signalKind: DataQualityFindingKeys.SignalKinds.Missing));
            }

            if (project.LastUpdated is null)
            {
                missing++;
                findings.Add(Flag(slice, "Project has no last-updated date.", project.Source, Severity.Amber,
                    "Set the project's last-updated date.", signalKind: DataQualityFindingKeys.SignalKinds.Missing));
            }
        }

        // Milestones missing a due date can't be assessed for adherence downstream.
        foreach (var milestone in slice.Data.Milestones.Where(m => m.ProjectKey == slice.ProjectKey && m.DueDate is null))
        {
            missing++;
            findings.Add(Flag(slice, $"Milestone '{milestone.Name}' has no due date.", milestone.Source, Severity.Amber,
                "Add a due date to the milestone.", signalKind: DataQualityFindingKeys.SignalKinds.Missing));
        }

        // Budget lines missing actuals — a completeness gap (L3 #6).
        foreach (var line in slice.Data.BudgetLines.Where(b => b.ProjectKey == slice.ProjectKey && b.Actual is null))
        {
            missing++;
            findings.Add(Flag(slice, $"Budget category '{line.Category}' is missing actuals.", line.Source, Severity.Amber,
                "Enter actual spend to date for this budget line.", signalKind: DataQualityFindingKeys.SignalKinds.Missing));
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
                    metricValue: (decimal)Math.Round(ageDays.Value), metricUnit: "days", signalKind: DataQualityFindingKeys.SignalKinds.Stale));
            }
        }

        // Per-risk/issue staleness (L3 #1): a RAID item not reviewed within the window is a DQ gap. The
        // window is configured (DataQuality:RiskStaleThresholdDays, EXAMPLE — PMO tunes at kickoff); the
        // age is a structured metric.
        foreach (var raid in slice.Data.RaidItems.Where(r => r.ProjectKey == slice.ProjectKey && r.LastUpdated is not null))
        {
            var raidAge = (slice.Run.StartedAt - raid.LastUpdated!.Value).TotalDays;
            if (raidAge > options.RiskStaleThresholdDays)
            {
                findings.Add(Flag(slice, $"{raid.Type} '{raid.Description}' has not been updated in {raidAge:F0} days.",
                    raid.Source, Severity.Amber, "Review and update the RAID item.",
                    metricValue: (decimal)Math.Round(raidAge), metricUnit: "days"));
            }
        }

        // Resource-plan vs time-entries consistency (L3 #3, POC). Only when the project has time data: a
        // person allocated but with no time logged — or time logged by someone off the plan — is a gap.
        if (slice.Data.TimeEntries.Any(t => t.ProjectKey == slice.ProjectKey))
        {
            var planned = slice.Data.Assignments
                .Where(a => a.ProjectKey == slice.ProjectKey && a.AllocationPercent > 0).ToList();
            var logged = slice.Data.TimeEntries
                .Where(t => t.ProjectKey == slice.ProjectKey && t.HoursLogged > 0).ToList();
            var loggedPeople = logged.Select(t => t.Person).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var plannedPeople = planned.Select(a => a.Person).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var a in planned.Where(a => !loggedPeople.Contains(a.Person)))
            {
                findings.Add(Flag(slice, $"{a.Person} is allocated ({a.AllocationPercent:F0}%) but has logged no time.",
                    a.Source, Severity.Amber, "Confirm the allocation, or log time for this person."));
            }

            foreach (var person in loggedPeople.Where(p => !plannedPeople.Contains(p)))
            {
                var src = logged.First(t => string.Equals(t.Person, person, StringComparison.OrdinalIgnoreCase)).Source;
                findings.Add(Flag(slice, $"{person} logged time but is not on the resource plan.",
                    src, Severity.Amber, "Add the person to the plan, or correct the time entry."));
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
                "Correct the project-id reference, or add the missing project row.", signalKind: DataQualityFindingKeys.SignalKinds.Orphan));
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
                if (score >= options.DuplicateScoreThreshold)
                {
                    findings.Add(DuplicateCandidate(slice, project, other, score));
                }
            }
        }

        // Areas-completeness grid (L3 #7, POC — flagged): per input category, the % of that category's
        // records that have all their EXAMPLE mandatory fields present. Surfaced as one informational grid
        // finding (Green, excluded from scoring); the mandatory-field set is a placeholder (PMO tunes).
        if (project is not null)
        {
            findings.Add(CompletenessGrid(slice));
        }

        var signal = new DataQualitySignal
        {
            MissingFieldCount = missing,
            LastUpdateAgeDays = ageDays,
            SourceConsistent = consistent,
        };

        return Task.FromResult(new DataQualityResult(findings, signal));
    }

    // The 8 input categories (NOT the 5 HealthArea buckets) and their EXAMPLE mandatory fields (POC).
    private static Finding CompletenessGrid(ProjectSlice slice)
    {
        var key = slice.ProjectKey;
        var d = slice.Data;
        var grid = new Dictionary<string, string>
        {
            [DataQualityFindingKeys.Kind] = DataQualityFindingKeys.Kinds.CompletenessGrid,
            [DataQualityFindingKeys.Remediation] = "Fill the missing mandatory fields per category (POC mandatory-field set).",
            ["Schedule"] = Pct(d.Milestones.Where(m => m.ProjectKey == key),
                m => !string.IsNullOrWhiteSpace(m.Name) && m.DueDate is not null),
            ["Budget"] = Pct(d.BudgetLines.Where(b => b.ProjectKey == key),
                b => b.Budget > 0 && b.Forecast > 0 && b.Actual is not null),
            ["Scope"] = Pct(d.ScopeChanges.Where(s => s.ProjectKey == key),
                s => !string.IsNullOrWhiteSpace(s.Title) && s.Type is not null && s.Status is not null),
            ["Resources"] = Pct(d.Assignments.Where(a => a.ProjectKey == key),
                a => !string.IsNullOrWhiteSpace(a.Person) && !string.IsNullOrWhiteSpace(a.Role) && a.AllocationPercent > 0),
            ["Risks"] = Pct(d.RaidItems.Where(r => r.ProjectKey == key),
                r => !string.IsNullOrWhiteSpace(r.Description) && r.Severity is not null && r.Status is not null),
            ["Decisions"] = Pct(d.Decisions.Where(x => x.ProjectKey == key),
                x => !string.IsNullOrWhiteSpace(x.Title) && x.Owner is not null && x.NeededBy is not null && x.Status is not null),
            ["Minutes"] = Pct(d.Minutes.Where(x => x.ProjectKey == key), x => !string.IsNullOrWhiteSpace(x.Text)),
            ["Time"] = Pct(d.TimeEntries.Where(t => t.ProjectKey == key), t => t.HoursLogged > 0),
        };

        return FindingFactory.Analysis(slice, "DataQuality", $"Areas-completeness grid for '{key}'.",
            new SourceRef($"completeness:{key}"), Confidence.High, HealthArea.DataQuality, Severity.Green,
            metricDetail: grid);
    }

    // Percentage of records that satisfy the completeness predicate; "n/a" when the category has no records.
    private static string Pct<T>(IEnumerable<T> records, Func<T, bool> complete)
    {
        var list = records.ToList();
        return list.Count == 0 ? "n/a" : ((int)Math.Round(100.0 * list.Count(complete) / list.Count)).ToString();
    }

    /// <summary>
    /// POC duplicate score (0–100, configured weights — <see cref="DataQualityOptions.DuplicateWeights"/>):
    /// name-token Jaccard × NameSimilarity + same customer × SameCustomer + any shared resource ×
    /// SharedResource. Name similarity is the discriminator — same-customer/shared-PM alone can't cross
    /// the threshold when the configured NameSimilarity weight dominates.
    /// </summary>
    private int DuplicateScore(ProjectRecord a, ProjectRecord b, CollectedData data)
    {
        var name = NameSimilarity(a.Name, b.Name);
        var sameCustomer = !string.IsNullOrWhiteSpace(a.Customer)
            && string.Equals(a.Customer, b.Customer, StringComparison.OrdinalIgnoreCase);
        var sharedResource = SharedResourceCount(a.Key, b.Key, data) > 0;
        var weights = options.DuplicateWeights;
        return (int)Math.Round(name * weights.NameSimilarity
            + (sameCustomer ? weights.SameCustomer : 0)
            + (sharedResource ? weights.SharedResource : 0));
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
            [DataQualityFindingKeys.Kind] = DataQualityFindingKeys.Kinds.DuplicateCandidate,
            [DataQualityFindingKeys.Candidate] = b.Key,
            [DataQualityFindingKeys.CandidateName] = b.Name,
            [DataQualityFindingKeys.Score] = score.ToString(),
            [DataQualityFindingKeys.Remediation] = "Review the pair and record Merge or Keep-separate — the system never merges automatically.",
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
        string remediation, decimal? metricValue = null, string? metricUnit = null,
        string signalKind = DataQualityFindingKeys.SignalKinds.None) =>
        FindingFactory.Analysis(slice, "DataQuality", summary, source, Confidence.High, HealthArea.DataQuality,
            severity, metricValue: metricValue, metricUnit: metricUnit,
            // signalKind maps the finding to the DQ signal component it represents (missing/stale/orphan/none),
            // so the L3 read can reconstruct the signal and compute each item's confidence lift (L3 #5).
            metricDetail: new Dictionary<string, string>
            {
                [DataQualityFindingKeys.Remediation] = remediation,
                [DataQualityFindingKeys.SignalKind] = signalKind,
            });

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
