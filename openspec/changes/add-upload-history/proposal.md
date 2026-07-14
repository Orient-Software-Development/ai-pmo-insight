## Why

Users can upload files and analyze them, but once they navigate away there is no way to see
what was uploaded before or revisit an analysis. Every upload and every analysis run is already
persisted (uploads in the `Uploads` table; findings carry `uploadId` + `runId` + `createdAt`), so
the history is sitting in the database with no read surface over it. This change adds that surface.

## What Changes

- **New read endpoint — list uploads.** `GET /api/uploads` returns every upload (id, file name,
  uploaded-at), newest first. Any authenticated user sees all uploads (shared-workspace model).
- **New read endpoint — an upload's findings.** `GET /api/uploads/{id}/findings` returns the
  findings from that upload's **latest** analysis run, in the same four-section shape the existing
  project view already uses (analysis / narrative / challenge / review). An upload that was never
  analyzed returns empty sections (200, not 404). An unknown upload id returns 404.
- **New React page — `/history`.** A sidebar list of uploads; selecting one shows that upload's
  findings. Reachable from the app nav. View-only.
- **Two new repository reads.** `IUploadRepository` gains a list method; `IFindingRepository` gains
  a "by upload id" query. Both are additive.

Non-breaking: no existing endpoint, contract, or table changes. History is purely additive read
capability.

### Explicitly out of scope (deferred)

- **Per-user filtering.** `Upload` has no `UserId`; everyone sees everything. If per-user isolation
  is needed later it requires a schema migration — tracked as future work, not this change.
- **Delete / edit / search / pagination** of history.
- **Re-analyze button and run-comparison / diff view.** History is view-only; re-analyzing the
  same upload is not a user workflow, so multiple runs per upload collapse to "show the latest".
- **Batch grouping of multi-file uploads into one history row.** The in-flight
  `add-multi-file-analyze` change will later let one analysis span several files; when it lands,
  revisit whether a history row should represent a file or a batch. Until then, one upload = one row.

## Capabilities

### New Capabilities

- `upload-history`: Read-only browsing of past uploads and their latest analysis. Owns the
  list-uploads contract, the upload-scoped findings read (latest-run selection, empty-vs-404
  semantics), and the shared-workspace visibility rule (any authenticated caller sees all uploads).

### Modified Capabilities

<!-- None. This change adds a new read surface; it does not change any existing spec requirement.
     The existing project-findings capability (latest-run-per-project read) is untouched. -->

## Impact

- **New code:**
  - `source/AiPMOInsight.Api/Endpoints/` — a new `UploadHistoryEndpoints` (or extend `IngestEndpoints`)
    mapping `GET /api/uploads` and `GET /api/uploads/{id}/findings`, both `.RequireAuthorization()`.
  - `source/AiPMOInsight.Application/Features/` — two query slices: list uploads, and
    get-findings-by-upload (latest run).
  - `source/AiPMOInsight.Api/ClientApp/src/` — a `History` page + nav entry, using the existing
    `authFetch` wrapper.
- **Modified code (additive):**
  - `IUploadRepository` + `EfUploadRepository` — add `ListAsync`.
  - `IFindingRepository` + its EF adapter — add `GetByUploadIdAsync`.
- **Data model:** no change. No migration, no new tables. `RunId` is derived from findings
  (`MAX(createdAt)` picks the latest run), matching the "history preserved for free" design already
  documented on `AnalysisRun`.
- **Tests:** integration tests for both endpoints (list ordering; latest-run selection;
  empty-sections vs 404); repository tests for the two new reads.
- **Docs:** brief mention in `CLAUDE.md` once landed; a row in `docs/roadmap.md`.
