## Why

Phase 4 shipped the per-project **Red/Amber/Green (RAG) health score** engine ("RAG" here = the health
colour, never retrieval-augmented generation) and exposed it at `GET /api/projects/{projectKey}/health`
— fully auditable (raw score + bucket, applied overrides, confidence, "Needs PM Review", per-area
breakdown). But **nothing in the UI consumes it.** The Level-2 individual-project view
(`ProjectFindings.jsx`, route `/projects`) still shows only four raw finding tables and never calls the
health endpoint, so a PM opening a project sees cited findings but **no headline status, no score, no
confidence, no override trail.** The PRD's Level-2 story (#5, #6, #10) is "open one project and see its
RAG status + explanation + confidence, with the reasoning I can verify." The data already exists and is
cited; only the read surface is missing. This is the first of three Phase 5 dashboard changes and the
cheapest — it needs **zero new backend or analysis**.

## What Changes

- **New `project-status-dashboard` capability** — the Level-2 rich view. The React project view
  additionally fetches `GET /api/projects/{projectKey}/health` alongside the existing
  `GET /api/projects/{projectKey}`, and renders the two together as one project status page.
- **RAG status banner** — the view surfaces `FinalBucket` (the health colour) and `RawScore` as a
  prominent banner at the top of the project view.
- **Score audit surfacing** — the per-area breakdown (`Area → Severity → Weight → Contribution`), the
  aggregate `Confidence`, and the ordered applied-override trail (each override's `RuleId` / `Floor` /
  `Reason` + the cited finding locator that tripped it) are rendered, so "raw score was Green but an
  override forced Amber" is visible in the UI, not just the API (PRD user story #10).
- **"Needs PM Review" flag** — rendered as a distinct signal, orthogonal to the RAG colour (it can fire
  on any colour when aggregate confidence is very low).
- **Existing four cited sections kept** — Narrative / Findings / Challenge / Review continue to render
  exactly as today, each item citing its source. The banner sits above them.
- **Defined rendering for every health-endpoint response** — the view has a specified state for each:
  `200` with a `Score` (full banner + breakdown), `200` with a `null Score` ("scoring pending — findings
  exist but nothing scoreable yet"), and `404` ("no such project / no findings on record"). These already
  distinguish at the API; this change pins the UI behaviour for each.
- **Presentation-only, gaps flagged not filled** — where the PRD's Level-2 wishlist asks for **dated
  upcoming milestones (next 2–4 weeks)**, **per-decision owner/deadline/consequence**, or an **explicit AI
  recommendation** and the current finding shape does not carry that structure, the view renders what
  findings *do* carry and notes the gap. **No agent, prompt, or finding-shape changes** are in scope.

Not in scope (roadmap follow-ons, separate later changes): **Level-1 Executive Portfolio** (needs a
portfolio-enumeration query — `DistinctProjectKeys` + a `ScorePortfolio` fan-out) and **Level-3 Data
Quality** (portfolio-wide filter on `DataQuality`-area findings). No portfolio enumeration is introduced
here. No changes to ingest, analysis, the scoring engine, or any API contract.

## Capabilities

### New Capabilities

- `project-status-dashboard`: The Level-2 individual-project status view. Owns the requirement that the
  project view consumes both the findings and health read surfaces for a project key; the RAG status
  banner; the per-area / confidence / override audit surfacing; the "Needs PM Review" presentation; the
  defined rendering for each health-endpoint response (scored / scoring-pending / not-found); and the
  presentation-only boundary (render existing finding shape, flag unmet PRD fields, never enrich findings).

### Modified Capabilities

<!-- None. This change is purely additive presentation consuming existing, unchanged API contracts
     (project-findings and the health-scoring read endpoint). No spec-level behaviour of an existing
     capability changes. -->

## Impact

- **Code (client only):**
  - `source/AiPMOInsight.Api/ClientApp/src/components/ProjectFindings.jsx` — add a parallel fetch of
    `/api/projects/{projectKey}/health`; render the RAG banner, per-area breakdown, confidence, override
    trail, and "Needs PM Review" above the existing four sections; handle the scored / null-score / 404
    states. (May be split into a small `HealthBanner` sub-component for clarity.)
  - `source/AiPMOInsight.Api/ClientApp/src/styles.scss` — RAG colour styling for the banner/severity chips
    (Red/Amber/Green), theme-aware.
  - Possibly a rename/relabel of the view heading from "Project status (Level 2)" to match the dashboard
    framing; route stays `/projects`.
- **API:** none. Both endpoints (`GetProjectFindings`, `ScoreProject`) already return the required shape;
  no new endpoint, no contract change.
- **Tests:** an integration test that uploads a known set through `TestWebAppFactory`, analyzes, and
  asserts the health endpoint returns a scored result for the L2 view to consume (reusing the cookie/auth
  pattern); client-side rendering assertions for the three response states if a component test harness is
  in place, otherwise covered by the integration + manual `/verify` of the running app.
- **Docs:** a roadmap Phase 5 status update (Level-2 flips from 🟡 to ✅ for the rich view); a short note
  in `CLAUDE.md` describing the dashboard read surface.
- **Deferred:** Level-1 and Level-3 dashboards (separate changes); any finding-shape enrichment needed to
  fully satisfy the milestone/decision/recommendation PRD fields.
