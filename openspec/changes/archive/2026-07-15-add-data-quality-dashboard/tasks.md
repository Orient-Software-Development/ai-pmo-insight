> Implement test-first (red → green → refactor). Keep the suite green before checking off a task.
> Terminology: "RAG" = Red/Amber/Green health colour throughout, never retrieval-augmented generation.
> Scope: a backend roll-up over the latest-run `DataQuality`-area findings (real logic, TDD) **plus** an L3
> view built to the v2 wireframe (`docs/designs/phase5-wireframe-v2.html`, `data-page="l3"`). The roll-up
> reuses the existing `DistinctProjectKeysAsync` (enumeration) and `HealthScoringService` (confidence) — no
> new repository method, no new scoring logic, no re-analysis, no LLM, no migration.

## 1. SummarizeDataQuality slice: collection + latest-run filtering (TDD)

> Pin the semantics from design Decisions 1–4. Feed a fake/in-memory `IFindingRepository` known findings so
> `HealthScoringService` produces deterministic confidence, and so latest-run resolution is exercised.

- [x] 1.1 (red) Latest-run only: a project with an older run and a newer run → only the newer run's
      `DataQuality` findings are collected.
- [x] 1.2 (red) Area filter: a latest run mixing `DataQuality` and Budget/Schedule findings → only the
      `DataQuality` findings are collected (non-analysis kinds excluded too).
- [x] 1.3 Implemented `SummarizeDataQuality` (`Application/Features/DataQuality/`) — enumerate keys via
      `DistinctProjectKeysAsync`, load each project's findings (`GetByProjectKeyAsync`), resolve the latest
      run, keep `Area == DataQuality`.
- [x] 1.4 (green) Collection/filter tests pass; full suite green.

## 2. Confidence block: mean + configured threshold + below-target (TDD)

- [x] 2.1 (red) Mean confidence = mean of scored projects' `HealthScore.Confidence` (reuse
      `HealthScoringService`); an unscoreable project contributes nothing; empty portfolio → mean 0.
- [x] 2.2 (red) Threshold is read from `HealthScoringOptions.ConfidenceFloor` (injected, not hard-coded);
      `belowTarget = mean < ConfidenceFloor` — true below, false at-or-above.
- [x] 2.3 Implemented the confidence block in the handler (inject `HealthScoringOptions`; reuse the scorer's
      confidence per Decision 2).
- [x] 2.4 (green) Confidence-block tests pass.

## 3. Items list: worst-first, cited, with counts (TDD)

- [x] 3.1 (red) One entry per `DataQuality` finding: `{ projectKey, issue (Summary), severity,
      citationLocator }`; no entry has an empty locator.
- [x] 3.2 (red) Ordering worst-first by severity (Red before Amber before Green), `projectKey`+locator as
      the deterministic tiebreak.
- [x] 3.3 (red) Counts: `totalItems` = list length; `perProject` = count grouped by `projectKey`
      (five items across two projects → total 5, two per-project entries).
- [x] 3.4 Implemented the items projection + ordering + counts in the handler (Decision 4).
- [x] 3.5 (green) Items-list tests pass.

## 4. Endpoint: GET /api/data-quality/summary (TDD)

- [x] 4.1 (red) `DataQualityEndpointsTests` — upload→analyze (a workbook with a deterministic
      milestone-no-due-date DQ gap via new `OrbitFixtureBuilder.WorkbookWithDataQualityGap`) →
      `GET /api/data-quality/summary` 200 with the confidence block (mean + threshold=50 + consistent
      below-target), a non-empty worst-first **cited** items list, and consistent counts. (Severity
      *ordering* across Red/Amber/Green is pinned by the §3 unit tests; the shared `Workbook` fixture
      yields no DQ findings, so a dedicated gap fixture is used here.)
- [x] 4.2 (red) Empty store → 200 zeroed (mean 0, empty items, total 0); unauthenticated → 401. (Red
      confirmed via the gate: 404 before the endpoint existed.)
- [x] 4.3 Added `DataQualityEndpoints` (`GET /api/data-quality/summary`, `RequireAuthorization`, shared
      workspace); registered in `Program.cs`. Enums surface as strings via the result shape.
- [x] 4.4 (green) Endpoint tests pass (3/3); full backend suite green (117 Application + 122 Api = 239).

## 5. L3 data-quality view built to v2 (no JS harness — see L1 §5)

> Repo has no JS test runner; any render mapping lives in a small pure module (verified by driving the app),
> the component is built directly to the v2 layout on the shared Phase 5 design system, and correctness is
> anchored by the §4 integration test.

- [x] 5.1 Read v2 `data-page="l3"` in full — catalogue panels: BACKED = confidence hero (mean %, threshold,
      below-target banner), missing/inconsistent items `records` table; UNBACKED = per-item age column,
      suggested-remediation column, lift ordering, eight-category areas-completeness grid, duplicate-identity
      candidates table + merge/keep-separate.
- [x] 5.2 Added `DataQuality.jsx` + protected route `/data-quality` (`AppRoutes.jsx`) + `NavMenu`
      "Data quality" link; fetches `GET /api/data-quality/summary` via `authFetch`.
- [x] 5.3 Backed panels render live: the confidence hero (mean %, threshold, below-target banner) and the
      worst-first items table (project, issue, `sev` severity chip, cited locator) on the shared `--rag-*`
      design system (new compact `.conf-hero` block reuses the existing tokens).
- [x] 5.4 Unbacked panels (age, remediation, lift ordering, areas-completeness grid, duplicate candidates)
      render dashed "not yet captured — follow-on" placeholders matching the v2 slots; **no merge /
      keep-separate control is shipped** (US-2 never-silently-merge); no fabricated data.
- [x] 5.5 Response→viewmodel mapping (safe empty shape, mean %, below-target flag; severity→chip via the
      reused `bucketColour`) in a small pure helper `dataQuality.js` mirroring L2's `health.js`.

## 6. Verify + document

- [x] 6.1 Verify: the `DataQualityEndpointsTests` integration test drives upload→analyze→
      `GET /api/data-quality/summary` (confidence block + cited worst-first items + counts + zeroed-empty
      200 + 401) over real HTTP; `SummarizeDataQualityTests` pins collection/latest-run/confidence/ordering/
      counts; the `dataQuality.js` helper is a pure normaliser; the Vite build compiles the L3 view + SCSS.
      **Open item:** a live browser visual pass needs the full stack (Postgres via Docker), not runnable
      headlessly here — offered to the user (same as L1).
- [x] 6.2 Diff scoped: the new `DataQuality` slice + endpoint + `Program.cs` registration, the client
      view/route/nav/helper + a compact `.conf-hero` SCSS block, and a new additive test fixture method. No
      new repository method, no change to existing API contracts, finding shape, agents, prompts, or DB
      schema; no merge action. (Working tree carried no other pending changes.)
- [x] 6.3 Roadmap Phase 5 Level-3 flipped ⬜ → ✅; "Dashboards (Phase 5, Level 3)" note added to `CLAUDE.md`.
      Issue #35 + epic #8 close via the PR ("Closes #35") — not edited directly here (outward-facing).
- [x] 6.4 Full suite green (117 Application + 122 Api = 239); `openspec validate --strict` passes.
