using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Tests.HealthScoring;

/// <summary>Shared builders for the health-scoring service/override tests.</summary>
internal static class HealthScoringFixtures
{
    public static readonly DateTimeOffset T0 = new(2026, 07, 10, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A valid EXAMPLE-shaped options object; tests tweak facets as needed.</summary>
    public static HealthScoringOptions Options() => new()
    {
        WeightTotal = 100,
        Weights = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Schedule"] = 20,
            ["Budget"] = 30,
            ["Risk"] = 30,
            ["Resource"] = 15,
            ["DataQuality"] = 5,
        },
        SeverityScores = new(StringComparer.OrdinalIgnoreCase) { ["Green"] = 100, ["Amber"] = 70, ["Red"] = 30 },
        Thresholds = new RagThresholds { Green = 80, Amber = 60 },
        ConfidenceScores = new(StringComparer.OrdinalIgnoreCase) { ["Low"] = 30, ["Medium"] = 70, ["High"] = 100 },
        ConfidenceFloor = 50,
        Overrides =
        [
            new OverrideRuleOptions { Id = "forecast-overrun-critical", Area = "Budget", WhenSeverityAtLeast = "Red", Floor = "Red" },
            new OverrideRuleOptions { Id = "critical-milestone-missed", Area = "Schedule", WhenSeverityAtLeast = "Red", Floor = "Amber" },
            new OverrideRuleOptions { Id = "critical-unmitigated-risk", Area = "Risk", WhenSeverityAtLeast = "Red", Floor = "Red" },
            new OverrideRuleOptions { Id = "key-decision-overdue", Area = "Decision", WhenSeverityAtLeast = "Red", Floor = "Amber" },
        ],
    };

    public static Finding AnalysisFinding(
        HealthArea area,
        Severity severity,
        Guid runId,
        DateTimeOffset? createdAt = null,
        Confidence confidence = Confidence.High,
        string projectKey = "ALPHA",
        string locator = "sheet!row2") =>
        Finding.Create(
            projectKey: projectKey,
            summary: $"{area} {severity}",
            citation: Citation.Create(Guid.NewGuid(), locator),
            now: createdAt ?? T0,
            runId: runId,
            producingAgent: area.ToString(),
            kind: FindingKind.Analysis,
            confidence: confidence,
            area: area,
            severity: severity);
}
