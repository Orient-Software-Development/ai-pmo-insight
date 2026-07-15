> Scope: **client-only, presentation-only** — rebuild `History.jsx` into a master-detail audit surface on
> the shared Phase 5 design system. **No backend/API/finding-shape/schema change.** The repo has no JS test
> runner, so behaviour that can be a pure function lives in a `history.js` helper (verified by reading +
> driving the app), the data path is already locked by `UploadHistoryEndpointsTests` + the health-endpoint
> tests, and the render is `/verify`-checked in the running app (as L1/L2/L3). Reuse `bucketColour` /
> `healthState` from `health.js`; do not duplicate them.

## 1. Pure helper: history.js (mappings)

- [x] 1.1 Add `ClientApp/src/history.js` with pure helpers: `uploadStatus(view)` → coarse `Analyzed` /
      `NotAnalyzed` (findings present vs known-but-empty); `runProvenance(view)` → `{ runId, createdAt,
      promptHashes[] }` (distinct non-null `promptVersion` across the run); `projectKeys(view)` → the
      distinct `projectKey`s across the run's findings. No React, no I/O.
- [x] 1.2 Node-check the helpers against representative inputs (empty view, deterministic-only run with no
      prompt hash, multi-project run) so their branches are exercised without a JS harness. All pass.

## 2. Master-detail layout + master list

- [x] 2.1 Read the wireframe History surface (master-detail intent) and catalogue: BACKED = date + file name
      per row, detail four sections + citations, header run id/prompt hash/date, score audit via health;
      UNBACKED = uploader, project count, multi-file summary, live Running/Failed status, LLM model, strict
      per-run historical audit.
- [x] 2.2 Rebuilt `History.jsx` shell on the shared system: sticky left master list (newest first, from
      `GET /api/uploads`) + right detail panel; pre-selection prompt; empty-store state; independent
      list/detail loading + a single page error line (never blanks both).
- [x] 2.3 Master rows show only backed data (date, file name). **Refinement:** the list read carries no
      findings info, so per-row status isn't backed either — the coarse `uploadStatus` pill moved to the
      **detail** (derived once findings load), and per-row status joins uploader / project count / multi-file
      in a single flagged follow-on note under the list. Never fabricated. (Spec already allows this — the
      master requirement only mandates date + file name; status is in the flagged set.)

## 3. Detail: provenance header + four cited sections

- [x] 3.1 Provenance header from `runProvenance` (run id, date, distinct prompt hash(es)); LLM model +
      uploader render a "not captured — follow-on" state.
- [x] 3.2 Four sections (Analysis findings + citations, Narrative #7, Challenge #8, Review #9) restyled on
      the shared `records` / `sev` / `block` idioms; each finding shows confidence + cited locator.
- [x] 3.3 Known-upload-no-findings → explicit "not analyzed yet" state (not an error).

## 4. Detail: score-audit section (US-10, reuse health)

- [x] 4.1 For each `projectKeys(view)` entry, fetch `GET /api/projects/{key}/health` in parallel
      (`Promise.allSettled`, independent — one 404/failure never blanks the section).
- [x] 4.2 Renders each project's `FinalBucket` (`bucketColour` chip) + score audit by **reusing the existing
      `HealthBanner`** (area breakdown + applied-override trail with cited locators; empty overrides and the
      SCORING_PENDING/NOT_SCORED/ERROR states already handled by that component — no duplication).
- [x] 4.3 Section labelled the project's **current** health with the explicit caveat that a strict per-run
      historical audit is a follow-on (user-confirmed approach).

## 5. Read-only + boundary

- [x] 5.1 No re-analyze / delete / edit / search / pagination control anywhere in the view (rows are plain
      selection buttons; the only actions are navigation).
- [x] 5.2 All flagged follow-on markers use the existing `flagged-note` idiom; no fabricated uploader /
      model / count / status values are rendered.

## 6. Verify + document

- [x] 6.1 Verify: Vite build compiles `History.jsx` + `history.js` + SCSS (✓, 1.34s); the `history.js`
      helpers node-checked (§1.2); the data path is covered by the existing `UploadHistoryEndpointsTests` +
      health-endpoint tests (no backend change). **Open item:** a live browser pass needs the running stack
      (the API is in fact running locally) — offered to the user.
- [x] 6.2 Diff scoped: `History.jsx` rebuild + new `history.js` + minimal shared-system SCSS only (git
      status confirms exactly these + the openspec change). No new endpoint, no change to existing API
      contracts, finding shape, agents, prompts, or DB schema.
- [x] 6.3 "History rich detail" note added to `CLAUDE.md`; roadmap gets an additive ✅ History-detail bullet.
      (The L3 roadmap/CLAUDE line state is being managed by the user on this branch — left untouched.)
- [x] 6.4 `openspec validate --strict` passes. **Backend suite unchanged** — zero backend edits, so it stays
      at the last-run 239 green; not re-run this pass because the running app holds a build lock on the DLLs
      (a transient environment lock, not a code issue).
