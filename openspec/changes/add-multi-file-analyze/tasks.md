## 1. Provenance on `SourceRef` (TDD)

- [ ] 1.1 Extend the `SourceRef` tests (or add new ones under `tests/AiPMOInsight.Application.Tests/Analysis/`) that pin: `SourceRef` carries a `Guid UploadId`; `ToCitation()` returns a citation whose `UploadId` equals that field; `ToCitation()` no longer takes an `uploadId` parameter; `ToCitation()` throws when `UploadId` is `Guid.Empty`.
- [ ] 1.2 Update `source/AiPMOInsight.Application/Features/Analysis/Model/TypedRecords.cs`: `SourceRef` gains `Guid UploadId`; `ToCitation()` uses it. Land with `UploadId` init-default `Guid.Empty` for transitional compile; add a runtime assertion in `ToCitation()` that rejects `Guid.Empty`.
- [ ] 1.3 **Thread `uploadId` into the parser** (it is not there today — `UploadPayload` is `(FileName, Content)` and `IUploadParser.Parse(fileName, content)` has no id). Add `Guid UploadId` to `UploadPayload`; pass it through `IUploadParser.Parse` and `DataCollectorSkill` into the **format** parsers.
- [ ] 1.4 Update every `SourceRef` construction site — in `source/AiPMOInsight.Infrastructure/Analysis/Parsing/ExcelProjectParser.cs`, `OrbitXmlParser.cs`, and `DocxMinutesParser.cs` (NOT `UploadParser.cs`, which only dispatches), plus `tests/**/*` fixtures — to stamp the threaded `uploadId`.
- [ ] 1.5 **Synthesis findings get the run's primary upload** (design §2). The four run-level `ToCitation` sites — `NarrativeSkill`, `ChallengeSkill`, `ReviewSkill`, and the `RiskAndIssue` synthesis site — build an inline `SourceRef` with no id and today pass `slice.Run.UploadId`. Give their `SourceRef` the run's **primary** `uploadId` (first in request order) so the `Guid.Empty` assertion passes and multi-file runs still cite exactly one file. Add a test: a multi-file run's synthesis findings cite the first `uploadId` of the request.
- [ ] 1.6 Delete the transitional default in step 1.2 once the tree compiles clean; every call site now passes an explicit `uploadId`.
- [ ] 1.7 Green: existing analysis + orchestrator tests still pass; new SourceRef + synthesis-citation tests pass.

> ⚠️ **Open decision blocks §2 and §5.3** — the spreadsheet shape (one workbook per category vs. one
> workbook with multiple named tabs) and the sheet→category mapping rule are **not yet decided**. See
> design.md Open Questions + `docs/kickoff-questions.md` §A1. Confirm the convention with the client
> before implementing the merge (§2) and the Absenteeism parser (§5.3).

## 2. Data Collector merge (TDD)

- [ ] 2.1 Add `tests/AiPMOInsight.Application.Tests/Analysis/DataCollectorMergeTests.cs` (red): parsing two uploads produces one `CollectedData` whose category lists are the union of both files' records; each record's `SourceRef.UploadId` matches the file it came from; no cross-file dedup.
- [ ] 2.2 Extend `IUploadParser` or `DataCollectorSkill` with a merge helper that takes `IEnumerable<(Guid uploadId, string fileName, byte[] content)>` and returns one `CollectedData`. Single-upload path stays as an overload that delegates to the merge helper with N=1.
- [ ] 2.3 Green: merge tests pass.

## 3. Multi-file batch ingest endpoint (TDD)

- [ ] 3.1 Add `tests/AiPMOInsight.Api.Tests/Ingest/BatchUploadEndpointTests.cs` (red): `POST /api/ingest/uploads` with six multipart files returns 200 with six `{uploadId, fileName}` entries; every upload is retrievable by its id; one empty file in a batch produces a per-file error but does not fail the others; the singular `POST /api/ingest/upload` still works unchanged.
- [ ] 3.2 Add `UploadFixtures.BatchCommand` in `source/AiPMOInsight.Application/Features/Ingest/` — takes `IReadOnlyList<(string FileName, byte[] Content)>`, returns `IReadOnlyList<{UploadId, FileName, Error?}>`. Reuses the existing single-file store logic per file so back-compat is exact.
- [ ] 3.3 Add `MapPost("/uploads", …)` alongside the existing `MapPost("/upload", …)` in `IngestEndpoints.cs`.
- [ ] 3.4 Green: batch + single-file ingest tests all pass.

## 4. Multi-file analyze endpoint + orchestrator overload (TDD)

- [ ] 4.1 Add `tests/AiPMOInsight.Api.Tests/Analysis/MultiFileAnalyzeEndpointTests.cs` (red): `POST /api/analyze` with `{"uploadIds":[a,b,c]}` returns one `runId` and findings whose citations reference `a`, `b`, or `c` appropriately; empty array → 400; unknown id in the set → 404 with no run persisted; singular `POST /api/analyze/{uploadId}` still works unchanged.
- [ ] 4.2 Extend `AnalysisOrchestrator` with `RunAsync(IReadOnlyList<Upload> uploads, CancellationToken ct)` that: preflights (non-empty), calls the merged Data Collector, then delegates to the existing per-project loop. Fresh `AnalysisRun` id; the run records all participating `uploadId`s (design §5 — pick JSON column or join table).
- [ ] 4.3 Add `AnalyzeUploads.Command(IReadOnlyList<Guid> UploadIds)` in `source/AiPMOInsight.Application/Features/Findings/` — resolves each upload, returns 404-shape if any missing, otherwise drives the new orchestrator overload.
- [ ] 4.4 Add `MapPost("", …)` on `/api/analyze` alongside `MapPost("/{uploadId:guid}", …)` in `FindingsEndpoints.cs`.
- [ ] 4.5 Persistence tweak for `AnalysisRun.UploadIds` — new EF migration if a join table is chosen, or JSON column mapping otherwise. Include the migration file + `AppDbContextModelSnapshot` update.
- [ ] 4.6 Green: multi-file analyze tests + all pre-existing analyze tests pass.

## 5. Tracer-bullet cross-file rule: assignments × absenteeism in Resource #6 (TDD)

- [ ] 5.1 Add `tests/AiPMOInsight.Application.Tests/Analysis/ResourceCrossFileTests.cs` (red): when merged `CollectedData` contains an `AssignmentRecord` and an `AbsenceRecord` for the same `Person` on the same `ProjectKey`, `ResourceSkill` emits a cited "key-person / overlap risk" finding; when only assignments are present, the cross-file finding does NOT fire and no synthetic warning appears; when the same input runs twice, the finding is reproducible.
- [ ] 5.2 Add `AbsenceRecord` (Person, ProjectKey, Start, End, `SourceRef`) to `TypedRecords.cs`, and an `IReadOnlyList<AbsenceRecord> Absences` field on `CollectedData` (with `Empty` seeded to `[]`).
- [ ] 5.3 Extend `UploadParser` to recognise the Absenteeism `.xlsx` shape (columns: `person`, `project_key`, `start_date`, `end_date`) and produce `AbsenceRecord`s stamped with the file's `uploadId`.
- [ ] 5.4 Update the merge helper (§2) to include `Absences` in the union.
- [ ] 5.5 Add the cross-file rule to `ResourceSkill.ExecuteAsync`: for each project, if both `Assignments` and `Absences` are non-empty, join by `Person` and emit one finding per overlap. Cite the assignment record; include the absence date range in `StructuredExcerpt`.
- [ ] 5.6 Add a synthetic Absenteeism fixture under `tests/AiPMOInsight.Api.Tests/Fixtures/` for the integration test.
- [ ] 5.7 Add an integration test that uploads the Assignments + Absenteeism fixtures via the batch endpoint, calls the multi-file analyze endpoint, and asserts the cross-file finding lands in the response.
- [ ] 5.8 Green: cross-file rule tests pass; existing Resource #6 tests still pass.

## 6. React drag-and-drop multi-file UI

- [ ] 6.1 Read the wireframe (`docs/inputs/pmo-wireframe.html`) upload section — six category-labelled slots.
- [ ] 6.2 Add a `MultiFileUpload.jsx` component under `source/AiPMOInsight.Api/ClientApp/src/components/` — six slots (Budget, Hours, Assignments, Absenteeism, RAID, Minutes), drag-and-drop targets, filename display, per-slot upload state.
- [ ] 6.3 Wire the component to `POST /api/ingest/uploads` (batch) and `POST /api/analyze` (multi-file); on success, navigate to the project findings view showing findings from all files.
- [ ] 6.4 Keep the existing single-file upload input available (e.g. under an "Advanced / single file" toggle) so the pre-change UX still works during the transition.
- [ ] 6.5 Verify by hand: drop six fixture files, click Analyze, confirm the cross-file finding renders in the project view.

## 7. Docs + validate

- [ ] 7.1 Update `CLAUDE.md` — one paragraph under the ingest / analysis section describing the multi-file flow (batch endpoint, merged `CollectedData`, per-record `uploadId` on `SourceRef`, cross-file rules).
- [ ] 7.2 Update `docs/roadmap.md` — add a row for "multi-file analyze" under the appropriate phase; note the shipped tracer-bullet rule and that the wireframe's other cross-file signals become incremental adds.
- [ ] 7.3 Run `dotnet test` end-to-end. Confirm the singular endpoints' tests + the new multi-file tests all pass together.
- [ ] 7.4 Run `openspec validate add-multi-file-analyze --strict`; fix any reported issue.
- [ ] 7.5 Summarise the change in the PR description: what's new, what's back-compat, and that the tracer-bullet rule proves the whole path (link this OpenSpec change).
