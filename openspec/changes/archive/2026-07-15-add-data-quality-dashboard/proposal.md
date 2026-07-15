## Why

Phase 5 defines three dashboard levels. L2 (`add-project-status-dashboard`) and L1
(`add-executive-portfolio-dashboard`) have shipped; **L3 Data Quality is the last one, and nothing
renders it today.** The PRD's US-8 is the data-lead's view: *"a Level-3 Data Quality view listing
missing/inconsistent items and remediation actions, so I know exactly what to fix to lift confidence
back above the target threshold."* The raw material already exists — the deterministic **Data Quality
agent (#2)** emits `Area = DataQuality` findings (missing fields, stale updates, inconsistent/orphan
references), each cited, on every run — but **no query rolls those findings up across the whole
portfolio**, and there is no read surface for the data lead. This change adds that roll-up and view.
Like L1 it is a **read over the findings store, not new analysis** (no LLM cost): it reuses L1's
`DistinctProjectKeysAsync` for enumeration and the existing `HealthScoringService` for the confidence
figure, so the portfolio's data-quality picture can never disagree with the scores it already shows.

## What Changes

- **New `data-quality-dashboard` capability** — a portfolio-wide read over the `DataQuality`-area
  findings, exposed as `GET /api/data-quality/summary` (authenticated, shared-workspace, view-only).
- **Enumeration is reused, not re-implemented** — the slice enumerates projects via the existing
  `IFindingRepository.DistinctProjectKeysAsync` (introduced by L1) and, per project, reads the **latest
  run's** findings (the same "latest run per project" resolution `HealthScoringService` already uses).
  **No new repository method, no schema change, no migration.**
- **`SummarizeDataQuality` slice** (`Application/Features/DataQuality`) rolls the latest-run
  `DataQuality`-area findings up into:
  - a **portfolio confidence figure** — the mean of each scored project's aggregate `Confidence` (reusing
    `HealthScoringService`, identical to L1's `AverageConfidence`) together with the configured **publish
    threshold** (`HealthScoringOptions.ConfidenceFloor`) and a **below-target** flag when the mean is under it;
  - a **missing/inconsistent items list** — one entry per `DataQuality` finding across the portfolio:
    project key, the issue text (finding `Summary`), its `Severity`, and its **citation locator**, ordered
    worst-first (Red before Amber);
  - **counts** — total items and a per-project count, so the data lead sees where the gaps cluster.
- **Empty store is a defined 200** — no findings on record → 200 with a zeroed confidence block and an
  empty items list, never a 404 (consistent with L1).
- **New L3 React view + route** (`/data-quality`, `RequireAuth`), built to the **v2 wireframe**
  (`docs/designs/phase5-wireframe-v2.html`, `data-page="l3"`) on the **shared Phase 5 SCSS design system**
  L1 established (`--rag-*`, `records` table, `sev` chips, `eyebrow`, `block`) — styled from the first
  commit, no plain-then-restyle. Added to `NavMenu`.
- **Presentation-only boundary holds (as in L1/L2) — flag, never fabricate.** The v2 L3 layout is matched,
  but panels are populated only where the current finding shape can back them. `DataQuality` findings carry
  `Summary` + `Severity` + `Citation` + `Confidence` and **nothing else** — no structured remediation text,
  no per-item age, no quantified confidence *lift*, no duplicate-group. So:

  | v2 L3 panel | Backed by `DataQuality` findings today? |
  |---|---|
  | Confidence hero — portfolio avg confidence % + publish threshold + below-target banner | ✅ mean of per-project `HealthScore.Confidence`; threshold = `ConfidenceFloor` |
  | Missing/inconsistent items — Project · Issue · Severity · **cited** source | ✅ each `DataQuality` finding (`Summary` + `Severity` + `Citation`), worst-first |
  | Items table — **Age** column | ⛔ no structured age field (staleness age lives only inside the summary prose) → flag |
  | Items table — **Suggested remediation** column | ⛔ no remediation field on findings → flag |
  | Items table — ordered by quantified **confidence lift** | ⛔ DQ findings are uniformly High confidence; no per-item lift → order by **Severity** instead, flag the lift quantification |
  | **Areas completeness grid** (Schedule · Budget · Scope · Resources · Risks · Decisions · Minutes · Time entries) | ⛔ no per-area completeness metric; `HealthArea` has 5 buckets, not these 8 → flag |
  | **Duplicate-identity candidates** table + Merge / Keep-separate actions (US-2) | ⛔ the DQ agent emits no duplicate-detection output; nothing to render → dashed placeholder, never fabricated |

  Every flagged panel renders a clear **"not yet captured — follow-on"** state; **US-2's never-silently-merge
  rule is honoured by not shipping a merge action at all** until the signal exists.

Not in scope (roadmap follow-ons): **finding-shape enrichment** to back the age / remediation / lift /
8-area-completeness / duplicate-candidate panels (each needs the agent to emit new structured signal — a
separate change); **any merge/keep-separate action** (US-2, blocked on duplicate output existing); a
first-class `Project` entity.

## Capabilities

### New Capabilities

- `data-quality-dashboard`: The Level-3 Data Quality read surface. Owns the portfolio-wide roll-up of
  latest-run `DataQuality`-area findings (reusing `DistinctProjectKeysAsync` for enumeration and
  `HealthScoringService` for the confidence figure), the result shape (confidence block with the
  configured publish threshold + below-target flag, and the worst-first cited items list with counts),
  the empty-store 200 semantics, the `GET /api/data-quality/summary` read surface, and the
  presentation-only boundary for the L3 view (populate backed panels, flag the age / remediation / lift /
  areas-grid / duplicate-candidate panels as follow-ons).

### Modified Capabilities

<!-- None. This change adds a new read capability over existing findings. It does not change the
     spec-level behaviour of data-quality analysis (agent #2), health-scoring, executive-portfolio,
     project-status, or the finding shape. Enumeration (DistinctProjectKeys) and per-project scoring
     are reused from add-executive-portfolio-dashboard as-is. -->

## Impact

- **Code:**
  - New `source/AiPMOInsight.Application/Features/DataQuality/` — the `SummarizeDataQuality` query +
    handler + result records, fanning out over `DistinctProjectKeysAsync` +
    `GetByProjectKeyAsync` + the existing `HealthScoringService`.
  - New `source/AiPMOInsight.Api/Endpoints/DataQualityEndpoints.cs` (`MapDataQualityEndpoints`, registered
    in `Program.cs`) exposing `GET /api/data-quality/summary`.
  - Client: new `ClientApp/src/components/DataQuality.jsx` + route in `AppRoutes.jsx` + `NavMenu` link;
    any render mapping as a pure helper (consistent with L2's `health.js`); L3 styles reuse the shared
    Phase 5 design system already in `styles.scss`.
- **API:** one new read endpoint; no change to existing contracts.
- **Persistence:** none — no new columns, no migration (reads existing findings by an already-indexed key).
- **Tests:** backend integration test via `TestWebAppFactory` — upload→analyze **multiple** project keys
  (at least one carrying `DataQuality` findings of differing severity), then assert
  `GET /api/data-quality/summary` returns the correct confidence block (mean + threshold + below-target),
  the worst-first cited items list, and the counts; empty store → zeroed 200. Repo has **no JS test
  harness** — client render logic covered by a pure helper + manual `/verify`, as in L1/L2.
- **Docs:** roadmap Phase 5 Level-3 flips ⬜ → ✅; a `## Dashboards (L3)` note in `CLAUDE.md`; issue #35
  and epic #8 updated.
- **Deferred:** finding-shape enrichment for the age / remediation / lift / areas-grid / duplicate panels;
  the US-2 merge action.
