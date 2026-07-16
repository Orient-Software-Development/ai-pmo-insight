## Context

`NarrativeSkill` (agent #7, hybrid template-first) computes a `NarrativeResult { Status, Narrative,
Recommendation }` where `Recommendation` is `{ Owner, Deadline, Action, Rationale }`. Both paths fill it:
the template classifier with generic values (`"Project Manager"` / `"next 2 weeks"` / `"this week"` /
`"n/a"`), the LLM path with specifics from the model. `ToFinding` then flattens all of it into one
`Summary` string and creates a `FindingKind.Narrative` finding.

The `Finding` aggregate carries an optional `MetricDetail` (`IReadOnlyDictionary<string,string>?`,
persisted as `jsonb`) added by #46 — the place a structured recommendation should live. Ticket #48
(parent #8); dependencies #46 + #47 both landed.

## Goals / Non-Goals

**Goals:**

- Persist the recommendation's `owner` / `deadline` / `action` / `rationale` as structured data on the
  narrative finding (via `MetricDetail`), so consumers read fields, not prose.
- Keep the prose summary unchanged (back-compat; the History/L2 views still render it today).
- No prompt / LLM-contract / template-classifier change — only stop discarding the structure.

**Non-Goals:**

- Enriching `owner` / `deadline` from decision data (#47) — a soft follow-on.
- The L1/L2 view rendering of the structured recommendation — presentation, separate tickets.
- Confidence in the detail map — it stays the finding's existing `Confidence`.

## Decisions

**1. Stamp `MetricDetail`, keep the summary.** `ToFinding` adds a `MetricDetail` map
`{ owner, deadline, action, rationale }` from the `Recommendation`, and still builds the same summary
string. Additive: existing consumers that read the summary are unaffected; new consumers read the map.
`MetricValue`/`MetricUnit` stay null (the recommendation is not a single number).

**2. Both paths, same stamping.** The stamping happens in the shared `ToFinding`, so the template and LLM
paths both get the structured detail with no path-specific code. The template path's generic values are
honestly generic (that's what it computed); the LLM path's are specific.

**3. Stable key names.** Lower-case `owner` / `deadline` / `action` / `rationale` — a small fixed contract
the L1/L2 renderers can rely on. Documented here so the presentation tickets know the keys.

## Risks / Trade-offs

- **Very low risk.** One method adds a dictionary to an already-created finding; no schema change (the
  `jsonb` column + converter landed in #46), no new dependency, no prompt change. The round-trip of
  `MetricDetail` through EF is already tested in #46.
- **Generic template values could read as "fake specifics."** They are honestly generic ("Project
  Manager" / "next 2 weeks"); a later change can enrich them from decision owners/`needed_by` (#47). Not
  fabricated — they are the template's computed defaults, same as the prose already shows.
