## Context

Phase 4 shipped `HealthScoringService.Score(projectKey, findings)` — pure, deterministic, resolves the
latest run per key and returns a `HealthScore` (`RawScore`, `RawBucket`, `FinalBucket`, `NeedsPmReview`,
`Confidence`, `Areas[]`, `AppliedOverrides[]`). Change 1 wired that per-project score into the L2 view.
Both existing read slices (`ScoreProject`, `GetProjectFindings`) take a **known** `projectKey`; the
repository (`IFindingRepository`) exposes only `GetByProjectKeyAsync` / `GetByUploadIdAsync`. There is **no
enumeration** of projects and **no `Project` entity** — findings group by an opaque `project_key` string
column (already persisted and indexed).

This change adds the Level-1 portfolio roll-up. The scoring is solved; the missing pieces are **discovery**
(enumerate the keys) and **aggregation** (fan out the existing scorer and roll up). The L1 view is built to
`docs/designs/phase5-wireframe-v2.html` (`data-page="l1"`), whose panels split cleanly into
roll-up-backed and not-yet-backed (see proposal table).

## Goals / Non-Goals

**Goals:**

- Enumerate projects from the findings store (`DistinctProjectKeysAsync`) with no new entity/schema.
- Roll up per-project scores (reusing the pure `HealthScoringService`) into G/A/R counts, aggregate
  confidence, a "Needs PM Review" count, and a worst-first intervention list with **cited reasons**.
- `GET /api/portfolio` (authorized, shared-workspace, view-only); empty store → zeroed 200.
- L1 React view to the v2 layout, extracting a **shared stylesheet** the L2 view can later adopt.
- Hold the presentation-only boundary: populate backed panels, flag unbacked ones, fabricate nothing.

**Non-Goals:**

- A first-class `Project` entity; any finding-shape enrichment (€ exposure, per-decision, key-person,
  owned recommendations); L3 Data Quality; retrofitting L2 onto the shared stylesheet (all follow-ons).
- Re-running analysis or the LLM — the roll-up is a pure query over persisted findings.
- Pagination / filtering / search on the portfolio (POC scale is small; not required by the PRD).

## Decisions

**1. Discovery = `SELECT DISTINCT project_key`, not a `Project` entity.** Add
`IFindingRepository.DistinctProjectKeysAsync`; the EF impl is
`Set<Finding>().Select(f => f.ProjectKey).Distinct().ToListAsync()`. Rationale: matches the opaque-key model
the whole codebase already uses, needs no schema/migration, and keeps a `Project` entity out of POC scope
(the roadmap lists it as out-of-scope). *Alternative:* introduce `Project` now — rejected as scope the
roadmap defers and the roll-up doesn't need.

**2. The roll-up is a slice that fans out over the existing scorer — no scoring logic is duplicated.**
`ScorePortfolio.Handler` enumerates keys, and for each loads its findings (`GetByProjectKeyAsync`) and calls
`HealthScoringService.Score(key, findings)` — the *same* call `ScoreProject` makes. It then aggregates.
Rationale: single source of truth for scoring; the portfolio can never disagree with the per-project view.

**3. Aggregation semantics (pinned so tests are deterministic):**
- **RAG counts** = count of scored projects by `FinalBucket`. A project whose `Score` is null (enumerated
  but unscoreable) is **excluded** from all counts — it dilutes nothing.
- **Needs-PM-Review count** = number of scored projects with `NeedsPmReview == true`, independent of colour.
- **Aggregate confidence** = arithmetic mean of scored projects' `Confidence` (matches v2's "Confidence
  (avg)"); empty portfolio → 0.
- **Intervention list** = projects with `FinalBucket ∈ {Red, Amber}`, ordered **Red before Amber**, then
  **`RawScore` ascending** (worst score first) as a deterministic tiebreak. Green excluded.

**4. Reason + citation derivation for each intervention entry.** The handler already holds each project's
`HealthScore` *and* its findings, so it derives:
- **If an applied override set the floor** (`AppliedOverrides` non-empty): reason = the worst-floor
  override's `Reason`; citation = that override's `CitationLocator` (already carries `FindingId` +
  locator). This is the cleanest, most specific reason and is preferred.
- **Else (raw score drove the bucket):** reason = the **worst-severity area** (`Areas` reduced to the
  highest severity, weight as tiebreak); citation = the locator of the worst `Analysis` finding in that
  area for the project (looked up from the findings already loaded). This guarantees every entry is cited
  even when no override fired — consistent with the "every finding keeps its citation" invariant.

**5. L1 view: build to v2, extract a shared stylesheet.** The v2 pieces (KPI stat cards, RAG donut,
severity chips, intervention table, section layout) go into a shared SCSS partial/section so the L2 view
(Change 1) can be retrofitted onto the identical system in the follow-on. RAG colours reuse the custom
properties Change 1 already defined in `styles.scss`. A small pure helper module mirrors Change 1's
`health.js` for any response→viewmodel mapping. *Alternative:* style L1 in isolation — rejected; it would
re-introduce the look-mismatch the shared-stylesheet decision (exploration) exists to avoid.

**6. Unbacked panels render a flagged follow-on state.** Financial exposure (€), per-decision
days-open/owner, key-person risk (alloc × absence), and owned/dated recommendations exceed the finding
shape. They render a labelled "not yet captured — follow-on" placeholder in the v2 slot, never invented
numbers. This is the same boundary Change 1 established; it keeps the demo honest about what is real.

## Risks / Trade-offs

- **[N-project scoring = N repository loads (`GetByProjectKeyAsync` per key)]** → Acceptable at POC scale
  (a few dozen projects). If it becomes hot, add a single bulk `GetAllAsync` / group-in-memory later; the
  slice boundary makes that swap local. `log()`-able if project count grows large. Not optimised now.
- **[Unscoreable projects could silently vanish]** → They are *intentionally* excluded from RAG counts, but
  the behaviour is pinned by a test (`Unscoreable project does not distort counts`) so the exclusion is
  deliberate, not a bug.
- **[Reason/citation for the non-override path depends on finding lookup]** → Covered by a test asserting a
  raw-score-driven Red still yields a cited reason; if no Analysis finding is found in the worst area
  (shouldn't happen — the area came from a finding), fall back to the area name with the project's own
  latest citation rather than an empty locator.
- **[v2 has many unbacked panels — risk of a hollow-looking dashboard]** → The backed panels (health
  counts, confidence, intervention list) are the executive core (US #3/#6) and are fully live; the flagged
  panels communicate roadmap intent. Reviewers confirm no fabricated data slipped in.
- **[Presentation-only boundary drift]** → Spec pins it; diff must show no agent/prompt/finding-shape/API
  contract change beyond the one additive repository method + new read slice/endpoint.

## Migration Plan

No schema change, no migration (`DistinctProjectKeys` queries an existing indexed column). Backend adds one
repository method, one Application slice, one endpoint (registered in `Program.cs`). Client adds one view +
route + shared styles. Rollback = revert the commit; existing endpoints and the per-project score are
untouched. Roadmap Phase 5 Level-1 flips ⬜ → ✅ on merge.

## Open Questions

- Within a bucket, is `RawScore` ascending the intended tiebreak, or should lowest confidence rank first
  (most-uncertain-to-the-top)? (Leaning `RawScore` ascending; revisit at `/verify` against v2's ordering.)
- Does the shared stylesheet land as a new SCSS partial or a section appended to `styles.scss`? (Cosmetic;
  decide during implementation to minimise churn.)
- L1 route name: `/portfolio` vs `/` (make the portfolio the landing page for execs)? (Default `/portfolio`;
  navigation/landing decision can follow.)
