## Why

The Phase 5 v2 wireframe (`docs/designs/phase5-wireframe-v2.html`) has **seven pages**. Six map to
issues; **two do not** — `upload` and `l2` — and both are UI-only gaps sitting on top of surfaces that
already exist and are tested.

- **L2 is built but never restyled.** `add-project-status-dashboard` (archived) shipped the Level-2 view
  at route `/projects` (`ProjectFindings.jsx`): it reads the findings surface and the health surface
  concurrently, renders `HealthBanner` + the four cited sections, and its data path is locked by
  `ProjectStatusDashboardDataTests`. But the page still sits on **bare Pico defaults** — plain
  `<form role="group">` and `<table>` — so it does not look like the wireframe's `l2` page. L1
  (`ExecutivePortfolio`) was built to the v2 design system *"ready for the L2 retrofit"* (CLAUDE.md);
  that retrofit is the second half of this change.
- **The `/upload` cold-start page does not exist.** The Auth UI change (#33) specifies *"post-login
  navigates to `/upload`"*, but there is **no `/upload` route and no Upload component** in
  `AppRoutes.jsx`. Today the upload → analyze flow is embedded at the top of `ProjectFindings.jsx`. The
  wireframe's `upload` page is a dedicated ingest / cold-start surface (drop zone, per-file parse
  results, "Run analysis" + a live agent-pipeline panel). That page is the first half of this change.

Both halves are **presentation-only**: no agent, prompt, finding-shape, or API-contract change. They
compose the existing endpoints (`POST /api/ingest/upload`, `POST /api/analyze/{id}`,
`GET /api/projects/{key}`, `GET /api/projects/{key}/health`). Where the wireframe shows richer detail
than those surfaces carry, the view renders what exists and flags the gap — it never fabricates data.
Tracks GitHub issue #38.

## What Changes

- **New `/upload` cold-start page** — extract the upload → analyze flow out of `ProjectFindings.jsx` into
  its own `Upload.jsx` at route `/upload` (`RequireAuth`). Styled to the wireframe's `upload` page: a
  file drop zone (accepts `.xlsx · .xlsm · .xml · .docx`; CSV rejected up front, unchanged behaviour), a
  "this upload · parse results" table, and a "Run analysis →" action with an analysis-pipeline panel.
  Post-login redirect target becomes `/upload`, satisfying the assumption in #33.
- **L2 status retrofit** — restyle `ProjectFindings.jsx` (route `/projects`) to the wireframe's `l2`
  design system: a project header (key + name, RAG chip from `FinalBucket`, confidence pill, score-
  overridden indicator, sponsor/PM where present, project switcher), the sectioned body (Progress ·
  Deviations · Risks & Issues · Milestones · Decisions · AI Recommendation · Score audit), and each
  risk/issue row keeping its Finding · Challenge · Review breakdown with its citation. **No backend or
  data-path change** — `ProjectStatusDashboardDataTests` stays green.
- **Shared design tokens reused** — both pages consume the Phase 5 SCSS system L1 established (palette,
  type scale, `--rag-*` custom properties, `sev` chips, `records` table, hairline rules), so L1 / L2 /
  the upload page read as one system.
- **Presentation-only boundary, gaps flagged not filled** — panels the wireframe shows that the current
  surfaces do not back — **per-file parse status**, **duplicate-identity merge** (US-2), **live
  per-agent progress** (US-9), **dated upcoming milestones**, **per-decision owner/deadline/
  consequence** — render as explicit "not yet captured — follow-on" placeholders, never fabricated. Any
  of them becoming real is a separate backend change.

Not in scope: any change to ingest, analysis, the scoring engine, finding shape, or API contracts; the
L3 Data Quality dashboard (#35); the History rich detail view (#36); the auth surfaces (#33); multi-file
batch grouping (deferred to `add-multi-file-analyze`).

## Capabilities

### New Capabilities

- `analyze-flow-ui`: The ingest / cold-start `/upload` page. Owns the requirement that the upload →
  analyze flow has a dedicated authenticated page (extracted from the L2 view), that it renders the drop
  zone / accepted-formats / CSV-rejection behaviour, that it presents parse results and an analysis-
  pipeline panel for what the API returns, that post-login lands here, and the presentation-only boundary
  (per-file parse detail, duplicate-identity merge, and live per-agent progress render as flagged
  follow-on placeholders, never fabricated).

### Modified Capabilities

- `project-status-dashboard`: The Level-2 view gains a **wireframe-conformant presentation** requirement
  — the existing scored / scoring-pending / not-found render states and the cited four sections are
  unchanged in behaviour, but the view MUST present them in the v2 `l2` design system (project header
  with RAG chip + confidence + score-overridden + switcher; the sectioned body; per-row Finding /
  Challenge / Review). No data-path or API-contract behaviour changes.

## Impact

- **Code (client only):**
  - `source/AiPMOInsight.Api/ClientApp/src/components/Upload.jsx` — **new**; the extracted upload →
    analyze flow + parse-results + pipeline panel, styled to the wireframe.
  - `source/AiPMOInsight.Api/ClientApp/src/AppRoutes.jsx` — add the `/upload` protected route.
  - `source/AiPMOInsight.Api/ClientApp/src/components/ProjectFindings.jsx` — remove the embedded upload
    form (now on `/upload`); restyle to the `l2` design system (header, sectioned body, per-row
    Finding/Challenge/Review). Data path unchanged.
  - `source/AiPMOInsight.Api/ClientApp/src/components/NavMenu.jsx` — add an "Upload" / ingest nav entry;
    post-login redirect → `/upload` (coordinated with #33).
  - `source/AiPMOInsight.Api/ClientApp/src/styles.scss` — extend the shared Phase 5 design tokens to the
    upload page and the L2 header/sections; theme-aware.
- **API:** none. All four endpoints already return the required shape; no new endpoint, no contract change.
- **Tests:** the L2 data path stays covered by the existing `ProjectStatusDashboardDataTests`
  integration test (must stay green). The repo has **no JS test harness**, so the render of both pages is
  verified via the running app (`/verify`). No new backend test is required unless a follow-on adds a
  backend signal.
- **Docs:** a short `CLAUDE.md` note that the `/upload` cold-start page and the L2 wireframe retrofit
  landed; a roadmap Phase 5 status update (L2 flips to ✅ for the styled rich view).
- **Deferred:** per-file parse status, duplicate-identity merge, and live per-agent progress (each needs
  a backend signal); L3 Data Quality (#35); History rich detail (#36); auth surfaces (#33).
