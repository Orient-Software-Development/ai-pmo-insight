## 1. Repository reads (Application ports + EF adapters)

- [ ] 1.1 Add `Task<IReadOnlyList<Upload>> ListAsync(CancellationToken)` to `IUploadRepository`.
- [ ] 1.2 (RED) Write a repository test asserting `EfUploadRepository.ListAsync` returns all uploads ordered by `UploadedAt` descending.
- [ ] 1.3 (GREEN) Implement `EfUploadRepository.ListAsync` — `AsNoTracking`, `OrderByDescending(u => u.UploadedAt)`.
- [ ] 1.4 Add `Task<IReadOnlyList<Finding>> GetByUploadIdAsync(Guid uploadId, CancellationToken)` to `IFindingRepository`.
- [ ] 1.5 (RED) Write a repository test asserting `GetByUploadIdAsync` returns only findings whose `Citation.UploadId` matches, ordered by `CreatedAt`.
- [ ] 1.6 (GREEN) Implement `EfFindingRepository.GetByUploadIdAsync` — `Where(f => f.Citation.UploadId == uploadId)`, `AsNoTracking`, `OrderBy(f => f.CreatedAt)`.

## 2. Index migration (design D4)

- [ ] 2.1 Add `HasIndex(c => c.UploadId)` inside the owned-Citation block in `FindingConfiguration`.
- [ ] 2.2 Generate the migration: `dotnet ef migrations add AddCitationUploadIdIndex --project source/AiPMOInsight.Infrastructure --startup-project source/AiPMOInsight.Api`; commit the generated files.
- [ ] 2.3 Verify the migration is index-only (no column/table changes) by reading the generated `Up`/`Down`.

## 3. List-uploads query slice + endpoint

- [ ] 3.1 (RED) Integration test: `GET /api/uploads` returns 200 with uploads newest-first; entries contain id/fileName/uploadedAt and NOT content.
- [ ] 3.2 (RED) Integration test: `GET /api/uploads` returns 200 empty list when no uploads exist.
- [ ] 3.3 (RED) Integration test: `GET /api/uploads` returns 401 when unauthenticated.
- [ ] 3.4 (GREEN) Add a `GetUploads` query slice (Query + `Result`/`UploadListItem` with id, fileName, uploadedAt) over `IUploadRepository.ListAsync`.
- [ ] 3.5 (GREEN) Add `UploadHistoryEndpoints` mapping `GET /api/uploads` under `.RequireAuthorization()`; register in `Program.cs`.

## 4. Upload-findings query slice + endpoint

- [ ] 4.1 (RED) Integration test: `GET /api/uploads/{id}/findings` returns 200 with the latest run's findings in four sections (analysis/narrative/challenge/review) for an analyzed upload.
- [ ] 4.2 (RED) Integration test: when an upload has two runs, only the latest run's findings are returned.
- [ ] 4.3 (RED) Integration test: a known upload with no findings returns 200 with all four sections empty.
- [ ] 4.4 (RED) Integration test: an unknown upload id returns 404.
- [ ] 4.5 (RED) Integration test: unauthenticated caller returns 401.
- [ ] 4.6 (GREEN) Add a `GetUploadFindings` query slice mirroring `GetProjectFindings` (same `Result`/`FindingView`/`CitationView` shape + latest-run selection), taking `IUploadRepository` (existence → null/404) and `IFindingRepository.GetByUploadIdAsync`.
- [ ] 4.7 (GREEN) Map `GET /api/uploads/{id:guid}/findings` in `UploadHistoryEndpoints` (`.RequireAuthorization()`); null result → 404.

## 5. React history page

- [ ] 5.1 Add a `History` page: left column lists uploads from `GET /api/uploads`; selecting one fetches `GET /api/uploads/{id}/findings` via `authFetch` and renders the four sections.
- [ ] 5.2 Add a `/history` route guarded by `RequireAuth` and a nav link to it.
- [ ] 5.3 Handle the empty states (no uploads; selected upload with no findings) and the loading state.

## 6. Verify + document

- [ ] 6.1 Run the full test suite (Application + Api) green; run `openspec validate add-upload-history`.
- [ ] 6.2 End-to-end check: upload a file, analyze it, open `/history`, confirm the upload appears and its findings render.
- [ ] 6.3 Add a brief note to `CLAUDE.md` (history read surface) and a row to `docs/roadmap.md`.
