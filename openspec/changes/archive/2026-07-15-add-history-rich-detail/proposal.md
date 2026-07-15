## Why

`add-upload-history` shipped the two read endpoints (`GET /api/uploads`, `GET /api/uploads/{id}/findings`)
and a **minimal** `History.jsx` that lists uploads and renders the four sections as bare tables. The
**audit surface** the PRD's trust story needs (US-9 "see what the AI said / how it was challenged /
reviewed"; US-10 "every score change traces to a cited finding") is not built — there is no master-detail
layout, no run-provenance header (run id, prompt hash), and no score-audit view. This change rebuilds
`/history` into that audit surface. Per issue #36 it is **presentation-only — no backend/API/finding-shape
change**: the four cited sections come straight from `GET /api/uploads/{id}/findings`, and the score audit
**reuses the existing `GET /api/projects/{key}/health`** (per project referenced in the run) rather than
adding a read. Where the wireframe's header/list fields exceed what those reads return, the view **flags
them, never fabricates** — the same boundary L1/L2/L3 hold.

## What Changes

- **New `history-rich-detail` capability** — the view-level requirements for a master-detail `/history`
  audit surface. No change to the `upload-history` endpoint requirements, agents, prompts, finding shape,
  or any API contract.
- **Master-detail rebuild of `History.jsx`** on the shared Phase 5 design system (`--rag-*`, `records`,
  `sev`, `eyebrow`, `block`): a sticky left list of uploads (newest first) + a right detail panel for the
  selected upload, replacing the bare `.grid` of tables.
- **Run-provenance detail header** — surfaces what makes a run auditable months later, **from data the
  findings response already carries**: the **run id** (`RunId`), the **prompt hash(es)** (`PromptVersion`,
  per LLM finding — the content hash that pins the exact prompt), and the run **date** (`CreatedAt`).
- **Four cited sections** (unchanged data, restyled): **Analysis** (findings + citations), **Narrative**
  (#7), **Challenge** (#8), **Review** (#9) — each with confidence + citation locator.
- **Fifth section — Score audit (US-10)** — reuses `GET /api/projects/{key}/health` for each **distinct
  project key** present in the selected run, rendering that project's `AppliedOverrides` (rule, floor,
  reason, **cited** finding locator) and its RAG bucket. This needs **no backend change**.
- **Read-only, preserved** — no re-analyze, delete, edit, search, or pagination (the moment any appears the
  constraint has slipped).
- **Presentation-only boundary — flag, never fabricate.** The reads do not return everything the wireframe
  lists. Provenance of every panel is explicit:

  | Wireframe field | Backed by the current reads? |
  |---|---|
  | Master row: date, file name | ✅ `GetUploads` (`UploadedAt`, `FileName`) |
  | Detail: 4 sections (analysis/narrative/challenge/review) + citations | ✅ `GetUploadFindings` |
  | Detail header: run id, prompt hash, date | ✅ finding `RunId` / `PromptVersion` / `CreatedAt` |
  | Score audit: overrides that fired, each cited | ✅ via `GET /api/projects/{key}/health` per referenced project (see caveat) |
  | Master row: **uploader** | ⛔ `Upload` has no `UserId` (shared workspace) → flag |
  | Master row: **project count**, **files summary** (multi-file) | ⛔ list item has no project data; one file per upload (batch grouping = `add-multi-file-analyze`, deferred) → flag |
  | Master row / header: **live status pill** (Running / Failed) | ⛔ analysis is synchronous; no run-status entity — only coarse *Analyzed* (findings present) vs *Not analyzed* (empty) is derivable → flag Running/Failed |
  | Detail header: **LLM model** | ⛔ `FindingView` carries `PromptVersion` but no model id → flag |
  | Score audit as a strict **per-run historical** audit | ⚠ the health endpoint always scores the project's **latest** run; it is exact when the upload's run *is* the project's latest, but shows the *current* audit for a superseded run — labelled as such; a per-run historical score read is a follow-on |

Not in scope (roadmap follow-ons): any **backend read** to back uploader / LLM model / project count /
multi-file grouping / live per-agent status (US-9 live progress) / a strict per-run historical score audit;
multi-file batch grouping (`add-multi-file-analyze`). This change adds no endpoint and no finding field.

## Capabilities

### New Capabilities

- `history-rich-detail`: The `/history` master-detail audit view. Owns the master list + detail layout, the
  run-provenance header (run id, prompt hash, date), the four cited sections, the score-audit section that
  reuses `GET /api/projects/{key}/health` per referenced project, the read-only constraint, and the
  presentation-only boundary (flag uploader / LLM model / project count / multi-file / live status / strict
  per-run audit — never fabricate). No backend behaviour is introduced.

### Modified Capabilities

<!-- None. The upload-history endpoints (List uploads, Read an upload's latest analysis) are reused
     verbatim; their requirements do not change. The health endpoint is reused as-is. -->

## Impact

- **Code:** rebuild `source/AiPMOInsight.Api/ClientApp/src/components/History.jsx` (master-detail layout,
  provenance header, four sections, score-audit section) on the shared `styles.scss` design system; any
  response→viewmodel mapping (status derivation, distinct project keys, prompt-hash set) as a pure helper
  (mirrors `health.js` / `dataQuality.js`). Reuse `bucketColour` and the `healthState` mapping already in
  `health.js` for the score-audit section.
- **API / Persistence:** none — no new endpoint, no schema change, no migration. Reuses
  `GET /api/uploads`, `GET /api/uploads/{id}/findings`, and `GET /api/projects/{key}/health`.
- **Tests:** the repo has **no JS test harness**; the existing backend integration coverage
  (`UploadHistoryEndpointsTests`, health-endpoint tests) already locks the data path this view reads. Any
  new pure helper is node-checkable; the render is `/verify`-checked in the running app (as L1/L2/L3).
- **Docs:** roadmap Phase 5 History detail note; a `## History (rich detail)` note in `CLAUDE.md`.
- **Deferred:** backend reads for uploader / LLM model / project count / multi-file / live status / strict
  per-run score audit.
