## Context

The deterministic **Data Quality agent (#2)** (`DataQualitySkill`) already emits `Area = DataQuality`
findings on every run — missing project fields, missing milestone due dates, stale project data, and
inconsistent/orphan references — each with a `Summary`, a `Severity` (Amber for gaps, Red for
inconsistency), `Confidence.High`, and a mandatory `Citation`. Phase 4's `HealthScoringService` already
resolves the **latest run per project** and produces a per-project aggregate `Confidence`, and L1
(`add-executive-portfolio-dashboard`) added `IFindingRepository.DistinctProjectKeysAsync` to enumerate the
opaque `project_key`s. So the raw material and both enabling primitives exist; what is missing is a
**portfolio-wide roll-up of the `DataQuality` findings** and a **read surface** for the data lead.

This change adds that roll-up (`SummarizeDataQuality`) and the L3 view, built to
`docs/designs/phase5-wireframe-v2.html` (`data-page="l3"`) on the shared Phase 5 SCSS design system L1
established. Like L1 it is a **read over persisted findings, not new analysis** (no LLM). The v2 L3 panels
split cleanly into finding-backed and not-yet-backed (see the proposal's provenance table).

## Goals / Non-Goals

**Goals:**

- Roll up the latest-run `DataQuality`-area findings across all projects, reusing `DistinctProjectKeysAsync`
  (enumeration) and `HealthScoringService` (the confidence figure) — **no new repository method, no schema
  change, no migration.**
- A confidence block: mean per-project confidence + the configured publish threshold
  (`HealthScoringOptions.ConfidenceFloor`) + a below-target flag.
- A worst-first, **cited** missing/inconsistent items list (project, issue, severity, citation) + total and
  per-project counts.
- `GET /api/data-quality/summary` (authenticated, shared-workspace, view-only); empty store → zeroed 200.
- L3 React view to the v2 layout on the **shared** Phase 5 design system (no new stylesheet).
- Hold the presentation-only boundary: populate backed panels, flag unbacked ones, fabricate nothing, and
  ship **no** merge action (US-2).

**Non-Goals:**

- Any **finding-shape enrichment** — a per-item age field, a suggested-remediation field, a quantified
  confidence *lift*, an eight-category completeness metric, or a duplicate-identity/duplicate-group signal.
  Each needs the DQ agent to emit new structured output; all are deferred to separate changes.
- The **US-2 merge / keep-separate action** — blocked on a duplicate-identity signal existing at all.
- Re-running analysis or the LLM; a first-class `Project` entity; pagination / filtering / search.

## Decisions

**1. Enumeration + latest-run resolution are reused, not re-implemented.** `SummarizeDataQuality.Handler`
enumerates keys via `DistinctProjectKeysAsync`, and for each loads its findings via `GetByProjectKeyAsync`
(the same pair L1 uses). It resolves the latest run per project the same way the scorer does — group by
`RunId`, take the newest by `CreatedAt` — and keeps only that run's `Area == DataQuality` findings.
Rationale: single source of truth for "what the latest run said"; the L3 page can never disagree with L1/L2.
*Alternative:* a new `GetDataQualityFindingsAsync` repo method — rejected; the existing method already
returns everything, and a filter belongs in the slice, not a new query surface.

**2. The confidence figure is the health score's confidence, not a fresh DQ-only average.** The confidence
block's mean is the arithmetic mean of each **scored** project's `HealthScore.Confidence` (via
`HealthScoringService.Score`), identical to L1's `AverageConfidence`. Rationale: US-8 is about lifting
*confidence back above the target* — that target is the health scorer's `ConfidenceFloor`, so the figure
shown must be the same confidence the floor governs. Reusing it keeps one definition of "confidence" across
L1 and L3. Projects with a null score contribute nothing to the mean; empty portfolio → mean 0.

**3. The publish threshold comes from config, injected — never hard-coded.** The block reports
`HealthScoringOptions.ConfidenceFloor` as the threshold and sets `belowTarget = mean < ConfidenceFloor`.
The handler reads it from the already-registered `HealthScoringOptions` (the same options the scorer binds).
Rationale: the PMO owns that number (it is a flagged placeholder today); the dashboard must not fork it.

**4. Items list shape + ordering (pinned so tests are deterministic).** One entry per collected
`DataQuality` finding: `{ projectKey, issue = finding.Summary, severity = finding.Severity, citationLocator
= finding.Citation.Locator }`. Ordered **worst-first by severity** (Red → Amber → Green), with `projectKey`
then locator as a deterministic tiebreak. Counts: `totalItems` = list length; `perProject` = count grouped
by `projectKey`. Rationale: severity is the only prioritisation signal the finding shape actually carries —
the v2 "order by confidence lift" is **not** backed (DQ findings are uniformly `High` confidence), so
severity ordering is the honest substitute and the lift quantification is flagged.

**5. Result contract (enums as strings, matching the other read surfaces).**

```
Result(
  ConfidenceView Confidence,                 // Mean (double), Threshold (int), BelowTarget (bool)
  IReadOnlyList<ItemView> Items,             // worst-first, each cited
  int TotalItems,
  IReadOnlyList<ProjectCountView> PerProject // { ProjectKey, Count }
)
ItemView(string ProjectKey, string Issue, string Severity, string CitationLocator)
```

Endpoint `GET /api/data-quality/summary` (`DataQualityEndpoints.MapDataQualityEndpoints`, registered in
`Program.cs`), `.RequireAuthorization()`, shared-workspace. Empty store → 200 with `Confidence` mean 0,
empty `Items`, `TotalItems` 0.

**6. L3 view: build to v2 on the shared design system; flag the unbacked panels.** The view consumes the
endpoint and renders: the **confidence hero** (mean %, threshold, below-target banner) and the
**missing/inconsistent items table** (`records`/`sev` chips, worst-first) from live data. The panels the
finding shape cannot back — a per-item **age** column, a **suggested-remediation** column, ordering by a
quantified **lift**, the eight-category **areas-completeness grid**, and the **duplicate-identity
candidates** table — render a dashed **"not yet captured — follow-on"** placeholder in their v2 slot.
Crucially, **no merge / keep-separate control is shipped** while there is no duplicate signal, so US-2's
never-silently-merge rule cannot be violated. A small pure helper (mirroring L2's `health.js`) maps the
response to the view state and the below-target banner. RAG colours reuse the existing `--rag-*` custom
properties — no new stylesheet.

## Risks / Trade-offs

- **[N-project scoring = N repository loads (`GetByProjectKeyAsync` per key)]** → Same trade-off L1 accepted;
  fine at POC scale (a few dozen projects). A bulk load can replace it later behind the slice boundary.
- **[Much of the v2 L3 layout is unbacked — risk of a hollow page]** → The backed core (confidence hero +
  cited items list) *is* the US-8 spine (know what to fix to lift confidence); the flagged panels
  communicate roadmap intent honestly. Reviewers confirm no fabricated values and no merge action slipped in.
- **[Two "confidence" numbers could drift]** → Avoided by Decision 2: the DQ page reuses the health scorer's
  confidence, so it is the same number L1 shows, governed by the same `ConfidenceFloor`.
- **[Severity-vs-lift ordering could be mistaken for the PRD's "lift" ordering]** → The lift column/ordering
  is explicitly flagged as follow-on in the view; the spec pins severity ordering, so the substitution is
  deliberate and visible, not a silent approximation.
- **[Presentation-only boundary drift]** → Spec pins it; the diff must show no agent/prompt/finding-shape/API
  contract change beyond the one additive read slice + endpoint + view.

## Migration Plan

No schema change, no migration (reads existing findings by an already-indexed key). Backend adds one
Application slice (`Application/Features/DataQuality/SummarizeDataQuality`) and one endpoint (registered in
`Program.cs`). Client adds one view (`DataQuality.jsx`) + route (`/data-quality`) + `NavMenu` link + a pure
helper, all on the existing shared styles. Rollback = revert the commit; existing endpoints, scores, and the
finding shape are untouched. Roadmap Phase 5 Level-3 flips ⬜ → ✅ on merge; epic #8 / issue #35 close.

## Resolved Questions

- **Confidence hero denominator = scored projects only** (user-confirmed). The mean is over projects that
  produce a `HealthScore`, exactly matching L1's `AverageConfidence`, so L1 and L3 always show the same
  number. Projects with any `DataQuality` finding but no score do **not** widen the denominator.
- **Publish threshold = `HealthScoringOptions.ConfidenceFloor`, reused, not a separate DQ target**
  (user-confirmed). One threshold governs both the per-project "Needs PM Review" flag and the L3
  below-target banner — no new config, one consistent story. (It remains the flagged PMO placeholder.)
- **Item tiebreak within equal severity = `projectKey` then locator** — arbitrary but deterministic (tests
  depend on a stable order); the most-recent-first alternative buys nothing here.
- **Per-project count** rides in the result contract regardless; the L3 view renders it only where the v2
  `data-page="l3"` layout has a slot for it (a "where gaps cluster" cell), else it stays summary-only.
