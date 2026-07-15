> Implement test-first (red → green → refactor). Keep the suite green before checking off a task.
> Terminology: "RAG" = Red/Amber/Green health colour throughout, never retrieval-augmented generation.
> Scope reminder: **presentation-only, client-side.** No backend/API/finding-shape changes. Both endpoints
> (`GET /api/projects/{projectKey}` and `GET /api/projects/{projectKey}/health`) already return the shape
> this view consumes.

## 1. Confirm the consumed contracts (no code)

- [x] 1.1 Re-read `ScoreProject.Result`/`ScoreView` and `GetProjectFindings.Result` to pin the exact field
      names the view binds to (`FinalBucket`, `RawScore`, `Areas[].{Area,Severity,Weight,Contribution}`,
      `Confidence`, `NeedsPmReview`, `AppliedOverrides[].{RuleId,Floor,Reason,FindingId,CitationLocator}`).
      Confirmed against `ScoreProject.cs` / `GetProjectFindings.cs` — shapes match the specs.
- [x] 1.2 Confirm the health endpoint's three documented responses (200 scored, 200 null-score, 404) — no
      contract change. Confirmed in `HealthScoringEndpoints.cs` (`result is null ? NotFound : Ok`) and
      `ScoreProject.Handler` (null findings → null result → 404; findings-but-unscoreable → 200 null Score).
- [x] 1.3 Harness check: **`ClientApp` has NO JS/JSX test harness** (no vitest, no `@testing-library`, no
      `test` script in `package.json`). **Decision (user-confirmed):** do not introduce a JS toolchain in
      this presentation-only POC change. TDD is anchored on the **backend integration test (§2)** + manual
      `/verify` (§7). The client helpers (§3) and `HealthBanner` (§4) are implemented directly to the
      documented spec behaviour and verified by driving the running app across the three states.

## 2. Backend integration test asserting the L2 data is consumable (TDD)

- [x] 2.1 Added `ProjectStatusDashboardDataTests` (via `TestWebAppFactory`, `pmo-user` client) asserting a
      single upload→analyze makes BOTH surfaces populated for `ALPHA`: `/api/projects/ALPHA` returns the
      four sections AND `/api/projects/ALPHA/health` returns 200 with a non-null score (FinalBucket +
      areas + confidence + overrides present).
- [x] 2.2 Asserted the not-found path: an unknown key returns 404 on `/health` and 200 with empty sections
      on the findings endpoint. (Scoring-pending 200-null-score is a defined state but not reachable via a
      normal analyze — the fake pipeline always emits scoreable Analysis findings; it is covered at the
      unit level by the `add-health-scoring` service tests and by the view's render logic + §7 verify.)
- [x] 2.3 (green) Both integration tests pass against the real endpoints — no backend change required; the
      contract the view depends on is locked.

## 3. RAG colour + response-state mapping helpers (no JS harness — see §1.3)

> Pure, side-effect-free helpers kept in their own module so they read as testable units even though the
> repo has no JS runner; their behaviour is exercised end-to-end by the §2 integration test data + §7 verify.

- [x] 3.1 Implement a pure `bucketColour(finalBucket)` mapping: `Red`/`Amber`/`Green` → the intended colour
      class; unknown/missing → a neutral fallback. (Was "red unit test" — no JS harness, implemented directly.)
- [x] 3.2 Implement a pure `healthState(status, body)` reducer mapping {200+score, 200+null, 404, error} →
      one of `SCORED | SCORING_PENDING | NOT_SCORED | ERROR` (the total mapping from design Decision 3).
- [x] 3.3 Keep both helpers in a small pure module under `ClientApp/src` (`health.js`), independently
      importable so a JS harness could later unit-test them without refactoring.

## 4. HealthBanner component (no JS harness — see §1.3)

> Implemented directly to the spec's scenarios; verified by driving the running app (§7) across states.

- [x] 4.1 `SCORED`: banner renders the `FinalBucket` label + colour and the (formatted) `RawScore`; renders
      `FinalBucket` even when it differs from `RawBucket`.
- [x] 4.2 Audit surfacing: renders each area row (area, severity, weight, contribution), the aggregate
      confidence, and each applied override (rule, floor, reason, cited locator); no override entries when
      the list is empty.
- [x] 4.3 `NeedsPmReview` true renders a distinct indicator alongside any colour (incl. Green); false renders
      none. Status is never colour-only (label/score always present).
- [x] 4.4 `SCORING_PENDING` and `NOT_SCORED` each render their defined copy ("findings exist but nothing
      scoreable yet" / "no such project — no findings on record"), not an error, not a coloured banner.
- [x] 4.5 Implement `HealthBanner.jsx` consuming the §3 helpers, covering all the states above.

## 5. Wire the banner into the project view

- [ ] 5.1 In `ProjectFindings.jsx`, add a health fetch for the current project key via `authFetch`,
      resolving to a health-state value; run it concurrently with the findings load (`Promise.all` /
      parallel effects) so it adds no perceived latency.
- [ ] 5.2 Render `HealthBanner` above the existing Narrative/Findings/Challenge/Review sections; keep the
      four sections rendering independently of the health result (a health failure/404 must not blank the
      findings, and vice-versa).
- [ ] 5.3 Ensure the health fetch re-runs whenever the loaded project key changes (upload→analyze→read and
      manual "Load" both refresh the banner for the resulting key).
- [ ] 5.4 Presentation-only note: where the PRD Level-2 wishlist exceeds the finding shape (dated
      milestones, per-decision owner/deadline, explicit recommendation), render what exists (e.g. Schedule
      area, decision-area findings, Narrative) and add a small "not yet captured" note — fabricate nothing.

## 6. Styling + accessibility

- [ ] 6.1 Add RAG banner + severity-chip styles to `styles.scss`, theme-aware (light/dark) following the
      existing Pico theme-variable pattern; no inline hex.
- [ ] 6.2 Pair every RAG colour with its text label and the numeric score so status is never conveyed by
      colour alone (colour-blind safe).

## 7. Verify + document

- [ ] 7.1 Run `/verify` (or manually drive the app): upload→analyze a fixture and confirm the banner renders
      SCORED with the correct colour/score, the area breakdown, confidence, override trail, and PM-review
      flag; then confirm the SCORING_PENDING and NOT_SCORED states render as specified.
- [ ] 7.2 Confirm no backend/API/finding-shape change crept in (diff touches only `ClientApp` + docs).
- [ ] 7.3 Flip roadmap Phase 5 Level-2 from 🟡 to ✅ (rich view); add a short "dashboard read surface" note
      to `CLAUDE.md`.
- [ ] 7.4 Full suite green (backend + any client tests); `openspec validate --strict` passes for this change.
