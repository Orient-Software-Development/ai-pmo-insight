> Implement test-first where a test surface exists (red → green → refactor); keep the suite green before
> checking off a task. Terminology: "RAG" = Red/Amber/Green health colour, never retrieval-augmented generation.
> Scope reminder: **presentation-only, client-side.** No backend / API / finding-shape / agent / prompt
> changes. The four consumed endpoints (`POST /api/ingest/upload`, `POST /api/analyze/{id}`,
> `GET /api/projects/{key}`, `GET /api/projects/{key}/health`) already return everything both pages need.
> Land the `/upload` route in this change **before** #33 wires the post-login redirect.

## 1. Confirm contracts and harness (no code)

- [x] 1.1 Re-read the ingest/analyze responses (`POST /api/ingest/upload` → `{ uploadId }`;
      `POST /api/analyze/{uploadId}` → the analysis result with `findings[].projectKey`) and confirm the
      `/upload` page needs no new field beyond what they return. Confirmed against the existing embedded flow
      in `ProjectFindings.jsx` — upload returns `{ uploadId }`, analyze returns `{ findings: [{ projectKey }] }`.
- [x] 1.2 Confirm the L2 data path in `ProjectFindings.jsx` (the `Promise.allSettled` dual fetch + the
      `healthState` mapping in `health.js`) is the contract to preserve, and that `ProjectStatusDashboardDataTests`
      is the backend test that must stay green through the retrofit. Confirmed.
- [x] 1.3 Harness check: **`ClientApp` has no JS/JSX test harness** — do not introduce one in this
      presentation-only change. Coverage = the existing backend integration test (§2) staying green +
      manual `/verify` of both pages (§6). Read the v2 wireframe's `upload` and `l2` pages for the target layout.

## 2. Lock the L2 data path (regression guard, TDD)

- [x] 2.1 (red/green) Run `ProjectStatusDashboardDataTests` and confirm it passes on the current view — this
      is the behaviour the retrofit must preserve. No new test is added unless a gap is found. **Green: 2 passed.**
- [x] 2.2 Note the assertion surface (both surfaces populated for `ALPHA`; unknown key → 404 health + 200
      empty findings) so any accidental data-path change during the restyle is caught by re-running it.

## 3. `/upload` cold-start page (extract, then style)

- [x] 3.1 Create `Upload.jsx` and move the upload → analyze flow out of `ProjectFindings.jsx`: file picker,
      `POST /api/ingest/upload`, `POST /api/analyze/{uploadId}`, using the existing `authFetch`. Preserve the
      `ACCEPTED_EXTENSIONS` / `isAcceptedFile` guard and CSV-rejection copy verbatim (accepts `.xlsx .xlsm
      .xml .docx`; CSV refused before upload).
- [x] 3.2 Add the protected `/upload` route to `AppRoutes.jsx` (`RequireAuth`), an "Upload" entry to
      `NavMenu.jsx`, and point post-login (`Login.jsx`) at `/upload` (coordinates with #33).
- [x] 3.3 Style the page to the wireframe's `upload` page: drop zone (drag-drop + click), advertised
      accepted formats, a "this upload" panel, and a "Run analysis →" action with a coarse pipeline stepper
      (uploading → analyzing → done / failed) driven by the request lifecycle.
- [x] 3.4 On analyze success, link to `/projects?key=<projectKey>` for the analyzed key (view-results
      hand-off; L2 auto-loads from `?key=`).
- [x] 3.5 Render the wireframe panels the API does not back — per-file parse status/notes, duplicate-identity
      merge (US-2), the live nine-agent stepper (US-9) — as explicit "not yet captured — follow-on"
      placeholders. Nothing fabricated.

## 4. L2 status retrofit (styling/layout only, data path frozen)

- [x] 4.1 Removed the embedded upload form from `ProjectFindings.jsx` (now on `/upload`); kept the
      `Promise.allSettled` dual fetch, `healthState` mapping, and the four cited sections exactly as tested.
- [x] 4.2 Added the wireframe project header: project key (title), RAG chip from `FinalBucket` (labelled
      via `.sev`), confidence, score-overridden indicator (when `FinalBucket` ≠ `RawBucket`), and a project
      switcher (free-text key entry — no enumeration endpoint). Sponsor/PM rendered only if the surface
      carries them (it does not yet).
- [x] 4.3 Restyled the body into the shared design system (`block` / `sec-head` / `records` / `sev` /
      `cite`); the four cited sections keep their citations; the wireframe's dated-milestones/per-decision
      panels render as a flagged follow-on.
- [x] 4.4 Kept the honest gap-note: dated milestones / per-decision owner-deadline / explicit AI
      recommendation exceed the finding shape → Narrative stays the closest recommendation surface; nothing
      fabricated.
- [x] 4.5 Re-ran `ProjectStatusDashboardDataTests` — still green (2 passed); retrofit changed no data path.

## 5. Shared styling + accessibility

- [x] 5.1 Extended the L1-established Phase 5 design tokens in `styles.scss` with `.dropzone`, `.pipeline`,
      `.l2-header` / `.l2-title-row` / `.l2-switcher` / `.l2-overridden`, `.analyze-done`, `.block` — reusing
      the existing `--rag-*` / `.sev` / `.records` / `.flagged-*` tokens (theme-aware via the shared vars), no
      inline hex.
- [x] 5.2 Every RAG colour is paired with its text label (`.sev` shows the FinalBucket text) + numeric score
      across both pages; status never conveyed by colour alone.

## 6. Verify + document

- [x] 6.1 Verified against the real stack (Docker Postgres up): login → `POST /api/ingest/upload`
      (orbit-sample.xlsx) → `POST /api/analyze/{id}` → `GET /api/projects/ORB-1006` returns all four cited
      sections AND `GET /api/projects/ORB-1006/health` returns 200 SCORED (Amber, score 70, confidence 100,
      area breakdown) — the exact data both pages consume. Unknown key → findings 200 + health 404
      (NOT_SCORED) confirmed. Vite production build compiles; `Upload.jsx` and the L2 header retrofit are
      present in the built bundle. **Open item:** a live in-browser visual pass runs against the Vite dev
      server (`npm run dev`) — offered to the user; not run headlessly here.
- [x] 6.2 Diff is presentation-only: only `ClientApp` (`Upload.jsx` new; `AppRoutes.jsx`, `Login.jsx`,
      `NavMenu.jsx`, `ProjectFindings.jsx`, `styles.scss`) + docs (`roadmap.md`, `CLAUDE.md`) + openspec.
      No backend/API/domain source touched.
- [x] 6.3 Roadmap Phase 5 updated (analyze flow + L2 retrofit entry, ✅) and a "Analyze flow UI + L2 retrofit"
      note added to `CLAUDE.md`.
- [x] 6.4 Full backend suite green (108 Application + 119 Api = 227 passed); `openspec validate` passes.
