## Why

The plan doc lists **Decisions** as both a health-scoring area (10% in its EXAMPLE table) and a Level-2
panel ("decisions needed: owner / deadline / consequence"), and `decisions.csv` already carries every
field the formula needs — `status`, `needed_by`, `owner`, `consequence_if_delayed`. But **nothing reads
it**: `ExcelProjectParser` doesn't parse a Decisions sheet, the LLM minutes extraction pulls only
Risks/Issues, there is no Decision agent, and `HealthArea` has no `Decision` bucket. The concrete cost:
the fixture's **D-1002-1 OVERDUE decision is silently missed** by every analysis run today.

This change (shared tickets #45 + #47, parent #8) closes that: it adds the `Decision` health area (#45)
and the parse + deterministic agent + override (#47), following the exact RAID → Risk pattern already in
the pipeline. It lights up **both** the L1 decision-backlog panel and the L2 decisions-needed panel (their
roll-up/presentation are their own tickets — this change produces the findings + scoring).

## What Changes

- **`HealthArea.Decision`** (#45) — add the enum member (persisted as string, like the others) so Decision
  findings can be scored. Add a `Decision` weight to the `HealthScoring` config **and rebalance** the
  existing weights so they still sum to `WeightTotal` (validation enforces `sum == WeightTotal`, so adding
  a weight naively fails at startup). All numbers stay `EXAMPLE` placeholders (`IsPlaceholder: true`).
- **`DecisionRecord` + parsing** (#47) — a new typed record (`ProjectKey`, `Title`, `Status`, `Owner`,
  `NeededBy`, `Consequence`) parsed from a `Decisions` sheet by `ExcelProjectParser`, threaded into
  `CollectedData.Decisions`.
- **`DecisionSkill`** (#47) — a deterministic agent (no LLM) that emits an `Area == Decision` finding per
  decision that is **overdue** (`NeededBy` passed and `Status` not `Approved`) or **due soon** (`NeededBy`
  within the near window), each cited to its decision row, severity by band (overdue → Red, due-soon →
  Amber). Registered in DI and run in the orchestrator's parallel analysis stage (#3–#6).
- **Key-decision override** (#47) — a config override `{ Area: Decision, WhenSeverityAtLeast: Red, Floor:
  Amber }` implementing the plan doc's *"key decision overdue → minimum Amber"*, using the existing generic
  override engine (no engine change).

Not in scope (consumers, separate tickets): the L1 decision-backlog **count** in the portfolio roll-up
(`add-l1-portfolio-signals` slice E) and any L2 view restyle — this change produces the Decision findings
and their scoring; the findings already flow to the existing `GET /api/projects/{key}` read. Also out of
scope: `Scope` area (blocked on a client Scope-RAG rule), a "blocking" flag (not in the data — overdue is
the trigger).

## Capabilities

### Modified Capabilities

- `analysis-pipeline`: a new deterministic **Decision agent** joins the parallel analysis stage, reading a
  newly-parsed `DecisionRecord` and emitting cited `Area == Decision` findings (overdue / due-soon).
- `health-scoring`: a new `Decision` health **area** (weight, placeholder, rebalanced) and a
  **key-decision-overdue** override (worst-case floor to Amber); the engine is unchanged.

## Impact

- **Domain:** `HealthArea` gains `Decision`. No `Finding` change (metric field already landed in #46).
- **Application:** new `DecisionRecord` (`TypedRecords`) + `CollectedData.Decisions`; new `DecisionSkill`;
  `AnalysisOrchestrator` constructor + parallel stage gain it; `DependencyInjection` registers it.
- **Infrastructure:** `ExcelProjectParser` reads a `Decisions` sheet. Enum persists as string — **no
  migration** for the enum. (The `area` column already stores the area string.)
- **Config:** `appsettings.json` `HealthScoring` — add the `Decision` weight (rebalanced) + the
  `key-decision-overdue` override. `IsPlaceholder` stays true.
- **Fixture:** confirm the consolidated `orbit-sample.xlsx` carries a `Decisions` tab whose headers match
  the reader; regenerate the tab if not (we own the fixtures). The `DecisionSkill` unit tests build their
  own records, so agent logic is proven regardless of the fixture.
- **Tests (TDD):** parser test (Decisions sheet → records); `DecisionSkill` tests (overdue → Red cited;
  approved → nothing; due-soon → Amber); `HealthScoring` tests (Decision area scored; key-decision override
  floors to Amber); re-derive any shifted `HealthScoringService`/portfolio expectations deliberately. Full
  suite stays green (baseline 132 Application + 124 Api = 256).
- **Docs:** tick #45 + #47; update `docs/l1-`/`l2-` follow-up registers (Decisions items → done, backlog
  roll-up still pending slice E); note the corrected weights in `docs/dashboard-output-formats.md`.
