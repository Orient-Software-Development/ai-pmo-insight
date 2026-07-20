using AiPMOInsight.Application.Features.Analysis.Model;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Application.Features.Analysis.Agents;

/// <summary>
/// Agent #5 — Financial. Pure deterministic math over budget lines (no LLM): forecast-vs-budget
/// variance, burn rate, and a budget-vs-progress cross-signal (spend outrunning percent-complete),
/// plus total financial exposure. Each finding cites its budget line; confidence comes from the DQ
/// signal via <see cref="ConfidencePolicy"/>.
/// </summary>
public sealed class FinancialSkill : IAgentSkill<AnalysisInput, IReadOnlyList<Finding>>
{
    // Spend may lead progress a little before it's worth flagging (percentage points).
    private const double SpendAheadOfProgressPoints = 10;

    public string Name => "Financial";

    public Task<IReadOnlyList<Finding>> ExecuteAsync(AnalysisInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var slice = input.Slice;
        var confidence = ConfidencePolicy.FromSignals(input.Quality);
        var percentComplete = slice.Data.Projects.FirstOrDefault(p => p.Key == slice.ProjectKey)?.PercentComplete;

        var lines = slice.Data.BudgetLines.Where(b => b.ProjectKey == slice.ProjectKey).ToList();
        var findings = new List<Finding>();

        foreach (var line in lines)
        {
            if (line.Budget <= 0)
            {
                continue; // DQ flags an unusable budget; nothing to compute against.
            }

            if (line.Forecast > line.Budget)
            {
                var overPercent = (line.Forecast - line.Budget) / line.Budget * 100m;
                findings.Add(Finding(slice, confidence,
                    $"'{line.Category}' forecast exceeds budget by {overPercent:F0}% (forecast {line.Forecast:N0} vs budget {line.Budget:N0}).",
                    line.Source, OverrunBand(overPercent)));
            }

            // Spend-ahead-of-progress cross-signal — only when actuals are present (missing actuals are a
            // data-quality gap the DQ agent flags, not a financial signal here).
            if (line.Actual is { } actual && percentComplete is { } progress)
            {
                var spendPercent = (double)(actual / line.Budget) * 100;
                if (spendPercent - progress > SpendAheadOfProgressPoints)
                {
                    findings.Add(Finding(slice, confidence,
                        $"'{line.Category}' spend is running ahead of progress ({spendPercent:F0}% of budget spent at {progress:F0}% complete).",
                        line.Source, Severity.Amber));
                }
            }
        }

        var overLines = lines.Where(l => l.Forecast > l.Budget).ToList();
        var exposure = overLines.Sum(l => l.Forecast - l.Budget);
        if (exposure > 0)
        {
            var currency = overLines.First().Currency;
            // Carry the amount + currency as a typed metric so the L1 roll-up can sum exposures without
            // parsing the summary string; currency is null when the source omits it (no fabrication).
            findings.Add(FindingFactory.Analysis(slice, "Financial",
                $"Total financial exposure across budget lines is {exposure:N0}.",
                overLines[0].Source, confidence, HealthArea.Budget, Severity.Amber,
                metricValue: exposure, metricUnit: currency));
        }

        return Task.FromResult<IReadOnlyList<Finding>>(findings);
    }

    // A forecast overrun of more than this share of budget is treated as critical (Red). EXAMPLE band
    // — mirrors the PRD's "forecast overrun >15% → Red"; final numbers are the PMO's.
    private const decimal CriticalOverrunPercent = 15m;

    /// <summary>RAG severity for a forecast-overrun percentage — the Budget-area signal for scoring.</summary>
    private static Severity OverrunBand(decimal overPercent) =>
        overPercent > CriticalOverrunPercent ? Severity.Red : Severity.Amber;

    private static Finding Finding(
        ProjectSlice slice, Confidence confidence, string summary, SourceRef source, Severity severity) =>
        FindingFactory.Analysis(slice, "Financial", summary, source, confidence, HealthArea.Budget, severity);
}
