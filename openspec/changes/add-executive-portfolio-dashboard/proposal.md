## Why

Phase 5 Change 1 (`add-project-status-dashboard`) shipped the Level-2 view — **one** project in depth. The
PRD's headline executive deliverable (user story #3) is the opposite: the **Level-1 portfolio roll-up** —
G/A/R counts across *all* projects, the handful needing intervention now, financial exposure, and owned
recommendations, so leadership spends its next hour on the projects that actually need it. **Nothing can
produce that today.** Every read path starts from a `projectKey` you already know (`GetProjectFindings`,
`ScoreProject` both take a key), there is **no first-class `Project` entity**, and **no query enumerates or
scores the whole portfolio**. Unlike Change 1 (pure presentation over existing endpoints), Change 2 needs a
real backend slice: portfolio-wide **discovery** + **roll-up**. The scoring itself is already solved —
`HealthScoringService` is pure and re-runnable — so the roll-up is a fan-out over it, not new analysis (no
LLM cost), keeping the Phase 4 "query over the findings store" pattern.

## What Changes

- **New `executive-portfolio` capability** — a portfolio-wide read over the findings store, exposed as
  `GET /api/portfolio` (authorized, shared-workspace, view-only).
- **Portfolio discovery (the architectural gap)** — a new `IFindingRepository.DistinctProjectKeysAsync`
  returns the distinct project keys on record (`SELECT DISTINCT project_key`). This is the minimal,
  opaque-key-preserving way to enumerate projects; **no `Project` entity is introduced** (stays out of POC
  scope).
- **`ScorePortfolio` slice** — enumerates the keys, scores each via the **existing pure
  `HealthScoringService`** (latest run per project, as Phase 4 already does), and rolls the results up into:
  **G/A/R counts** (count of each `FinalBucket` across projects); **aggregate confidence** + a **count of
  projects flagged `NeedsPmReview`**; and an ordered **"projects needing intervention"** list (red/amber
  worst-first — each entry carries the `projectKey`, `FinalBucket` status, aggregate confidence, a
  worst-area **reason**, and the **citation** of the finding/override that drove it).
- **Empty store is a defined 200** — no findings on record → 200 with zeroed counts and an empty
  intervention list, never a 404.
- **New L1 executive React view + route** (`/portfolio`), built to the **v2 wireframe**
  (`docs/designs/phase5-wireframe-v2.html`, `data-page="l1"`) **from the first commit** — no
  plain-then-restyle. Its reusable pieces (KPI cards, RAG donut, severity chips, layout) are extracted into
  a **shared stylesheet** so the shipped L2 view can be retrofitted onto the same design system as a
  follow-on.
- **v2 is a visual target, not a data contract — presentation-only boundary holds (as in Change 1).** The
  v2 L1 layout is matched, but panels are populated only where the portfolio scores/findings can back them;
  panels that exceed the current finding shape render a clear **"not yet captured — follow-on"** state,
  never fabricated data. Provenance of every panel is explicit:

  | v2 L1 panel | Backed by portfolio scoring? |
  |---|---|
  | Portfolio health — G/A/R counts | ✅ count `FinalBucket` across scored projects |
  | Confidence (avg) + "Needs PM Review" count | ✅ aggregate `Confidence` + `NeedsPmReview` |
  | Projects needing intervention (project, status, confidence, reason + citation) | ✅ derived from each project's health score (worst area / applied override + cited finding) |
  | Financial exposure — € amounts | ⛔ findings carry `Severity`, not monetary € → flag (US #3, deferred detail) |
  | Decisions blocking — per-decision days-open / owner / exec-queue | ⛔ per-decision structure not captured → flag |
  | "Where the pressure is" — Money / Decisions / **key-person risk** (alloc × absence, per person) | ⛔ per-person allocation/absence not captured → flag (US #7, deferred) |
  | Recommended actions — **owned + dated** table | ⛔ no recommendation-with-owner/deadline entity; Narrative is the closest surface → flag (US #4, deferred) |

Not in scope (roadmap follow-ons): **L3 Data Quality** (separate later change; it will reuse this change's
`DistinctProjectKeys` enumeration); **retrofitting the shipped L2 view** onto the shared design system
(small follow-on after this change); any **finding-shape enrichment** to back the € / per-decision /
key-person / recommendation panels; a **first-class `Project` entity**.

## Capabilities

### New Capabilities

- `executive-portfolio`: The Level-1 portfolio roll-up. Owns portfolio-wide project **discovery**
  (distinct keys over the findings store), the **fan-out scoring** over the existing per-project
  `HealthScoringService`, the roll-up result shape (G/A/R counts, aggregate confidence + `NeedsPmReview`
  count, ordered intervention list with cited reasons), the empty-store 200 semantics, the `GET
  /api/portfolio` read surface, and the presentation-only boundary for the L1 view (match v2 layout,
  populate backed panels, flag unbacked ones).

### Modified Capabilities

<!-- None. This change adds a repository method and a new read capability; it does not change the
     spec-level behaviour of project-findings, health-scoring, or the analysis pipeline. The existing
     per-project score contract (ScoreProject) and finding shape are unchanged and reused as-is. -->

## Impact

- **Code:**
  - `source/AiPMOInsight.Application/Abstractions/IFindingRepository.cs` — add
    `DistinctProjectKeysAsync(CancellationToken)`.
  - `source/AiPMOInsight.Infrastructure/Findings/EfFindingRepository.cs` — implement it
    (`Set<Finding>().Select(f => f.ProjectKey).Distinct()`); no schema change / no migration
    (`project_key` already exists and is queryable).
  - New `source/AiPMOInsight.Application/Features/ExecutivePortfolio/` — the `ScorePortfolio` query +
    handler + result records, fanning out over the existing `HealthScoringService`.
  - New `source/AiPMOInsight.Api/Endpoints/ExecutivePortfolioEndpoints.cs` (`MapExecutivePortfolioEndpoints`,
    registered in `Program.cs`) exposing `GET /api/portfolio`.
  - Client: new `ClientApp/src/components/ExecutivePortfolio.jsx` + route in `AppRoutes.jsx` + `NavMenu`;
    a shared stylesheet (extracted v2 pieces) in `styles.scss` (or a partial); pure helpers for any
    render mapping (consistent with Change 1's `health.js`).
- **API:** one new read endpoint; no change to existing contracts.
- **Persistence:** none — no new columns, no migration (`DistinctProjectKeys` is a query over an existing
  indexed column).
- **Tests:** backend integration test via `TestWebAppFactory` — upload→analyze **multiple** project keys,
  then assert `GET /api/portfolio` returns the correct G/A/R counts, intervention ordering (worst-first),
  and cited reasons; empty store → zeroed 200. Repo has **no JS test harness** — client render logic
  covered by pure helpers + manual `/verify`, as in Change 1.
- **Docs:** roadmap Phase 5 Level-1 flips ⬜ → ✅; a `## Dashboards (L1)` note in `CLAUDE.md`.
- **Deferred:** L3 Data Quality; L2 retrofit onto the shared design system; finding-shape enrichment for
  the € / per-decision / key-person / owned-recommendation panels.
