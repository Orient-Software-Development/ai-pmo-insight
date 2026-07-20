> **TDD** — failing test first (red), minimal code (green), refactor; keep the full backend suite green
> before checking off a task. Baseline: 132 Application + 124 Api = 256. Shared tickets #45 (enum + weight)
> + #47 (parse + agent + override). Numbers stay `EXAMPLE` placeholders (`IsPlaceholder: true`). Follows
> the RAID → Risk pattern; no new architecture. The L1 decision-backlog roll-up is slice E of
> `add-l1-portfolio-signals` (out of scope here).

## 1. Baseline (no code)

- [x] 1.1 Baseline green (256). 1.2 Precedent re-read (RAID path, FindingFactory, HealthScoringOptions,
      decisions.csv columns).

## 2. Slice A — `HealthArea.Decision` + weight (#45, TDD)

- [x] 2.1 (red) Added `Decision_is_a_recognised_weighted_area` — failed (enum member + Validate rejected
      "Decision").
- [x] 2.2 (green) Added `HealthArea.Decision = 5` (persists as string). Rebalanced `appsettings.json`
      `HealthScoring.Weights` to Schedule 20 / Budget 25 / Risk 25 / Resource 15 / Decision 10 /
      DataQuality 5 = 100. `IsPlaceholder` stays true.
- [x] 2.3 Green: Application 133; Api host boots + startup validation accepts the Decision weight
      (`AuthEndpointsTests` 14 pass — proves the config validates).

## 3. Slice B — `DecisionRecord` + parsing (#47, TDD)

- [x] 3.1 (red) Added 2 parser tests (`Decisions` sheet → records; no-sheet → empty). Added a `Decisions`
      tab to `OrbitFixtureBuilder.Workbook()`. Failed as expected (Decisions empty / API absent).
- [x] 3.2 (green) Added `DecisionRecord` + `CollectedData.Decisions` (+ `Empty`); `ExcelProjectParser`
      reads a `Decisions` sheet by header (`ProjectKey`/`Title`/`Status`/`Owner`/`NeededBy`/`Consequence`).
- [ ] 3.3 **Pending /verify (§6.2):** confirm the consolidated `orbit-sample.xlsx` carries a matching
      `Decisions` tab; regenerate if not. (Unit/parser tests use `OrbitFixtureBuilder`, so agent logic is
      proven independent of the real fixture.)
- [x] 3.4 Parser tests green (7 passed). Clean Release rebuild confirmed the code is correct in both
      configs (earlier hook failures were stale incremental Release binaries).

## 4. Slice C — `DecisionSkill` (#47, TDD)

- [x] 4.1 (red) Added `DecisionAgentTests` (overdue→Red cited; due-soon→Amber; Approved→none; no
      needed-by→none; area check) — failed (no `DecisionSkill`).
- [x] 4.2 (green) Implemented deterministic `DecisionSkill` (overdue = needed-by < as-of & not Approved →
      Red; due-soon = within 14 days & not Approved → Amber) via `FindingFactory.Analysis` +
      `HealthArea.Decision`.
- [x] 4.3 (green) Registered `DecisionSkill` in DI; added to the `AnalysisOrchestrator` constructor +
      parallel `Task.WhenAll` stage; updated `AnalysisOrchestratorTests.Build` for the new arg.
- [x] 4.4 Full backend suite green: 138 Application + 126 Api = 264.

## 5. Slice D — Key-decision override (#47, TDD)

- [x] 5.1 (test) Added `Overdue_key_decision_floors_the_bucket_to_amber` — a Red Decision finding over
      Green fillers → RawBucket Green, FinalBucket Amber, `key-decision-overdue` audited. (Config-driven via
      the existing generic engine — the rule in the test fixture is the "implementation".)
- [x] 5.2 (green) Added the `key-decision-overdue` override (`{ Area: Decision, WhenSeverityAtLeast: Red,
      Floor: Amber }`) to `appsettings.json` `HealthScoring`. No engine change; host boots with it.
- [x] 5.3 Full backend suite green: 139 Application + 126 Api = 265.

## 6. Verify + document

- [x] 6.1 Full suite green (139 Application + 126 Api = 265); `openspec validate --strict` passes; build
      clean (suite builds).
- [ ] 6.2 **Pending /verify (needs running stack):** upload `orbit-sample.xlsx`, analyze; confirm ORB-1002
      carries a Decision finding for the overdue D-1002-1 and its health reflects the key-decision override.
      This also settles task 3.3 (real fixture's `Decisions` tab). Unit/parser tests already prove the
      logic on `OrbitFixtureBuilder`.
- [x] 6.3 Ticked #45 + #47; updated `docs/l1-`/`l2-` follow-up registers (Decisions produced; backlog
      roll-up still pending slice E) and the weights + Decision area in `docs/dashboard-output-formats.md`.
- [x] 6.4 Boundary held: enum + weight + parse + `DecisionRecord` + `DecisionSkill` + override + orchestrator
      wiring only. No L1 roll-up/count, no L2 view, no `Finding`-shape change (metric landed in #46).
