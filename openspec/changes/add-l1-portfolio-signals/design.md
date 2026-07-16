## Context

Analysis is a write-time pipeline (`#1 Data Collector → #2 Data Quality → parallel(#3 Status, #4 Risk,
#5 Financial, #6 Resource) → #7 Narrative → #8 Challenge → #9 Review → persist`). Health scoring is a
**read-side** query (`HealthScoringService`) over persisted findings. The L1 view reads
`GET /api/portfolio` (`ScorePortfolio`), which fans out over the scorer.

Two facts drive this change, both verified in code:

1. **Every agent already holds the whole portfolio.** `ProjectSlice.Data` is the full `CollectedData` — its
   own doc comment says *"Agents filter `Data` to `ProjectKey`"*. So cross-project counts (key-person
   concentration) need no new architecture — just read the unfiltered collection.
2. **Numbers are trapped in prose.** `Finding` carries only `Summary / Area / Severity / Confidence /
   Citation`; the Financial agent's exposure € and the Narrative's `Recommendation { Owner, Deadline,
   Action }` are flattened into the `Summary` string at persist time.

The two agent bugs (`ResourceSkill` role match; `StatusSkill` ignoring `MilestoneRecord.Status`) are
confirmed against the fixtures. Scoring weights/overrides are **config-driven** (`HealthScoringOptions`,
`IsPlaceholder = true`); the shipped placeholder currently uses 5 areas (Schedule 20 / Budget 30 / Risk 30 /
Resource 15 / DataQuality 5) and 3 overrides.

## Goals / Non-Goals

**Goals:**

- Remove the two deterministic-agent falsehoods (no-PM, missed-milestone-as-green).
- Surface the L1 panels whose data already exists: financial exposure (€), decision backlog, key-person
  concentration, customer-exposure proxy — from live findings, never fabricated.
- Add the one genuinely-missing area (Decisions) end to end: parse → agent → area → override → roll-up.
- Give `Finding` a structured metric so numbers (and the recommendation) are data, not prose — reused later
  by L3.
- TDD throughout; keep the full backend suite green; scoring numbers stay `EXAMPLE` placeholders.

**Non-Goals:**

- L2 / L3 view work (this change touches shared agents + the L1 surface only; the L2 decisions panel is
  lit up as a side effect but not restyled here).
- `MilestoneRecord.baseline_date` / `is_critical` (needed for schedule-slip + as the override's ideal
  trigger) — flagged follow-on.
- US-7 `× absence` combine (no parsed absence signal) — concentration only.
- Scope area/agent; run-over-run "this-period progress"; the true commercial-risk signal (needs client
  contract/margin/SLA data).

## Decisions

**1. Key-person: concentration-only now, `× absence` deferred.** The plan doc's *scoring* rule (line 148)
is pure concentration (5+/3–4/<3); US-7's `× absence` is an enhancement. The fixture has **no parsed
absence signal** (`resources.csv` has no `OnLeave` column → `AssignmentRecord.OnLeave` is always false;
`time-used.csv` is not parsed). Rather than fabricate or block, ship concentration-only (which is the
actual scoring rule) and flag `× absence` as a follow-on needing either a parsed `OnLeave` column or a
time-used parser. *Alternative considered:* seed an `OnLeave` column into the Resources fixture now —
rejected for this change to keep the fixture honest to Orbit's export shape until we confirm where absence
lives (kick-off question).

**2. Concentration attaches per project, not portfolio-once.** A person on 5 projects is a risk **on each**
of those projects, so the finding is emitted for each project slice (deterministic, cited to that project's
assignment row). The L1 roll-up then reports the distinct people over threshold. *Alternative:* emit once
portfolio-wide — rejected because per-project findings keep the L2 project view able to show "this project
depends on an over-committed person" and preserve citations.

**3. `Finding` metric is optional + additive.** Add nullable structured fields (a small typed shape:
`MetricValue` decimal? + `MetricUnit` string?, plus an optional `Detail` key/value map for the
recommendation's owner/deadline/action) — never required, so existing findings and the `Kind==Analysis ⇒
Area+Severity` invariant are untouched. EF migration adds nullable column(s); no backfill. *Alternative:*
re-parse numbers out of `Summary` strings — rejected as fragile (format drift breaks it silently).

**4. Decision agent mirrors the RAID pattern.** `DecisionSkill` is deterministic (no LLM), reads the parsed
`DecisionRecord`s for the project, emits an `Area==Decision` finding per overdue / due-soon decision, cited
to its row, severity from overdue+blocking. Overdue = `needed_by < run date` and status ≠ `Approved`;
due-soon = `needed_by` within a window (14 days, matching the Status agent's window constant). This is the
established shape (RAID → Risk), so it carries no new architectural risk.

**5. Health scoring: add a `Decision` area + a key-decision override, both placeholder.** Add a `Decision`
weight to the placeholder `Weights` block (re-normalising is automatic — the scorer weight-normalises over
areas present) and a config override `{ Area: Decision, WhenSeverityAtLeast: <Red|Amber>, Floor: Amber }`
for "key decision overdue → minimum Amber". Numbers remain `EXAMPLE` (`IsPlaceholder` stays true). The
generic `{Area, WhenSeverityAtLeast, Floor}` override model already supports this — no engine change.

**6. Customer-exposure proxy is labelled, not fabricated.** Group scored Red/Amber projects by
`ProjectRecord.Customer` and report per-customer at-risk counts as **relationship exposure**. The response
field and the view label both say "customer exposure (at-risk projects grouped by customer)", explicitly
**not** contract/margin/SLA commercial risk. The true signal stays a documented kick-off question.

**7. Missed-milestone severity mapping.** In `StatusSkill`, when `MilestoneRecord.Status` indicates a
missed/at-risk state (e.g. `"Missed"`, `"At Risk"`), the emitted Schedule finding takes a non-Green
severity (Missed → Red, At Risk → Amber) regardless of the date-window branch, so the health `Schedule`
area can reach Red and the `critical-milestone-missed` override fires. Date-based bands still apply for
milestones without an explicit adverse status. *Alternative:* rely on `is_critical` — not on the record, so
deferred; `Status` is present and sufficient to stop the false-green.

## Risks / Trade-offs

- **EF migration on `Finding`.** Adding column(s) to a persisted aggregate is the highest-risk part. Mitigation:
  nullable + additive, no backfill; Development auto-migrates; a dedicated migration file is committed and
  applied as a deploy step in prod (per repo convention).
- **Fixture `Decisions` tab may be absent.** The parser reads a `Decisions` sheet; the consolidated
  `orbit-sample.xlsx` lists Decisions as a staged tab but the parser never read it. If the tab's headers
  don't match the reader (PascalCase, like the other sheets), `DecisionSkill` gets nothing. Mitigation: a
  test verifies decisions parse from the fixture; regenerate the fixture tab if needed (we own it).
- **Scoring behaviour shifts once `Decision` weights + the missed-milestone fix land.** Existing
  `HealthScoringService` / `ScorePortfolio` tests may change expected buckets. This is intended (the fix
  makes ORB-1002 correctly worse), but every changed assertion must be re-derived deliberately, not
  force-passed. The placeholder nature is preserved (`IsPlaceholder` true).
- **Scope creep risk.** This is a large change spanning domain + 4 agents + a new agent + config + roll-up +
  view. Mitigation: `tasks.md` sequences it as independently-landable slices (bug fixes first, then metric
  field, then Decisions, then roll-up/view), each kept green, so it can be paused between slices.
