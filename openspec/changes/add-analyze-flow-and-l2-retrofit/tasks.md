> Implement test-first where a test surface exists (red → green → refactor); keep the suite green before
> checking off a task. Terminology: "RAG" = Red/Amber/Green health colour, never retrieval-augmented generation.
> Scope reminder: **presentation-only, client-side.** No backend / API / finding-shape / agent / prompt
> changes. The four consumed endpoints (`POST /api/ingest/upload`, `POST /api/analyze/{id}`,
> `GET /api/projects/{key}`, `GET /api/projects/{key}/health`) already return everything both pages need.
> Land the `/upload` route in this change **before** #33 wires the post-login redirect.

## 1. Confirm contracts and harness (no code)

- [ ] 1.1 Re-read the ingest/analyze responses (`POST /api/ingest/upload` → `{ uploadId }`;
      `POST /api/analyze/{uploadId}` → the analysis result with `findings[].projectKey`) and confirm the
      `/upload` page needs no new field beyond what they return.
- [ ] 1.2 Confirm the L2 data path in `ProjectFindings.jsx` (the `Promise.allSettled` dual fetch + the
      `healthState` mapping in `health.js`) is the contract to preserve, and that `ProjectStatusDashboardDataTests`
      is the backend test that must stay green through the retrofit.
- [ ] 1.3 Harness check: **`ClientApp` has no JS/JSX test harness** — do not introduce one in this
      presentation-only change. Coverage = the existing backend integration test (§2) staying green +
      manual `/verify` of both pages (§6). Read the v2 wireframe's `upload` and `l2` pages for the target layout.

## 2. Lock the L2 data path (regression guard, TDD)

- [ ] 2.1 (red/green) Run `ProjectStatusDashboardDataTests` and confirm it passes on the current view — this
      is the behaviour the retrofit must preserve. No new test is added unless a gap is found.
- [ ] 2.2 Note the assertion surface (both surfaces populated for `ALPHA`; unknown key → 404 health + 200
      empty findings) so any accidental data-path change during the restyle is caught by re-running it.

## 3. `/upload` cold-start page (extract, then style)

- [ ] 3.1 Create `Upload.jsx` and move the upload → analyze flow out of `ProjectFindings.jsx`: file picker,
      `POST /api/ingest/upload`, `POST /api/analyze/{uploadId}`, using the existing `authFetch`. Preserve the
      `ACCEPTED_EXTENSIONS` / `isAcceptedFile` guard and CSV-rejection copy verbatim (accepts `.xlsx .xlsm
      .xml .docx`; CSV refused before upload).
- [ ] 3.2 Add the protected `/upload` route to `AppRoutes.jsx` (`RequireAuth`), and an "Upload" entry to
      `NavMenu.jsx`.
- [ ] 3.3 Style the page to the wireframe's `upload` page: drop zone ("Drop files here, or click to browse"),
      advertised accepted formats, a "this upload · parse results" area, and a "Run analysis →" action with a
      coarse pipeline status (uploading → analyzing → done / failed) driven by the request lifecycle.
- [ ] 3.4 On analyze success, offer a "view results →" path to the L2 view for the analyzed `projectKey`
      (per design Open Question — link, not inline findings).
- [ ] 3.5 Render the wireframe panels the API does not back — per-file parse status/notes, duplicate-identity
      merge (US-2), the live nine-agent stepper (US-9) — as explicit "not yet captured — follow-on"
      placeholders. Fabricate nothing.

## 4. L2 status retrofit (styling/layout only, data path frozen)

- [ ] 4.1 Remove the embedded upload form from `ProjectFindings.jsx` (now on `/upload`); keep the
      `Promise.allSettled` dual fetch, `healthState` mapping, and the four cited sections exactly as tested.
- [ ] 4.2 Add the wireframe project header: project key + name, RAG chip from `FinalBucket` (labelled),
      confidence pill, score-overridden indicator (when `FinalBucket` ≠ `RawBucket`), sponsor/PM when present,
      and a project switcher (keys the view already holds / free-text entry — no enumeration endpoint).
- [ ] 4.3 Restyle the body into the wireframe `l2` sections (Progress · Deviations · Risks & Issues ·
      Milestones · Decisions · AI Recommendation · Score audit), each risk/issue row keeping its
      Finding · Challenge · Review breakdown and citation.
- [ ] 4.4 Keep the honest gap-note: dated milestones / per-decision owner-deadline / explicit AI
      recommendation exceed the finding shape → Narrative stays the closest recommendation surface; nothing
      fabricated.
- [ ] 4.5 Re-run `ProjectStatusDashboardDataTests` — still green (retrofit changed no data path).

## 5. Shared styling + accessibility

- [ ] 5.1 Extend the L1-established Phase 5 design tokens in `styles.scss` (`--rag-*`, `sev` chips, `records`
      table, hairline rules) to the `/upload` page and the L2 header/sections — theme-aware (light default +
      `[data-theme=dark]` / `prefers-color-scheme: dark`), no inline hex.
- [ ] 5.2 Every RAG colour is paired with its text label + numeric score across both pages (colour-blind
      safe); status never conveyed by colour alone.

## 6. Verify + document

- [ ] 6.1 `/verify` the running app: sign in → land on `/upload` → upload an accepted file → analyze →
      "view results" → L2 renders in the wireframe system across SCORED / SCORING_PENDING / NOT_SCORED;
      CSV rejection still fires; placeholders render where panels are unbacked.
- [ ] 6.2 Confirm the diff is presentation-only: only `ClientApp` (`Upload.jsx`, `AppRoutes.jsx`,
      `ProjectFindings.jsx`, `NavMenu.jsx`, `styles.scss`) + docs/openspec. No backend/API/domain source touched.
- [ ] 6.3 Roadmap Phase 5: L2 flips to ✅ (styled rich view) and note the `/upload` cold-start page; add a
      short "analyze flow + L2 retrofit" note to `CLAUDE.md`.
- [ ] 6.4 Full backend suite green; `openspec validate add-analyze-flow-and-l2-retrofit --strict` passes.
