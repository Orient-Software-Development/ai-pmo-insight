using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Model;

/// <summary>
/// Where a parsed record came from inside its upload. Threaded from the Data Collector into every
/// downstream finding so a deterministic math finding cites the exact cell / XML node / minutes
/// passage it was derived from. The upload id is known at run scope, so a <see cref="SourceRef"/>
/// becomes a full <see cref="Citation"/> via <see cref="ToCitation"/> when a finding is emitted.
/// </summary>
public sealed record SourceRef(string Locator, string? StructuredExcerpt = null, string? TextSnippet = null)
{
    public Citation ToCitation(Guid uploadId) =>
        Citation.Create(uploadId, Locator, StructuredExcerpt, TextSnippet);
}

/// <summary>RAID category for a <see cref="RaidItemRecord"/>.</summary>
public enum RaidType
{
    Risk,
    Assumption,
    Issue,
    Dependency,
}

// Transient analysis-run models (PRD: parsed intermediate structures are not part of the durable
// domain model). Fields are what the deterministic agents (#2, #3, #5, #6) and #4 need; nullable
// where the source may omit them (Data Quality flags the gaps).

public sealed record ProjectRecord
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public double? PercentComplete { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
    public required SourceRef Source { get; init; }
}

public sealed record MilestoneRecord
{
    public required string ProjectKey { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }
    public string? Status { get; init; }
    public string? DependsOn { get; init; }
    public required SourceRef Source { get; init; }
}

public sealed record BudgetLineRecord
{
    public required string ProjectKey { get; init; }
    public required string Category { get; init; }
    public decimal Budget { get; init; }
    public decimal Forecast { get; init; }
    public decimal Actual { get; init; }
    public required SourceRef Source { get; init; }
}

public sealed record AssignmentRecord
{
    public required string ProjectKey { get; init; }
    public required string Person { get; init; }
    public required string Role { get; init; }
    public double AllocationPercent { get; init; }
    public double CapacityPercent { get; init; } = 100;
    public bool OnLeave { get; init; }
    public required SourceRef Source { get; init; }
}

public sealed record MinuteEntryRecord
{
    public required string ProjectKey { get; init; }

    /// <summary>Meeting date if the minutes state one; null when it cannot be determined.</summary>
    public DateTimeOffset? Date { get; init; }
    public required string Text { get; init; }
    public required SourceRef Source { get; init; }
}

public sealed record RaidItemRecord
{
    public required string ProjectKey { get; init; }
    public required RaidType Type { get; init; }
    public required string Description { get; init; }
    public string? Severity { get; init; }
    public string? Status { get; init; }
    public required SourceRef Source { get; init; }
}

/// <summary>
/// The Data Collector's output: the typed records parsed from one upload, grouped by category. This
/// is the shared input the analysis agents (#2–#6) read; it is never persisted.
/// </summary>
public sealed record CollectedData
{
    public required IReadOnlyList<ProjectRecord> Projects { get; init; }
    public required IReadOnlyList<MilestoneRecord> Milestones { get; init; }
    public required IReadOnlyList<BudgetLineRecord> BudgetLines { get; init; }
    public required IReadOnlyList<AssignmentRecord> Assignments { get; init; }
    public required IReadOnlyList<MinuteEntryRecord> Minutes { get; init; }
    public required IReadOnlyList<RaidItemRecord> RaidItems { get; init; }

    public static CollectedData Empty { get; } = new()
    {
        Projects = [],
        Milestones = [],
        BudgetLines = [],
        Assignments = [],
        Minutes = [],
        RaidItems = [],
    };
}
