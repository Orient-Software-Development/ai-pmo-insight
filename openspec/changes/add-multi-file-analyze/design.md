## Context

The 9-agent pipeline was shipped in `add-analysis-agent-pipeline` (Phase 3) against a single `Upload`.
[`AnalyzeUpload.cs:44`](../../../source/AiPMOInsight.Application/Features/Findings/AnalyzeUpload.cs) hands one
`Upload` to [`AnalysisOrchestrator.RunAsync(Upload upload, …)`](../../../source/AiPMOInsight.Application/Features/Analysis/AnalysisOrchestrator.cs);
the orchestrator parses that one file via `DataCollectorSkill`, groups by `projectKey` inside the resulting
`CollectedData`, and fans out. Findings accumulate under a `projectKey` across successive analyses (append,
never overwrite), so **read-side** portfolio views already tolerate "one project, many analyze calls." The
problem is **write-side**: any rule that needs two categories together (e.g. assignments × absenteeism) can
only fire when both categories happen to co-exist in a single uploaded file — which is not the real Orbit
export shape.

The wireframe's category slots (Budget/BPI, Hours/Overview, Assignments, Absenteeism, RAID, Minutes) and the
PRD's ten input categories are the target user experience. Multi-file is the missing plumbing; the agents
themselves and their citation invariants stay put.

## Goals / Non-Goals

**Goals:**

- Upload N files in one HTTP call, analyze them as one run, produce one `RunId`, with findings citing the
  specific `uploadId` each derived from.
- Ship at least one deterministic cross-file finding end-to-end (Resource #6 assignments × absenteeism)
  as the tracer bullet that proves the whole path.
- Keep the existing single-file endpoints and their tests untouched — additive change only.
- Preserve the "no finding without a citation" invariant, unchanged. Every finding still names a file and
  a locator inside that file.

**Non-Goals:**

- Automatic file→category classification on the server. Users assign a file to a slot in the UI (or don't
  — the API doesn't enforce category labels).
- Zip-file upload. Excluded to keep the parser surface small; a single zip pretending to be multiple files
  is a follow-up change if there's demand.
- LLM routing or provider-selection changes. Orthogonal to file count.
- New agent framework or orchestrator shape. `AnalyzeProjectAsync` in the orchestrator already loops over
  distinct `projectKey`s inside one `CollectedData`; that loop stays.
- Retroactive cross-file signals for #7/#8/#9. The trust-layer agents already see the merged findings and
  need no shape change.

## Decisions

### 1. New endpoints, not "widen the existing ones"

**Choice:**
- `POST /api/ingest/uploads` — new batch endpoint, `multipart/form-data` with N `files`. Returns
  `{ uploads: [ { uploadId, fileName, error? } ] }`, one entry per file in the batch.
- `POST /api/analyze` — new multi-uploadId endpoint, body `{ "uploadIds": ["…", "…"] }`. Returns the same
  `AnalyzeUpload.Result` shape (`{ runId, findings: […] }`).
- Existing `POST /api/ingest/upload` (singular) and `POST /api/analyze/{uploadId}` remain, unchanged.

**Alternatives considered:**
- **Widen `POST /api/ingest/upload` to accept N files.** Rejected: breaks the single-file endpoint's
  `IFormFile? file` binding, forces every existing test through a new code path, and blurs the API
  contract (is `file` singular or plural?).
- **One `POST /api/analyze` that accepts EITHER a single `uploadId` or an array.** Rejected: same
  ambiguity, and `MapPost("/{uploadId:guid}")` is a distinct route from `MapPost("")` — cleaner to keep
  them separate.
- **Deprecate the singular endpoints immediately.** Rejected: the existing UI + integration tests use
  them; a follow-up change can retire them once the multi-file UI is in production. Keep both for now.

### 2. Threading `uploadId` through `SourceRef`

**Choice:** Extend `SourceRef` (in
[`TypedRecords.cs`](../../../source/AiPMOInsight.Application/Features/Analysis/Model/TypedRecords.cs)) to
carry the originating `Guid UploadId` alongside its existing `Locator` / excerpts. `SourceRef.ToCitation()`
stops taking `uploadId` as a parameter and uses the field. Every parser call site (`UploadParser`) is
already single-file, so it already knows the `uploadId` — it just needs to stamp it onto every `SourceRef`
it emits.

**Rationale:** `SourceRef` is the single carrier of provenance; adding `uploadId` here means every
downstream `Finding.Create` call automatically gets the right one, without threading a per-call parameter
through the orchestrator. Zero risk of the wrong `uploadId` ending up on a finding — the field travels
with the record.

**Alternatives considered:**
- **Per-record dictionary lookup at citation time** (`Dictionary<recordRef, uploadId>` in the orchestrator).
  Rejected: fragile, requires stable record identity, and re-introduces the "which upload does this record
  belong to" question at every finding site. `SourceRef` already answers it.
- **A wrapper `CollectedData` variant that groups by `uploadId`.** Rejected: agents currently operate over
  flat `IReadOnlyList<Record>`s; grouping would force every agent to iterate the outer dimension. The
  merge is deliberately transparent to agents.

### 3. Merge strategy: transparent union, no dedup

**Choice:** `AnalysisOrchestrator.RunAsync(IReadOnlyList<Upload> uploads, …)` — the new overload — calls
`DataCollectorSkill` for each upload in turn, then concatenates each category's records into one
`CollectedData`. No cross-file dedup at the merge layer. Agents that care about duplicates (Data Quality
#2 already flags inconsistent identities) handle it.

**Rationale:** the merge must not silently lose data. If a user uploads both a "Full RAID" file and an
"Updates to RAID" file, dedup at the merge layer risks discarding the more recent record. Let the
deterministic agents surface the duplication as a finding instead of hiding it.

**Alternatives considered:**
- **Category-aware merge with priority order.** Rejected: no reliable server-side signal about which file
  is authoritative for a category, and user-supplied category hints are out of scope for this change.
- **Structural dedup by hashing records.** Rejected: two structurally identical records from different
  files are valid data (e.g. same milestone listed in both the timeline export and a status summary).

### 4. Cross-file rule tracer bullet: assignments × absenteeism in Resource #6

**Choice:** Ship one new deterministic rule in `ResourceSkill` (agent #6) that fires when the merged
`CollectedData` contains BOTH assignment records and — this is new — absenteeism records for the same
employee on the same project. It emits a `Kind = Analysis`, `ProducingAgent = "Resource"` finding
citing the assignment record, with the absenteeism date range in `StructuredExcerpt`. The absenteeism
category needs a new record type (`AbsenceRecord` — Person, Start, End, `SourceRef`) and matching parsing
inside `UploadParser` (a new `.xlsx` shape).

**Rationale:** this is the *concrete* signal the wireframe advertises. Delivering it end-to-end (parser →
records → merged `CollectedData` → agent rule → cited finding → read view) proves every layer of the
new plumbing works. Additional cross-file rules can layer on afterwards without further architectural
changes.

**Non-Goal:** enumerating every cross-file rule the product might eventually want. Out of scope for this
proposal; each future rule is an incremental, contained change to the appropriate deterministic agent.

### 5. `Upload → many` on the persistence model: no schema change

**Choice:** The `AnalysisRun` domain object currently ties one run to one `uploadId`. Widen it (or a new
join collection) to hold `IReadOnlyList<Guid> UploadIds` — but keep the singular `uploadId` field on
`Finding.Citation` (each finding still cites exactly one file). Persistence-wise this is either a new
`analysis_run_uploads` join table, or a JSON column on `analysis_runs`; **decision deferred to
implementation** because #23 already established the migration pattern (see the OpenSpec archive of
`add-analysis-agent-pipeline`) and either shape satisfies the requirements. Task list flags this as a
design detail.

**Rationale:** the citation invariant stays crystal — each finding cites one file. The run just records
which set of files contributed. Foreign-key discipline (upload deletion / retention) is unchanged.

## Risks / Trade-offs

- **[R1] `uploadId` field on `SourceRef` breaks pre-existing test fixtures / seeded citations.** Every
  `SourceRef` factory call site needs the new field. → Land the field with a default of
  `Guid.Empty` for compilation transition, but assert non-empty at `SourceRef.ToCitation()` — turning
  a compile-hole into a runtime test failure. Remove the default in a cleanup pass once every call site
  is migrated.
- **[R2] Batch endpoint's per-file error shape is a new API surface.** UX must not lose which file failed.
  → Response includes per-file `{ uploadId?, fileName, error? }` entries; front-end shows the error next
  to the slot. Documented in the spec; test covers the mixed success/failure case.
- **[R3] Merge order sensitivity — do agents see records in a stable order?** Some agents may implicitly
  rely on ordering (e.g. "first record wins" in a tie-break). → Merge in the order `uploads` was passed
  to `RunAsync`; the multi-file endpoint preserves the request's `uploadIds` order. Add a test asserting
  a deterministic order across two runs over the same set of uploads.
- **[R4] React drag-and-drop UX complexity.** The wireframe's six labelled slots is more UI than we have
  today. → Land the API + tracer-bullet rule first (§1–§4 of the tasks); the React work is §5 and can
  ship as its own PR if the backend lands green first.
- **[R5] The tracer-bullet absence-record parsing bloats the parser.** → `UploadParser` gains one new
  `.xlsx` shape (Absenteeism); existing shapes untouched. Keep the parser code additive.

## Migration Plan

1. Land §1–§4 as one PR (new endpoints, orchestrator overload, merge, tracer-bullet rule). Existing UI
   + tests continue to work because the singular endpoints stay untouched.
2. Follow-up PR ships §5 (drag-and-drop React UI) once §1–§4 is on `main`.
3. When the multi-file UI is in production, a future change deletes the singular endpoints. Not this
   change.

Rollback: the multi-file endpoints are additive — reverting the PR restores the pre-change API surface
exactly.

## Open Questions

- Should `AnalysisRun` gain an owning `projectKey`? Currently multiple `projectKey`s can be produced from
  one upload (via `AnalyzeProjectAsync` looping inside the orchestrator). Multi-file inherits that. Not
  changing here; a future "same-project precondition on the batch analyze endpoint" is a possible
  follow-up refinement.
- Do we need a `dry-run` mode on multi-file analyze to preview which cross-file rules WOULD fire before
  persisting findings? Deferred — no user-story yet.
- **Spreadsheet shape + sheet→category mapping is unresolved and affects this change's core assumption.**
  This design assumes **one workbook per category** (multiple `.xlsx` files, one per slot) and merges N
  parsed outputs. But the client may instead export **one workbook with multiple named tabs** (Budget,
  Assignments, Absenteeism, RAID as separate sheets). If so, the "merge N files" model partly inverts
  into "split one file's tabs into categories," and the batch-upload UX changes (one file, not six). Also
  unresolved: how the parser maps a sheet/tab to a category — exact sheet name, position, or header
  inspection — given names may be localized (Nordic client) and free-typed by PMs. Tracked as
  `docs/gap-project.md` §2.12 (design) and §3.11 (client kick-off decision); depends on the real Orbit
  export shape (gap §1.2, §3.7). **Confirm the convention before implementing §2 (the merge) and §5.3
  (the Absenteeism parser),** or the parser may silently read the wrong sheet and drop categories.
