## Context

Uploads and findings are already persisted; nothing reads them back as "history". Two current
access paths exist:

- `IUploadRepository`: `AddAsync`, `GetAsync(uploadId)` — no list.
- `IFindingRepository`: `AddAsync`, `AddRangeAsync`, `GetByProjectKeyAsync(projectKey)` — no
  by-upload read.

Findings link to an upload through the owned `Citation.UploadId` value (column `citation_upload_id`
on the `findings` table). A run's identity (`RunId`) lives only on the findings it produced — there
is no runs table — so "the latest run for an upload" is `findings WHERE citation_upload_id = @id`
grouped by `run_id`, picking the group with the newest `created_at`. This mirrors the existing
`GetProjectFindings` slice, which already selects the latest run per project key.

Indexes today: `project_key` and `run_id`. There is **no** index on `citation_upload_id`.

Auth: every existing read endpoint uses `.RequireAuthorization()`; there is no per-user data
scoping anywhere in the app (`Upload` has no `UserId`).

## Goals / Non-Goals

**Goals:**

- Two authenticated read endpoints: list uploads (newest first), and an upload's latest-run findings.
- A React `/history` page: upload list + drill-down to findings, reusing existing view shapes.
- Reuse the existing four-section findings view contract so the frontend rendering is shared.
- No schema change to columns; no new tables; no migration to data.

**Non-Goals:**

- Per-user filtering, delete/edit, search, pagination (deferred; see proposal).
- Re-analyze and run comparison (history is view-only).
- Multi-file batch grouping (revisit after `add-multi-file-analyze`).

## Decisions

### D1: Reuse the existing latest-run-per-scope pattern, keyed by upload instead of project

`GetProjectFindings` already computes "latest run" as `all.MaxBy(f => f.CreatedAt).RunId` then filters
to that run and partitions into four sections. The upload-scoped read is the same algorithm with a
different filter (`citation_upload_id = @id` instead of `project_key = @key`).

**Decision:** add a new query slice `GetUploadFindings` that mirrors `GetProjectFindings`'s
`Result` / `FindingView` / `CitationView` shape and latest-run logic. Do **not** try to generalize
the two into one parameterized slice yet — vertical-slice convention in this repo favours a second
explicit slice over premature abstraction; the shared shape is small.

*Alternative considered:* extend `GetProjectFindings` with an optional upload filter. Rejected —
overloads the project-view semantics and its 404-vs-empty contract differs (project view always
returns 200 empty for unknown keys; the upload view must 404 an unknown upload id).

### D2: 404 vs empty-sections — distinguish unknown upload from analyzed-with-no-findings

The upload read must 404 an id that does not exist, but return 200 with empty sections for a known
upload that simply has no findings (never analyzed, or analyzed to zero findings). Because findings
alone cannot tell "unknown upload" from "no findings", the handler MUST check upload existence first.

**Decision:** the `GetUploadFindings` handler takes `IUploadRepository` **and** `IFindingRepository`.
It calls `uploads.GetAsync(id)`; if null → return null → endpoint maps to 404. Otherwise query
findings by upload id and partition (possibly empty).

### D3: Add a repository read per port, keep them thin

- `IUploadRepository.ListAsync(ct)` → all uploads ordered `UploadedAt` descending, `AsNoTracking`.
- `IFindingRepository.GetByUploadIdAsync(uploadId, ct)` → `WHERE citation_upload_id = @id` ordered
  by `CreatedAt`, `AsNoTracking` (same shape as `GetByProjectKeyAsync`).

The findings filter is on the owned Citation member: `db.Findings.Where(f => f.Citation.UploadId == uploadId)`.
EF translates owned-property predicates to the mapped column.

### D4: Index `citation_upload_id`

The new query filters on a currently-unindexed column. For correctness this is not required, but the
by-upload read is now a first-class access path (same status as by-project and by-run, both indexed).

**Decision:** add `builder.HasIndex(c => c.UploadId)` inside the owned-Citation configuration and
generate an EF migration for the index. This is an index-only migration — no data change, no column
change. It is the one migration this change ships. (If the team prefers zero migrations for this
change, the index can be deferred to a follow-up; the query is correct without it, just unindexed.)

### D5: Endpoint placement and shape

Map both under `/api/uploads` in a new `UploadHistoryEndpoints` class (keeps ingest-write and
history-read concerns separate):

- `GET /api/uploads` → `200` list of `{ id, fileName, uploadedAt }`.
- `GET /api/uploads/{id:guid}/findings` → `200` four-section result, or `404`.

Both `.RequireAuthorization()`. Register the group in `Program.cs` alongside the other `Map*Endpoints`.

*Note:* the existing upload-write endpoint is `POST /api/ingest/upload`. The read lives under
`/api/uploads` (resource-oriented) rather than `/api/ingest/*`. Minor asymmetry, accepted — the read
surface is a different resource view than the ingest-write surface.

### D6: Frontend — a new page reusing `authFetch`

A `History` page component: left column lists uploads (from `GET /api/uploads`); selecting one
fetches `GET /api/uploads/{id}/findings` and renders the four sections. Use the existing `authFetch`
wrapper (sends cookies, silent refresh). Add a nav link and a route guarded by `RequireAuth`.

## Risks / Trade-offs

- **[Unbounded list growth]** `GET /api/uploads` returns every upload with no pagination. For a real
  product this grows without limit. → Accepted for this change; pagination is an explicit deferred
  non-goal. The list projects only id/name/timestamp (not content), so payload per row is tiny;
  revisit pagination before the table reaches thousands of rows.
- **[Shared visibility is a product decision, not a security accident]** Every authenticated user
  sees every upload. → Intentional (shared-workspace model). Documented in the spec as a requirement
  so it is a deliberate contract, not an oversight. Per-user isolation is a future schema change.
- **[Second latest-run implementation]** `GetUploadFindings` duplicates `GetProjectFindings`'s
  latest-run logic. → Accepted per D1; if a third scope appears, extract a shared helper then.
- **[Owned-property query translation]** Filtering on `f.Citation.UploadId` relies on EF translating
  the owned member to its column. → Low risk (standard EF owned-type behaviour); covered by an
  integration test that asserts the by-upload filter returns only that upload's findings.

## Migration Plan

1. Add the `citation_upload_id` index via `dotnet ef migrations add AddCitationUploadIdIndex`
   (D4). Index-only; safe to apply online for the POC-scale table. In Development the API
   auto-migrates on startup; in production it runs as the usual deploy step.
2. No data backfill. No column changes. Rollback = drop the index (down migration is generated).
3. If the team elects to defer the index (D4 alternative), this change ships with **zero**
   migrations.

## Open Questions

- **Index now or defer?** (D4) Recommend shipping the index with the change since by-upload becomes a
  primary access path. Flagged for reviewer sign-off because it is the only migration here.
