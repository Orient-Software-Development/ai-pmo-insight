namespace AiPMOInsight.Domain.Findings;

/// <summary>
/// What role a finding plays in the pipeline output. The read surface partitions on this so the
/// Level-2 view can show four sections from a single aggregate / one query (design decision 6):
/// analytic findings, the synthesised narrative, the adversarial challenge, and the review.
/// </summary>
public enum FindingKind
{
    /// <summary>A concrete analytic finding from a data/analysis agent (#2, #3, #4, #5, #6).</summary>
    Analysis = 0,

    /// <summary>The synthesised prose status + recommendation (#7 Narrative).</summary>
    Narrative = 1,

    /// <summary>An adversarial critique of the findings + narrative (#8 Challenge).</summary>
    Challenge = 2,

    /// <summary>Anticipated stakeholder questions grouped by audience (#9 Review).</summary>
    Review = 3,
}
