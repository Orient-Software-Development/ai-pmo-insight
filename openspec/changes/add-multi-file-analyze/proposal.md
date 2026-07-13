## Why

The 9-agent pipeline currently sees at most one uploaded file per analysis run — `POST /api/ingest/upload` takes a single `IFormFile` and `POST /api/analyze/{uploadId}` runs the orchestrator over exactly one `Upload`. Real PMO usage exports project data as **separate files per category** (Budget, Hours/status, Resource assignments, Absenteeism, RAID, Minutes), and the wireframe in `docs/inputs/pmo-wireframe.html` shows a drag-and-drop with those exact six slots. Two consequences follow immediately: (1) the wireframe's own example cross-file signal — *"Assignments × absenteeism cross-linked by employee → surfaces key-person / overlap risk"* — cannot be produced today, because assignments and absenteeism live in separate files that never meet inside one Data Collector run; and (2) each single-file analyze appends findings under a shared `projectKey`, but every agent still only sees one file's data, so downstream rules that need multiple categories together are silently muted.

The PRD (`docs/prds/poc-ai-pmo-insight.md`) independently lists ~10 input categories for the POC scope. Both artefacts — the authoritative PRD and the draft wireframe (see `[[project-pmo-wireframe-draft]]`) — point at multi-file being intended scope, not an aspirational extension. This change closes that gap.

## What Changes

- **New batch ingest endpoint** — accept multiple files in one request and return per-file `uploadId`s (existing single-file endpoint remains untouched for back-compat with the current UI and integration tests).
- **New multi-file analyze endpoint** — accept a set of `uploadId`s belonging to the same project; the orchestrator merges each Data Collector output into ONE `CollectedData` before agent fan-out, so #2–#6 see the union of all inputs (existing single-uploadId endpoint remains).
- **Per-record upload provenance preserved through the merge** — every parsed record already carries a `SourceRef`; the merge additionally preserves which `uploadId` a record came from, so cited findings resolve back to the specific file/sheet/row and not just "one of the uploads".
- **Tracer-bullet cross-file rule** — Resource (#6) gains one new deterministic finding that fires only when assignments **and** absenteeism data are present in the same run (assignments × absenteeism cross-link by employee → key-person / overlap risk). Proves the whole merge path end-to-end.
- **React drag-and-drop upload UI** — the current single-input replaces with the wireframe's category-labelled multi-slot drop zone; users upload all their project files at once, then trigger a single analyze.
- **No changes to LLM routing** — this change is orthogonal to per-agent provider selection (already shipped in `add-per-agent-llm-routing`).
- **No changes to the citation / persistence model** — findings still carry a `Citation(uploadId, locator, …)`; multi-file just means findings within one run may cite different `uploadId`s.

Non-breaking: every existing endpoint and integration test continues to work. Multi-file is an additive capability.

## Capabilities

### New Capabilities

- `multi-file-ingest`: The multi-file upload contract, the multi-uploadId analyze endpoint, and the guarantees around per-file provenance preservation through the merge into `CollectedData`. Owns the batch endpoint semantics, the "same-project" precondition, and the failure modes for mismatched or missing uploads.

### Modified Capabilities

- `analysis-pipeline`: The Data Collector requirement widens from "parses an upload" to "parses one or more uploads, merging their records into a single `CollectedData` while preserving each record's source `uploadId`". The orchestrator requirement widens correspondingly from "one `Upload`" to "one or more `Upload`s". A new requirement is added for cross-file signals — deterministic agents may produce findings that require records from more than one input file, and those findings SHALL only fire when the required categories are all present in the same run.

## Impact

- **Code:**
  - `source/AiPMOInsight.Api/Endpoints/IngestEndpoints.cs` — add batch endpoint alongside the current single-file one.
  - `source/AiPMOInsight.Api/Endpoints/FindingsEndpoints.cs` — add multi-uploadId analyze endpoint alongside the single-id one.
  - `source/AiPMOInsight.Application/Features/Ingest/UploadFixture.cs` — new sibling command that stores N files in one operation (or reuse existing command N times inside a wrapper — decision in design.md).
  - `source/AiPMOInsight.Application/Features/Findings/AnalyzeUpload.cs` — new sibling handler taking `IReadOnlyList<Guid> UploadIds`.
  - `source/AiPMOInsight.Application/Features/Analysis/AnalysisOrchestrator.cs` — `RunAsync` gains an overload / new signature accepting multiple `Upload`s.
  - `source/AiPMOInsight.Application/Features/Analysis/Agents/DataCollectorSkill.cs` + `IUploadParser` — merge N parsed outputs, preserving per-record `uploadId`.
  - `source/AiPMOInsight.Application/Features/Analysis/Model/TypedRecords.cs` — `SourceRef` (or a new sibling type) carries the originating `uploadId` for post-merge citation resolution.
  - `source/AiPMOInsight.Application/Features/Analysis/Agents/ResourceSkill.cs` — new cross-file rule (assignments × absenteeism).
- **UI:** `source/AiPMOInsight.Api/ClientApp/src/` — new drag-and-drop upload component matching the wireframe's category slots; the existing single-file input can be kept as a fallback or replaced entirely (decision in design.md).
- **Tests:** new integration tests for the batch endpoints and the multi-file → merged-analysis path; a targeted test for the new cross-file rule; existing single-file tests must still pass.
- **Dependencies:** none added.
- **Docs:** brief mention of the multi-file flow in `CLAUDE.md` (under Ingest / analysis) once it lands; `docs/roadmap.md` gains a row for this phase.
- **Out of scope (deferred):** automated file→category classification (server-side inference of which file is budget vs. absenteeism), zip-file upload, and any change to LLM routing / agent providers.
