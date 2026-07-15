> Implement test-first (red → green → refactor). Keep the suite green before checking off a task.
> Terminology: "RAG" = Red/Amber/Green health colour throughout, never retrieval-augmented generation.
> Scope: backend enumeration + roll-up (real logic, TDD) **plus** an L1 view built to the v2 wireframe
> (`docs/designs/phase5-wireframe-v2.html`, `data-page="l1"`). The roll-up reuses the existing pure
> `HealthScoringService` — no new scoring logic, no re-analysis, no LLM. No `Project` entity, no migration.

## 1. Repository: distinct project-key enumeration (TDD)

- [x] 1.1 (red) `FindingRepositoryDistinctKeysTests` — three distinct keys (ALPHA with several findings)
      → returns each once; empty store → empty. (Red confirmed via the auto-test gate: method missing.)
- [x] 1.2 Added `DistinctProjectKeysAsync` to `IFindingRepository`; implemented in `EfFindingRepository`
      (`Findings.Select(f => f.ProjectKey).Distinct()`). No schema change / no migration. Updated the three
      existing test doubles (`InMemoryFindings`, two `StubRepo`) to implement the new port member.
- [x] 1.3 (green) Enumeration tests pass; full suite green (100 Application + 116 Api = 216).

## 2. ScorePortfolio slice: aggregation semantics (TDD)

> Pin the semantics from design Decision 3. Use a fake/in-memory `IFindingRepository` (or the real one)
> feeding known findings so `HealthScoringService` produces deterministic buckets.

- [x] 2.1 (red) RAG counts: 2 Red / 1 Amber / 3 Green → red=2, amber=1, green=3.
- [x] 2.2 (red) Needs-PM-Review count independent of colour (Green+Low-conf and Amber+Low-conf → count 2).
- [x] 2.3 (red) Aggregate confidence = mean of scored `Confidence` (mean of {100,30}=65); empty → 0.
- [x] 2.4 (red) Enumerated-but-unscoreable project (Narrative only, null Score) excluded from all counts.
- [x] 2.5 Implemented `ScorePortfolio` (`Application/Features/ExecutivePortfolio/`) — enumerate keys, load
      each project's findings, score via the existing `HealthScoringService.Score`, aggregate into
      counts / needs-review / mean confidence / intervention list.
- [x] 2.6 (green) Aggregation tests pass.

## 3. Intervention list: ranking + cited reason (TDD)

- [x] 3.1 (red) Ordering: Red before Amber (`OrderByDescending(FinalBucket).ThenBy(RawScore)`); Green
      excluded from the list.
- [x] 3.2 (red) Cited reason — override path: a Red-via-override project's entry names the override
      (`forecast-overrun-critical`) and carries that override's citation locator.
- [x] 3.3 (red) Cited reason — raw-score path: an Amber-Resource project (no override) still names the
      worst area ("Resource") and cites the worst Analysis finding in it (non-empty locator).
- [x] 3.4 Implemented the reason/citation derivation (prefer worst-floor override, else worst area + its
      worst finding's citation) and the ordering in the handler.
- [x] 3.5 (green) Intervention-list tests pass (8/8 in `ScorePortfolioTests`).

## 4. Endpoint: GET /api/portfolio (TDD)

- [x] 4.1 (red) `ExecutivePortfolioEndpointsTests` — upload→analyze → `GET /api/portfolio` 200 with counts
      summing to the analyzed project count; every intervention entry carries a non-empty reason +
      citation locator and a Red/Amber status.
- [x] 4.2 (red) Empty store → 200 zeroed (red/amber/green/needs-review = 0, empty intervention list);
      unauthenticated → 401. (Red confirmed via the gate: 404 before the endpoint existed.)
- [x] 4.3 Added `ExecutivePortfolioEndpoints` (`GET /api/portfolio`, `RequireAuthorization`); registered in
      `Program.cs`. Enums surface as strings via the `ScorePortfolio.Result` shape.
- [x] 4.4 (green) Endpoint tests pass (3/3); full backend suite green (108 Application + 119 Api = 227).

## 5. L1 executive view built to v2 (no JS harness — see Change 1 §1.3)

> Repo has no JS test runner; render mapping lives in a small pure module (verified by driving the app),
> the component is built directly to the v2 layout, and correctness is anchored by the §4 integration test.

- [x] 5.1 Read v2 `data-page="l1"` in full — panels catalogued: BACKED = summary-strip health (total +
      RAG bar + legend), confidence (avg) + Needs-PM-Review, intervention `records` table; UNBACKED =
      financial-exposure €, decisions-blocking, "Where the pressure is" (Money/Decisions/key-person),
      recommended-actions (owned/dated).
- [x] 5.2 Added `ExecutivePortfolio.jsx` + protected route `/portfolio` (`AppRoutes.jsx`) + `NavMenu`
      "Portfolio" link; fetches `GET /api/portfolio` via `authFetch`.
- [x] 5.3 Backed panels render live: portfolio-health total + segmented RAG bar + legend, aggregate
      confidence with the Needs-PM-Review count, and the worst-first intervention table (project, reason +
      cited locator, confidence, status `sev` chip).
- [x] 5.4 Unbacked panels (€ exposure, decisions-blocking, key-person risk, owned/dated recommendations)
      render dashed "not yet captured — follow-on" placeholders matching the v2 slots; no fabricated data.
- [x] 5.5 Extracted the reusable v2 pieces (eyebrow, summary-strip/cells, RAG bar + legend, sec-head,
      `records` table, `sev` chips, flagged panels) into a shared SCSS section reusing Change 1's
      `--rag-*` custom properties — ready for the L2 retrofit.

## 6. Verify + document

- [x] 6.1 Verified: the `ExecutivePortfolioEndpointsTests` integration test drives upload→analyze→
      `GET /api/portfolio` (counts + cited intervention entries + zeroed-empty 200) over real HTTP;
      `ScorePortfolioTests` pins the ordering/aggregation; the reused `bucketColour` helper node-checked for
      Red/Amber/Green→classes; Vite build compiles the L1 view + shared SCSS. **Open item:** a live browser
      visual pass needs the full stack (Postgres via Docker), not running headlessly here — offered to the
      user (same as Change 1).
- [x] 6.2 Diff scoped: one additive port method (`DistinctProjectKeysAsync`) + its EF impl + 3 test-double
      updates, the new `ExecutivePortfolio` slice + endpoint + `Program.cs` registration, and client
      view/route/nav/styles. No change to existing API contracts, finding shape, agents, prompts, or DB
      schema.
- [x] 6.3 Roadmap Phase 5 Level-1 flipped ⬜ → ✅ (L3 noted to reuse the enumeration; L2 retrofit noted as
      next follow-on); "Dashboards (Phase 5, Level 1)" note added to `CLAUDE.md`.
- [x] 6.4 Full suite green (108 Application + 119 Api = 227); `openspec validate --strict` passes.
