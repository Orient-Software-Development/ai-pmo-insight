using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Agents;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Assembles the canned fixture responses the <see cref="FakeLlmClient"/> serves, keyed by the
/// requested output type. One entry per LLM-backed agent output contract (#4 minutes, #7, #8, #9);
/// each factory may read the <see cref="LlmRequest"/> to shape its response. Populated as the LLM
/// agents are added.
/// </summary>
public static class FakeLlmFixtures
{
    public static IReadOnlyDictionary<Type, Func<LlmRequest, object>> Default()
    {
        var fixtures = new Dictionary<Type, Func<LlmRequest, object>>
        {
            // #4 Risk & Issue — risks extracted from meeting minutes.
            [typeof(MinuteRiskExtraction)] = _ => new MinuteRiskExtraction(
            [
                new ExtractedRisk(
                    Title: "Vendor delivery slip",
                    Kind: "risk",
                    Severity: "high",
                    Rationale: "Minutes note a possible two-week slip on the API integration."),
            ]),

            // #7 Narrative — LLM fallback for complex, multi-signal cases.
            [typeof(NarrativeResult)] = _ => new NarrativeResult(
                Status: "red",
                Narrative: "Multiple signals converge: a slipped milestone, forecast over budget, and an over-allocated lead.",
                Recommendation: new Recommendation(
                    Owner: "Project Manager",
                    Deadline: "next 2 weeks",
                    Action: "Escalate the vendor dependency and rebalance the lead's allocation",
                    Rationale: "Schedule, cost, and resource signals reinforce one another.")),

            // #8 Challenge — adversarial critique of the findings + narrative.
            [typeof(ChallengeResult)] = _ => new ChallengeResult(
            [
                new Critique(
                    Target: "Financial variance",
                    Concern: "The 18% overrun cites forecast vs budget but is unverified against actual spend.",
                    Severity: "medium",
                    Suggestion: "Cross-check the forecast against the actuals column before escalating."),
            ]),

            // #9 Review — anticipated stakeholder questions grouped by audience.
            [typeof(ReviewResult)] = _ => new ReviewResult(new Dictionary<string, IReadOnlyList<string>>
            {
                ["executive"] = ["When will the vendor slip be resolved, and what is the revised go-live?"],
                ["sponsor"] = ["What is the cost impact of the forecast overrun, and is more budget needed?"],
                ["data-lead"] = ["How current is the source data behind these findings?"],
                ["peer-pm"] = ["Is the over-allocated lead a risk to your dependencies?"],
            }),
        };

        return fixtures;
    }
}
