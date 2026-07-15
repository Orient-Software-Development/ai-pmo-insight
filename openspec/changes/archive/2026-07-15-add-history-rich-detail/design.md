## Context

`add-upload-history` shipped two authenticated reads — `GET /api/uploads` (`GetUploads` → `Id`,
`FileName`, `UploadedAt`) and `GET /api/uploads/{id}/findings` (`GetUploadFindings` → the latest run's
findings partitioned into `Findings`/`Narrative`/`Challenge`/`Review`, each `FindingView` carrying
`RunId`, `PromptVersion`, `Confidence`, `ProducingAgent`, `Citation{Locator,…}`, `CreatedAt`) — plus a
minimal `History.jsx` rendering the four sections as bare tables. Phase 4 added
`GET /api/projects/{key}/health` (`ScoreProject` → `HealthScore` with `FinalBucket` + cited
`AppliedOverrides`). This change rebuilds `History.jsx` into a master-detail **audit** surface (US-9/US-10)
on the shared Phase 5 design system, **presentation-only**: no endpoint, agent, prompt, finding-shape, or
schema change. The reads do not cover every field the wireframe lists, so the boundary (flag, never
fabricate — the same L1/L2/L3 hold) decides what renders live.

## Goals / Non-Goals

**Goals:**

- Master-detail `/history`: sticky upload list (newest first) + detail panel for the selected upload, on
  the shared `--rag-*` / `records` / `sev` / `eyebrow` / `block` system.
- A run-provenance header from findings data only: run id, prompt hash(es) (`PromptVersion`), date.
- The four cited sections (analysis/narrative/challenge/review) restyled, data unchanged.
- A score-audit section that **reuses `GET /api/projects/{key}/health`** per referenced project (cited
  overrides), labelled current with the latest-run caveat (user-confirmed).
- Strictly read-only; flag uploader / LLM model / project count / multi-file / live status; fabricate nothing.

**Non-Goals:**

- Any backend read/endpoint/finding field (uploader, LLM model, project count, multi-file grouping, live
  per-agent status, a strict per-run historical score audit) — all follow-ons.
- Search, pagination, re-analyze, delete, edit — explicitly out (read-only constraint).
- A JS test harness — none in the repo; correctness rides the existing backend integration tests + `/verify`.

## Decisions

**1. Reuse three existing reads; add none.** Master list ← `GET /api/uploads`. Detail sections + header ←
`GET /api/uploads/{id}/findings`. Score audit ← `GET /api/projects/{key}/health` per distinct project key
in the selected run. No new endpoint. Rationale: honors #36's presentation-only constraint; every datum is
already served.

**2. Provenance header is derived, not fetched.** Run id, date, and prompt hash come from the findings
already loaded: `RunId` and `CreatedAt` off any finding; the prompt-hash set = the distinct non-null
`PromptVersion`s across the run's LLM findings (deterministic agents have none). No model id exists on
`FindingView`, so "LLM model" renders a flagged follow-on — not inferred. Rationale: the hash is what makes
a run auditable; the model id is simply not in the contract.

**3. Score audit = per-project health, labelled current, caveat shown (user-confirmed).** For each distinct
`ProjectKey` in the selected run, call `GET /api/projects/{key}/health` and render `FinalBucket` +
`AppliedOverrides` (rule, floor, reason, cited locator). A project with no overrides is shown as
"raw score, no override fired" — not omitted. The section is labelled the project's **current** health with
an explicit caveat that, for a superseded run, a strict per-run historical audit is a follow-on. Reuse
`bucketColour` + the `healthState` mapping from `health.js`; a 404 (no findings for that key) renders a
neutral "not scored" line, never an error banner. Rationale: exact for the newest upload, honest about the
superseded case, and needs no backend change. *Alternative (per-run score read):* rejected here — it adds a
slice + endpoint and breaks the presentation-only boundary #36 sets; noted as the follow-on.

**4. Coarse status only.** Analysis is synchronous (`POST /api/analyze` returns when done); there is no
run-status entity. Status is derived: findings present → *Analyzed*; known upload, empty → *Not analyzed*.
Running/Failed pills render a flagged follow-on. Rationale: no fabricated lifecycle the backend can't confirm.

**5. Pure helper for the mappings.** A small `history.js` (mirroring `health.js`/`dataQuality.js`) holds:
the coarse status derivation, the distinct-project-key extraction, and the distinct prompt-hash set — so
the view logic is testable-by-reading even without a JS runner. `bucketColour`/`healthState` are imported
from `health.js`, not duplicated.

**6. Layout on the shared system, no new endpoints of CSS churn.** Master-detail via a two-column grid that
collapses on narrow screens; rows use the existing list/`records` idioms; the header uses `eyebrow` +
`sev` chips; flagged fields use the existing `flagged-panel`/`flagged-note`. Add only minimal
history-specific SCSS (e.g. a sticky master column) reusing the existing tokens.

## Risks / Trade-offs

- **[Score audit can look stale for a superseded upload]** → Mitigated by Decision 3's explicit "current
  health" label + caveat; pinned by the spec scenario. The alternative (per-run read) is the honest
  follow-on, not silent staleness.
- **[N health calls for a run spanning N projects]** → POC scale is tiny (usually one project per upload);
  fetch per distinct key, in parallel (`Promise.allSettled`) so one 404/failure never blanks the section.
- **[Wireframe implies more than the reads carry]** → The provenance table (proposal) + flagged states keep
  the page honest; reviewers confirm no fabricated uploader/model/status/count slipped in.
- **[No JS tests]** → Same posture as L1/L2/L3: pure helper + the existing `UploadHistoryEndpointsTests`
  (data path) + `/verify` in the running app.

## Migration Plan

Client-only: rebuild `History.jsx` + add `history.js` helper + minimal SCSS on the existing shared system.
No API, persistence, migration, or finding-shape change. Rollback = revert the commit; the endpoints and
the current data path are untouched. Roadmap Phase 5 History-detail note flips to ✅ on merge.

## Open Questions

- Does the score-audit section fetch health for **every** distinct project key in the run, or only the
  primary/first? (Leaning every distinct key, parallel + independent; revisit at `/verify` if a run spans
  many projects and the panel gets long.)
- Master-column density: show the prompt-hash short form in the master row, or only in the detail header?
  (Leaning detail-only to keep rows to date + file; decide at `/verify` against the wireframe.)
