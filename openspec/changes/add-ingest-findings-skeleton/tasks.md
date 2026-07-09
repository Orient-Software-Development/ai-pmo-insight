## 1. Domain

- [ ] 1.1 Add `Citation` value object (`UploadId`, `Locator`) in `AiPMOInsight.Domain`, with guards rejecting empty `UploadId`/`Locator`
- [ ] 1.2 Add `Finding` aggregate (`Id`, `ProjectKey`, `Summary`, `Citation`, `CreatedAt`) with a `Create(...)` factory that requires a non-null `Citation`
- [ ] 1.3 Add `Upload` domain type (`Id`, `FileName`, `Content` bytes, `UploadedAt`) or confirm it lives only at the persistence layer

## 2. Persistence (Infrastructure)

- [ ] 2.1 Add `Upload` and `Finding` DbSets to `AppDbContext`
- [ ] 2.2 Add snake_case EF configurations for `Upload` and `Finding` (Citation mapped as owned type / columns)
- [ ] 2.3 Add EF migration covering both tables (`dotnet ef migrations add AddUploadsAndFindings --project source/AiPMOInsight.Infrastructure --startup-project source/AiPMOInsight.Api`); commit generated files
- [ ] 2.4 Confirm Development auto-migrate via `DbInitializer.MigrateAndSeedAsync` picks up the new tables

## 3. Ingest slice (`orbit-ingest`)

- [ ] 3.1 Define `IUploadRepository` port in Application (`AddAsync`, `GetAsync(uploadId)`)
- [ ] 3.2 Implement `EfUploadRepository` in Infrastructure over `AppDbContext`
- [ ] 3.3 Add `UploadFixture` Application command + handler (store raw bytes, return `uploadId` + file name); reject empty content
- [ ] 3.4 Add `POST /api/ingest/upload` endpoint (multipart), `.RequireAuthorization()`, returns `201` with `uploadId`

## 4. Findings slice (`project-findings`)

- [ ] 4.1 Define `IFindingRepository` port in Application (`AddAsync`, `GetByProjectKeyAsync(projectKey)`)
- [ ] 4.2 Implement `EfFindingRepository` in Infrastructure over `AppDbContext`
- [ ] 4.3 Add `AnalyzeUpload` Application command + handler: resolve upload, emit one hard-coded `Finding` with a citation to that upload, persist; return `404` semantics when upload missing
- [ ] 4.4 Add `GetProjectFindings` Application query + handler returning findings (with citations) for a `projectKey`
- [ ] 4.5 Add `POST /api/analyze/{uploadId}` endpoint, `.RequireAuthorization()`, synchronous
- [ ] 4.6 Add `GET /api/projects/{projectKey}` endpoint, `.RequireAuthorization()`, returns `200` with findings (empty list when none)

## 5. Fixture

- [ ] 5.1 Create a dummy Orbit-shaped fixture under the repo (structured project rows modeled on Orbit export columns/XSD + one unstructured meeting-minutes blob)
- [ ] 5.2 Reference the fixture from tests as the golden-file input

## 6. Client (minimal React view)

- [ ] 6.1 Add a read-only page/component in `ClientApp/src` that calls `GET /api/projects/{projectKey}` and lists findings with their citation
- [ ] 6.2 Wire the view into the SPA routing/nav

## 7. Tests

- [ ] 7.1 Domain unit test: `Finding.Create` rejects a null/empty citation
- [ ] 7.2 Integration test via `TestWebAppFactory`: upload fixture → analyze → `GET /api/projects/{projectKey}` returns the cited finding
- [ ] 7.3 Integration test: `analyze` on unknown `uploadId` returns `404`; unauthenticated calls return `401`
- [ ] 7.4 Integration test: `GET /api/projects/{unknownKey}` returns `200` with an empty list

## 8. Verify

- [ ] 8.1 Run the app, exercise upload → analyze → read end-to-end (API + React view), confirm the finding shows its citation
- [ ] 8.2 Run `openspec validate --change add-ingest-findings-skeleton` and the full test suite
