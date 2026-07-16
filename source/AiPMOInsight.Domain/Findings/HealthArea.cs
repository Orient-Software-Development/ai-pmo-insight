namespace AiPMOInsight.Domain.Findings;

/// <summary>
/// The structured health dimension an <see cref="FindingKind.Analysis"/> finding speaks to, so the
/// health scorer can group findings by area and weight them (Phase 4). Maps (roughly) 1:1 to the
/// deterministic agents — Status → <see cref="Schedule"/>, Financial → <see cref="Budget"/>,
/// Risk &amp; Issue → <see cref="Risk"/>, Resource → <see cref="Resource"/>, Data Quality →
/// <see cref="DataQuality"/> — but is set per finding, not per agent, so one agent may contribute to
/// more than one area. The set is intentionally extendable: adding a member does not break existing
/// findings (persisted as strings).
/// </summary>
public enum HealthArea
{
    /// <summary>Schedule / timeline health (milestone slippage, delays). Status agent (#3).</summary>
    Schedule = 0,

    /// <summary>Budget / cost health (forecast overrun, burn). Financial agent (#5).</summary>
    Budget = 1,

    /// <summary>Risk &amp; issue health (unmitigated risks, open issues). Risk &amp; Issue agent (#4).</summary>
    Risk = 2,

    /// <summary>Resourcing health (over-allocation, key-role gaps). Resource agent (#6).</summary>
    Resource = 3,

    /// <summary>Data-quality health (missing fields, staleness). Data Quality agent (#2).</summary>
    DataQuality = 4,

    /// <summary>Decision health (overdue / due-soon decisions, blocked work). Decision agent.</summary>
    Decision = 5,
}
