using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Tests.Analysis;

/// <summary>Builders for the deterministic analysis agents' inputs, shared across agent tests.</summary>
internal static class AnalysisFixtures
{
    public static readonly DateTimeOffset RunTime = new(2026, 07, 10, 0, 0, 0, TimeSpan.Zero);

    public static readonly SourceRef Source = new("sheet!row2", "sheet=x;row=2", "snippet");

    public static ProjectSlice Slice(string projectKey = "ALPHA", CollectedData? data = null) => new()
    {
        Run = AnalysisRun.Start(Guid.NewGuid(), RunTime),
        ProjectKey = projectKey,
        Data = data ?? CollectedData.Empty,
    };

    public static AnalysisInput Input(ProjectSlice slice, DataQualitySignal? quality = null) =>
        new(slice, quality ?? DataQualitySignal.Clean());

    public static ProjectRecord Project(
        string key = "ALPHA",
        string name = "Alpha Platform",
        double? percentComplete = 45,
        DateTimeOffset? lastUpdated = null) => new()
    {
        Key = key,
        Name = name,
        PercentComplete = percentComplete,
        LastUpdated = lastUpdated,
        Source = Source,
    };

    public static CollectedData Data(
        IReadOnlyList<ProjectRecord>? projects = null,
        IReadOnlyList<MilestoneRecord>? milestones = null,
        IReadOnlyList<BudgetLineRecord>? budgetLines = null,
        IReadOnlyList<AssignmentRecord>? assignments = null,
        IReadOnlyList<MinuteEntryRecord>? minutes = null,
        IReadOnlyList<RaidItemRecord>? raidItems = null,
        IReadOnlyList<DecisionRecord>? decisions = null,
        IReadOnlyList<ScopeChangeRecord>? scopeChanges = null) => new()
    {
        Projects = projects ?? [],
        Milestones = milestones ?? [],
        BudgetLines = budgetLines ?? [],
        Assignments = assignments ?? [],
        Minutes = minutes ?? [],
        RaidItems = raidItems ?? [],
        Decisions = decisions ?? [],
        ScopeChanges = scopeChanges ?? [],
    };
}
