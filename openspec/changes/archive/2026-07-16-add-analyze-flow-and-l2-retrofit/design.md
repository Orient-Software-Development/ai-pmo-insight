## Context

The Phase 5 v2 wireframe (`docs/designs/phase5-wireframe-v2.html`) defines seven pages. Six are covered by
issues; two — `upload` and `l2` — are UI-only gaps over surfaces that already exist and are tested:

- **L2** ships at route `/projects` (`ProjectFindings.jsx`). It reads `GET /api/projects/{key}` and
  `GET /api/projects/{key}/health` concurrently (`Promise.allSettled`), renders `HealthBanner` + the four
  cited sections (Narrative / Findings / Challenge / Review), and maps the health response to
  SCORED / SCORING_PENDING / NOT_SCORED / ERROR via the pure helper `healthState` (`health.js`). Its data
  path is locked by `ProjectStatusDashboardDataTests`. But the page uses **bare Pico defaults** — it does
  not match the wireframe's `l2` design language the way L1 (`ExecutivePortfolio`, styled to the v2 system)
  does.
- The **upload → analyze flow** is embedded at the top of `ProjectFindings.jsx` (file picker →
  `POST /api/ingest/upload` → `POST /api/analyze/{uploadId}` → load findings). The wireframe wants this on
  its own `/upload` cold-start page — which does not exist (`AppRoutes.jsx` has no `/upload` route). #33
  already assumes post-login lands on `/upload`.

The SPA is plain **JSX** (Vite + React Router; `AppRoutes.jsx`), auth via `authFetch` / `RequireAuth`
(`AuthContext.jsx`), styling in `styles.scss` with the L1-established Phase 5 design tokens (`--rag-*`
custom properties, `sev` chips, `records` table) layered over Pico theme variables
(`ThemeContext` / `ThemeToggle` for light/dark). All four endpoints are authenticated and shared-workspace.

This is a **presentation-only** change: no new backend, endpoint, query, agent, prompt, finding-shape, or
API contract. It composes the four existing reads and reuses the L1 design system. Tracks issue #38.

## Goals / Non-Goals

**Goals:**

- Extract the upload → analyze flow into a dedicated `/upload` page (`Upload.jsx`, `RequireAuth`), styled
  to the wireframe's `upload` page; make it the post-login landing route.
- Restyle the L2 view (`ProjectFindings.jsx`, `/projects`) to the wireframe's `l2` design system without
  changing its data path (`ProjectStatusDashboardDataTests` stays green).
- Reuse the L1 Phase 5 design tokens so upload / L1 / L2 read as one system, theme-aware and colour-blind
  safe (status never conveyed by colour alone).
- Render only what the four existing surfaces return; flag richer wireframe panels as explicit follow-on
  placeholders.

**Non-Goals:**

- Any backend signal the wireframe implies but the API does not return: per-file parse status,
  duplicate-identity detection/merge (US-2), live per-agent progress (US-9), dated milestones, per-decision
  owner/deadline/consequence, owned/dated recommendations. Rendered as placeholders, not built.
- L3 Data Quality (#35), History rich detail (#36), Auth surfaces (#33), multi-file batch grouping
  (`add-multi-file-analyze`).
- Any change to the health/findings/ingest/analyze contracts, the scoring engine, or finding shape.

## Decisions

**1. `/upload` is a new page that owns the ingest flow; L2 stops owning it.** The file picker + upload →
analyze logic moves out of `ProjectFindings.jsx` into `Upload.jsx`. Rationale: the wireframe models ingest
as a distinct cold-start surface, and #33 already routes post-login to `/upload`; keeping the flow inside
L2 would leave `/upload` a 404. After analyze completes, `/upload` routes the user onward (to `/projects`
for the analyzed key, or `/portfolio`) rather than rendering findings inline. *Alternative considered:*
leave the flow in L2 and alias `/upload` → `/projects` — rejected; it contradicts the wireframe and #33.

**2. L2 retrofit is styling + layout only — the data path is frozen.** `ProjectFindings.jsx` keeps its
`Promise.allSettled` dual fetch, `healthState` mapping, and the four-section render exactly as tested. The
change is: a wireframe project header (key + name, RAG chip from `FinalBucket`, confidence pill,
score-overridden indicator, sponsor/PM when present, project switcher), the sectioned body styling, and
per-row Finding / Challenge / Review presentation. Rationale: `ProjectStatusDashboardDataTests` is the
contract; a retrofit that keeps it green is provably behaviour-preserving. The switcher lists keys the view
already has (no portfolio enumeration introduced here).

**3. Presentation-only gaps are labelled, not filled — same boundary L1/L2 already hold.** Panels the
wireframe shows that no surface backs render an explicit "not yet captured — follow-on" placeholder:
per-file parse detail and duplicate-identity merge and the live agent stepper on `/upload`; dated
milestones, per-decision owner/deadline, and an explicit AI recommendation on L2 (the Narrative section
stays the closest recommendation surface, as today). No fabricated data. Reviewers check no
agent/prompt/contract changed.

**4. The analysis-pipeline panel reflects request lifecycle, not live agent telemetry.** `POST
/api/analyze/{id}` is a single request that returns when the run completes; there is no per-agent progress
stream. The `/upload` pipeline panel therefore shows coarse request states (Uploading → Analyzing → Done /
Failed) and renders the wireframe's nine-agent live stepper as a labelled placeholder. Rationale: honest to
the contract; a real stepper needs a backend progress signal (a separate change). *Alternative considered:*
faking per-agent ticks on a timer — rejected as fabricated telemetry.

**5. CSV rejection and accepted formats are preserved verbatim.** `Upload.jsx` keeps the existing
client-side `ACCEPTED_EXTENSIONS` / `isAcceptedFile` guard (`.xlsx .xlsm .xml .docx`; CSV rejected up front
with the existing copy). Rationale: the guard is deliberate (CSV parses to nothing — see the
`csv-parsing-deferred` decision); the retrofit must not regress it.

**6. Design tokens are shared, defined once.** The upload page and the L2 header/sections consume the same
`--rag-*` / `sev` / `records` SCSS the L1 view established, extended as needed in `styles.scss` with
light/dark treatments — not inline hex. Rationale: one system across L1/L2/upload; consistent, legible in
both themes, and each RAG colour is always paired with its text label + score.

## Risks / Trade-offs

- **[Moving the upload flow could break the tested analyze path]** → The move is a relocation, not a
  rewrite; the same `authFetch` calls run from `Upload.jsx`. `ProjectStatusDashboardDataTests` exercises
  the backend data path (unchanged); verify the relocated flow end-to-end in the running app.
- **[Wireframe richness invites fabrication]** → The spec pins the presentation-only boundary; every
  unbacked panel is a labelled placeholder and reviewers confirm no contract/finding-shape changed.
- **[No JS test harness in `ClientApp`]** → Primary coverage stays the existing backend integration test
  (`ProjectStatusDashboardDataTests`, must stay green) plus manual `/verify` of both pages across their
  states. Do not introduce a JS test harness in this change.
- **[Project switcher could imply a portfolio query]** → The switcher only offers keys the L2 view already
  holds (or a free-text key entry, as today); it introduces no enumeration endpoint. Portfolio-wide
  discovery stays with L1's `DistinctProjectKeysAsync`.
- **[Post-login redirect coordinated across two issues]** → `/upload` route lands in this change; #33 wires
  the redirect. Land the route first (this change) so #33's redirect never targets a 404.

## Migration Plan

Client-only change; no schema, no data migration, no API contract change. Ships with the SPA build.
Rollback is reverting the client commit — all four endpoints continue to serve. Roadmap Phase 5: L2 flips
to ✅ for the styled rich view; the `/upload` cold-start page is noted as landed. Land this before #33's
post-login redirect wiring.

## Open Questions

- After analyze on `/upload`, should the user auto-route to `/projects` for the analyzed key, to
  `/portfolio`, or stay on `/upload` with a "view results" link? (Leaning: a "view results →" link to
  `/projects?key=…`, decided during implementation/verify.)
- Does the wireframe's `l2` project switcher expect a fixed demo list, free-text key entry (as today), or
  both? (Confirm against the wireframe during implementation; no enumeration endpoint either way.)
