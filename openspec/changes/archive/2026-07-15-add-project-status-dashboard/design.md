## Context

Phase 4 (`add-health-scoring`) shipped a pure, re-runnable per-project RAG score exposed at
`GET /api/projects/{projectKey}/health` (`ScoreProject.Result` → `ScoreView`: `RawScore`, `RawBucket`,
`FinalBucket`, `NeedsPmReview`, `Confidence`, `Areas[]`, `AppliedOverrides[]`). The Level-2 findings
surface `GET /api/projects/{projectKey}` (`GetProjectFindings.Result`) already returns the four cited
sections. The React SPA's `ProjectFindings.jsx` (route `/projects`) renders the findings sections and an
upload box, but **never calls the health endpoint** — so the score, confidence, and override audit are
invisible in the UI. This change wires the health surface into that view and renders the status banner.

The SPA is plain **JSX** (Vite + React Router; `AppRoutes.jsx`), auth via `authFetch`/`RequireAuth`
(`AuthContext.jsx`), Pico-based styling in `styles.scss` (theme variables like `--pico-del-color`,
`ThemeContext`/`ThemeToggle` for light/dark). Both endpoints are authenticated and shared-workspace.

This is Phase 5 **Change 1 of 3**, deliberately the cheapest slice: no new backend, no analysis, no new
endpoint. Level-1 (Executive Portfolio) and Level-3 (Data Quality) are separate later changes that will
require a portfolio-enumeration query (`DistinctProjectKeys` + a `ScorePortfolio` fan-out over the already
-pure `HealthScoringService`); this change deliberately does not touch that.

## Goals / Non-Goals

**Goals:**

- Consume `GET /api/projects/{projectKey}/health` in the Level-2 view alongside the existing findings read.
- Render a RAG status banner (`FinalBucket` colour + `RawScore`) above the existing four cited sections.
- Surface the audit detail: per-area breakdown, aggregate confidence, ordered applied-override trail (each
  with rule/floor/reason + cited finding locator), and the "Needs PM Review" flag as a distinct signal.
- Define and implement a rendering for each health response: scored / scoring-pending (200 null score) /
  not-found (404), none treated as an error.
- Keep it presentation-only: render existing shape, flag unmet PRD fields, change no backend contract.

**Non-Goals:**

- Level-1 Executive Portfolio and Level-3 Data Quality views (later changes).
- Any portfolio-wide enumeration or new query/endpoint/repository method.
- Enriching finding shape to add dated milestones, per-decision owner/deadline, or explicit AI
  recommendations (deferred; flagged in the view, not built).
- Changing the scoring engine, its config, ingest, or analysis.

## Decisions

**1. Two independent fetches, not a new aggregate endpoint.** The view issues the findings request and the
health request separately (both keyed by `projectKey`), rather than adding a backend endpoint that merges
them. Rationale: both endpoints already exist and are cheap reads; merging would add backend surface for
zero capability gain and couple two independently-useful reads. The two requests can run concurrently
(`Promise.all`) so the extra call adds no perceived latency. *Alternative considered:* a new
`GET /api/projects/{key}/status` aggregate — rejected as unnecessary backend work that this presentation
-only change is explicitly avoiding.

**2. The health request is independent of the findings request — one failing does not blank the other.**
The findings sections render from the findings result; the banner renders from the health result. A 404
or null score on `/health` yields a defined banner-area state while the findings sections still render (and
vice-versa). Rationale: the endpoints already model "findings exist but nothing scoreable" (200 null) and
"no findings on record" (404) as distinct, non-error outcomes — the UI mirrors that instead of collapsing
them into a single error.

**3. Response-state mapping is explicit and total.** The health fetch resolves to exactly one of three
render states:

```
  HTTP 200, score != null   → SCORED         (banner + areas + confidence + overrides + PM-review)
  HTTP 200, score == null   → SCORING_PENDING ("findings exist but nothing scoreable yet")
  HTTP 404                  → NOT_SCORED      ("no such project / no findings on record")
  (network / 5xx / 401)     → error surfaced via the existing error line; not a banner state
```

Rationale: the spec requires a defined rendering for each documented response; conflating null-score with
404 would mislead a PM (one means "analyzed, not yet scoreable", the other means "unknown key").

**4. RAG colour is data-driven from `FinalBucket`, theme-aware, defined once.** A small mapping
(`Red`/`Amber`/`Green` → colour token) drives the banner and the per-area severity chips. Colours are
defined in `styles.scss` with light/dark treatments (following the existing Pico theme-variable pattern)
rather than inline hex, so the palette is consistent and legible in both themes. The banner always shows
`FinalBucket`, never `RawBucket` — the override trail explains any divergence.

**5. Component shape: extract a `HealthBanner` sub-component, keep the page in `ProjectFindings.jsx`.** The
banner + audit rendering is self-contained and testable; the existing upload/analyze/load flow and the four
`Section` components stay as they are. Rationale: minimal churn to working code, clear seam for the new
rendering. *Alternative considered:* a brand-new page component — rejected; the existing view already owns
the project key and the four sections, and the banner belongs above them.

**6. Presentation-only gaps are labelled, not filled.** Where the PRD Level-2 wishlist exceeds the current
finding shape (dated milestones, per-decision owner/deadline/consequence, explicit recommendation), the
view renders what findings carry (e.g. Schedule severity, decision-area findings, the Narrative section as
the closest thing to a recommendation) and may show a small "not yet captured" note. No fabricated data.

## Risks / Trade-offs

- **[Two requests can resolve in either order / one can fail alone]** → Model the health result as its own
  state with the three defined outcomes plus an error fallback; render the findings sections independently
  of the banner so a health failure never blanks the cited findings (and vice-versa).
- **[RAG colours may be inaccessible to colour-blind users if colour is the only signal]** → Pair each RAG
  colour with its text label (Red/Amber/Green) and the numeric score, so status is never colour-only.
- **[Presentation-only boundary could drift into finding-shape changes]** → The spec pins the boundary; any
  field the PRD wants but findings lack is rendered as "not yet captured", and the roadmap follow-on owns
  the enrichment. Reviewers check that no agent/prompt/contract changed.
- **[Component test tooling may not be set up in ClientApp]** → Primary coverage is an integration test
  (upload → analyze → assert `/health` returns a scored result the view can consume) plus a manual
  `/verify` of the running app across the three states; add JSX component tests only if a harness already
  exists, otherwise do not introduce one in this change.
- **[`RawScore` is a raw double]** → Format for display (e.g. rounded) in the view; the API value is
  unchanged.

## Migration Plan

Client-only change; no schema, no data migration, no API contract change. Ships with the SPA build. Rollback
is reverting the client commit — the health endpoint continues to exist and serve other/future consumers.
Roadmap Phase 5 Level-2 status flips from 🟡 to ✅ (rich view) on merge.

## Open Questions

- Should the "scoring pending" and "not found" banner-area states share one neutral treatment, or read
  differently? (Leaning: distinct short copy, same neutral colour — decided during implementation/verify.)
- Is there an existing JSX component-test harness in `ClientApp`, or is integration + manual verify the
  intended coverage for SPA changes in this repo? (Confirm before writing client unit tests.)
