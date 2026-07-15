> Implement test-first (red → green → refactor). Keep the suite green before checking off a task.
> Terminology: "RAG" = Red/Amber/Green health colour throughout, never retrieval-augmented generation.
> Scope: backend enumeration + roll-up (real logic, TDD) **plus** an L1 view built to the v2 wireframe
> (`docs/designs/phase5-wireframe-v2.html`, `data-page="l1"`). The roll-up reuses the existing pure
> `HealthScoringService` — no new scoring logic, no re-analysis, no LLM. No `Project` entity, no migration.

## 1. Repository: distinct project-key enumeration (TDD)

- [ ] 1.1 (red) Repository test (in `Api.Tests`, the EF/in-memory harness the other repo tests use): after
      persisting findings for three distinct keys (one key with several findings), `DistinctProjectKeysAsync`
      returns exactly those three keys, each once; an empty store returns an empty set.
- [ ] 1.2 Add `DistinctProjectKeysAsync(CancellationToken)` to `IFindingRepository`
      (`Application/Abstractions`) and implement it in `EfFindingRepository`
      (`Set<Finding>().Select(f => f.ProjectKey).Distinct()`). No schema change / no migration.
- [ ] 1.3 (green) Enumeration tests pass; existing repository tests still pass.

## 2. ScorePortfolio slice: aggregation semantics (TDD)

> Pin the semantics from design Decision 3. Use a fake/in-memory `IFindingRepository` (or the real one)
> feeding known findings so `HealthScoringService` produces deterministic buckets.

- [ ] 2.1 (red) RAG counts: given projects scoring 2 Red / 1 Amber / 3 Green, the roll-up reports
      red=2, amber=1, green=3.
- [ ] 2.2 (red) Needs-PM-Review count is independent of colour (two flagged projects → count 2 regardless
      of their buckets).
- [ ] 2.3 (red) Aggregate confidence = mean of scored projects' `Confidence`; empty portfolio → 0.
- [ ] 2.4 (red) An enumerated-but-unscoreable project (null `Score`) is excluded from all RAG counts and
      contributes to no bucket.
- [ ] 2.5 Implement `ScorePortfolio` (`Application/Features/ExecutivePortfolio/`) — enumerate keys via
      `DistinctProjectKeysAsync`, load each project's findings (`GetByProjectKeyAsync`), score via the
      existing `HealthScoringService.Score`, aggregate. Return records for counts / confidence / review
      count / intervention list.
- [ ] 2.6 (green) Aggregation tests pass.

## 3. Intervention list: ranking + cited reason (TDD)

- [ ] 3.1 (red) Ordering: Red entries rank before Amber; within a bucket, `RawScore` ascending
      (worst first). Green projects are excluded from the list entirely.
- [ ] 3.2 (red) Cited reason — override path: a Red project whose bucket was set by an applied override
      yields an entry whose reason names the driving override and carries that override's citation locator.
- [ ] 3.3 (red) Cited reason — raw-score path: a Red project with no override yields an entry whose reason
      names the worst-severity area and carries the locator of the worst `Analysis` finding in that area
      (never an empty locator).
- [ ] 3.4 Implement the reason/citation derivation (design Decision 4: prefer worst-floor override, else
      worst area + its worst finding's citation) and the ordering in the `ScorePortfolio` handler.
- [ ] 3.5 (green) Intervention-list tests pass.

## 4. Endpoint: GET /api/portfolio (TDD)

- [ ] 4.1 (red) Integration test (`TestWebAppFactory`, `pmo-user` client): upload→analyze **multiple**
      project keys, then `GET /api/portfolio` returns 200 with the expected RAG counts, a worst-first
      intervention list, and each intervention entry carrying a non-empty reason + citation locator.
- [ ] 4.2 (red) Empty store: `GET /api/portfolio` returns 200 with red=0/amber=0/green=0 and an empty
      intervention list (not 404). Unauthenticated caller → 401.
- [ ] 4.3 Add `ExecutivePortfolioEndpoints` (`MapExecutivePortfolioEndpoints`, `RequireAuthorization`,
      `GET /api/portfolio`) and register it in `Program.cs`. Surface enums as strings (matching the other
      read surfaces).
- [ ] 4.4 (green) Endpoint tests pass; full backend suite green.

## 5. L1 executive view built to v2 (no JS harness — see Change 1 §1.3)

> Repo has no JS test runner; render mapping lives in a small pure module (verified by driving the app),
> the component is built directly to the v2 layout, and correctness is anchored by the §4 integration test.

- [ ] 5.1 Read `docs/designs/phase5-wireframe-v2.html` `data-page="l1"` in full; list each panel and mark
      it backed (health counts, confidence + review count, intervention table) vs. unbacked (€ exposure,
      per-decision detail, key-person risk, owned recommendations).
- [ ] 5.2 Add `ExecutivePortfolio.jsx` + a protected route (`/portfolio`) in `AppRoutes.jsx` and a
      `NavMenu` link; fetch `GET /api/portfolio` via `authFetch`.
- [ ] 5.3 Render the **backed** panels from live data: portfolio-health G/A/R counts (+ RAG donut),
      aggregate confidence with the "Needs PM Review" count, and the worst-first intervention table
      (project, status, confidence, reason, cited locator).
- [ ] 5.4 Render the **unbacked** panels (€ financial exposure, decisions-blocking detail, key-person risk,
      owned/dated recommended actions) as a clear "not yet captured — follow-on" state — match the v2 slot,
      fabricate nothing.
- [ ] 5.5 Extract the reusable v2 pieces (stat cards, RAG donut, chips, intervention table, layout) into a
      **shared** SCSS section/partial reusing Change 1's RAG custom properties, so L2 can adopt it later.

## 6. Verify + document

- [ ] 6.1 `/verify` (or drive the app): analyze a few project keys, open `/portfolio`, confirm the G/A/R
      counts, confidence, review count, and intervention ordering match the data; confirm unbacked panels
      show the follow-on state; confirm the empty-store zeroed 200. Node-check any pure helper across its
      branches (as in Change 1).
- [ ] 6.2 Confirm the diff is scoped: one additive repository method, the new `ExecutivePortfolio` slice +
      endpoint, and client view/styles — no change to existing API contracts, finding shape, agents,
      prompts, or DB schema.
- [ ] 6.3 Flip roadmap Phase 5 Level-1 ⬜ → ✅; add a `## Dashboards (L1)` note to `CLAUDE.md`; note the L2
      shared-stylesheet retrofit as the next follow-on.
- [ ] 6.4 Full suite green; `openspec validate --strict add-executive-portfolio-dashboard` passes.
