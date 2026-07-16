## Why

PRD US-4 requires each recommended action to name an **owner, deadline, and confidence** so the
recommendation is actionable, not advisory. The Narrative agent **already produces a structured
`Recommendation { Owner, Deadline, Action, Rationale }`** — but `NarrativeSkill.ToFinding` **flattens it
into the `Summary` string** (`"[status] … Recommendation (owner, by deadline): action — rationale"`), so a
consumer (the L1/L2 recommendation panel) can't read the fields as data — it would have to parse prose.
This is the "trapped in a string" case #48 (parent #8) closes.

The fix is small: stamp the recommendation's fields onto the finding's **structured metric detail** (the
`MetricDetail` map added by #46), keeping the summary for back-compat. Both dependencies are already
landed — #46 (the metric field) and #47 (Decisions, the eventual source for real owner/deadline).

## What Changes

- **`NarrativeSkill` stamps the recommendation as structured data.** In addition to the prose summary
  (unchanged), the narrative finding carries `MetricDetail` with `owner`, `deadline`, `action`, and
  `rationale` from the `Recommendation` record. Both narrative paths already fill those fields — the
  template path with generic values ("Project Manager" / "next 2 weeks"), the LLM path with specifics — so
  no prompt or LLM-contract change is needed; only the persistence stops discarding the structure.
- **Confidence stays where it is.** The recommendation's confidence is the finding's existing
  `Confidence`; it is not duplicated into the detail map.

Not in scope: enriching `owner` / `deadline` from decision data (#47) — a soft follow-on; the L1/L2 view
rendering of the structured recommendation (presentation, separate); any change to the Narrative prompt,
the LLM contract, or the template classifier.

## Capabilities

### Modified Capabilities

- `analysis-pipeline`: the Narrative agent's output now carries its recommendation as a **structured
  finding metric** (`owner` / `deadline` / `action` / `rationale`) alongside the existing prose summary,
  instead of only flattening it into the summary string.

## Impact

- **Application:** `NarrativeSkill.ToFinding` populates `MetricDetail` (both template and LLM paths). No
  other change.
- **Domain / Infra / API / client:** none (the `Finding` metric + persistence landed in #46; the L1/L2
  views that read the structured recommendation are separate presentation tickets).
- **Tests (TDD):** `NarrativeAgentTests` — the narrative finding carries `owner`/`deadline`/`action`/
  `rationale` in `MetricDetail`, and still keeps the summary prose. Full suite stays green (baseline 139
  Application + 126 Api = 265).
- **Docs:** tick #48; update `docs/l1-`/`l2-` follow-up registers (#7 — recommendation structured;
  view rendering still a presentation follow-on).
