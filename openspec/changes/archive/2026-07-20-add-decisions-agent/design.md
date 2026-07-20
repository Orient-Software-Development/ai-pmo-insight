## Context

The pipeline runs `#2 Data Quality → parallel(#3 Status, #4 Risk, #5 Financial, #6 Resource) → #7–#9`.
Each deterministic agent reads its records off `ProjectSlice.Data` (the full `CollectedData`), filters to
the project, emits cited `Area`+`Severity` findings via `FindingFactory.Analysis`. `HealthScoringService`
groups findings by `HealthArea`, reduces each area to its worst severity, weight-normalises, buckets, then
applies config-driven `{ Area, WhenSeverityAtLeast, Floor }` overrides as a worst-case floor. Weights +
overrides live in `appsettings.json` `HealthScoring` (`IsPlaceholder: true`), validated to `sum ==
WeightTotal` at startup.

The RAID → Risk agent (`RiskAndIssueSkill`) is the exact precedent for a new deterministic record-driven
agent. `decisions.csv` columns: `project_id, decision_id, title, status, owner, raised_date, needed_by,
consequence_if_delayed`. Shared tickets #45 (enum + weight) + #47 (parse + agent + override).

## Goals / Non-Goals

**Goals:**

- Score Decisions: add `HealthArea.Decision`, a placeholder weight (rebalanced to keep the sum valid), and
  the key-decision-overdue override.
- Emit cited, deterministic Decision findings (overdue / due-soon) from parsed `DecisionRecord`s — recover
  the missed D-1002-1 OVERDUE signal.
- Follow the RAID pattern exactly; no new architecture; TDD; suite stays green; numbers stay EXAMPLE.

**Non-Goals:**

- The L1 decision-backlog **count** roll-up (`add-l1-portfolio-signals` slice E) and any L2 view work —
  consumers of these findings, separate tickets. Findings already reach `GET /api/projects/{key}`.
- `Scope` area (blocked on a client Scope-RAG rule); a "blocking" flag (not in the data).
- Any `Finding`-shape change (the metric field landed in #46; a decision's owner/deadline can ride
  `MetricDetail` later if the L2 view wants it — out of scope here).

## Decisions

**1. `HealthArea.Decision` — enum member, persists as string, no migration.** `area` is already a string
column; adding an enum member needs no schema change (the health-scoring change established this). Placed
after `DataQuality` to keep existing ordinals stable.

**2. Weight rebalance (EXAMPLE).** The shipped placeholder is `Schedule 20 / Budget 30 / Risk 30 /
Resource 15 / DataQuality 5 = 100`. Validation enforces `sum == WeightTotal (100)`, so I fund `Decision`
by rebalancing rather than exceeding the total. New EXAMPLE set: **Schedule 20 / Budget 25 / Risk 25 /
Resource 15 / Decision 10 / DataQuality 5 = 100** (5 each from the inflated Budget/Risk → Decision). These
are placeholders for the PMO; the rebalance is deliberately simple and documented. *Alternative:* bump
`WeightTotal` to 110 — rejected: the sum-check exists precisely to force a conscious re-normalisation, and
the scorer weight-normalises over present areas anyway.

**3. `DecisionSkill` bands.** Overdue = `NeededBy` before the run's as-of date **and** `Status` not
`Approved` → **Red** (the plan doc's "overdue" is the serious case, and Red lets the override fire).
Due-soon = `NeededBy` within the upcoming window (14 days, matching the Status agent's constant) and not
approved → **Amber** (a heads-up). `Approved` (or no `NeededBy`) → no finding. Each finding cites its
`Decisions!row` source and carries `Area = Decision`. Deterministic, no LLM. *"Blocking"* is not in the
data (only free-text `consequence`), so overdue-Red is the trigger; the override name keeps the plan-doc
wording.

**4. Key-decision override.** Config: `{ Id: key-decision-overdue, Area: Decision, WhenSeverityAtLeast:
Red, Floor: Amber }` — implements "key decision overdue → minimum Amber" via the existing generic engine,
auditable like the other overrides. No engine change.

**5. `DecisionRecord` + parsing.** `DecisionRecord { ProjectKey, Title, Status, Owner, NeededBy (date?),
Consequence, Source }` on `CollectedData.Decisions`. `ExcelProjectParser` reads a `Decisions` sheet by
header name (like the others: `ProjectKey`, `Title`, `Status`, `Owner`, `NeededBy`, `Consequence`),
`NullIfBlank`/`ParseDate` as appropriate. `CollectedData.Empty` gets `Decisions = []`.

**6. Fixture tab.** The reader looks for a `Decisions` sheet; if the consolidated `orbit-sample.xlsx`
lacks it (or its headers don't match), the integration path finds no decisions. A parser test pins the
mapping; the `DecisionSkill` unit tests build their own records so agent logic is proven regardless. If the
tab is missing we regenerate it (we own the fixtures) — flagged in tasks, not assumed.

## Risks / Trade-offs

- **Scoring shifts once Decision is scored + the override fires.** Adding a weighted area and an overdue-Red
  on ORB-1002 (D-1002-1) will change some projects' buckets/audit trails — intended (they were incomplete
  before). Every changed `HealthScoringService` / `ScoreProject` / portfolio assertion is re-derived
  deliberately, not force-passed. `IsPlaceholder` stays true.
- **Weight rebalance is a judgement call on EXAMPLE numbers.** Documented as placeholder; the PMO sets real
  values at kick-off. The point of this change is the *mechanism*, not the specific weights.
- **Fixture `Decisions` tab may be absent.** Mitigated by unit tests that don't depend on it + a parser
  test that fails loudly if the tab/headers are wrong, prompting a fixture regen.
- **Orchestrator fan-out grows by one agent.** Trivial — `DecisionSkill` joins the existing
  `Task.WhenAll(#3–#6)`; it's deterministic and cheap.
