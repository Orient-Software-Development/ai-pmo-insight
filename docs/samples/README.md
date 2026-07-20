# Sample fixtures — dummy Orbit-shaped export

These are **dummy fixtures**, not real client data. They exercise the ingest → analyze →
cited-finding → read flow and double as golden-file inputs. The data uses **Orbit's real
terminology** where it could be confirmed (see *Field provenance* below).

The folder holds the same portfolio in **three forms**:

1. **Source CSV / Markdown** (`*.csv`, `meeting-minutes.md`) — human-readable, one file per input
   category. **Not directly uploadable** (see formats note below); these are the source of truth the
   `.xlsx`/`.docx` fixtures are generated from.
2. **Per-category workbooks** (`projects.xlsx`, `budget.xlsx`, …) — each CSV as a single-sheet
   `.xlsx` you can upload on its own.
3. **Consolidated workbook** (`orbit-sample.xlsx`) — every category as one tab in a single upload.

> **Formats the app parses:** the Data Collector (`UploadParser`) reads only **`.xlsx`/`.xlsm`**
> (Excel), **`.xml`** (Orbit), and **`.docx`** (minutes). A **`.csv` upload parses to nothing**
> (`CollectedData.Empty`) → zero findings, so the upload UI rejects `.csv`. A native CSV parser is
> deferred. Upload the `.xlsx`/`.docx` fixtures, not the CSVs.

## ⚠️ Which upload actually produces findings

The orchestrator derives project keys **only from the `Projects` data** (`AnalysisOrchestrator`). So:

| Upload | Parses? | Produces findings? |
|--------|:---:|---|
| **`orbit-sample.xlsx`** (consolidated) | ✅ | ✅ **Yes — full portfolio.** Recommended. |
| `projects.xlsx` | ✅ | ⚠️ Partial — project keys exist, but budget/milestone/resource cross-signals can't fire without those categories. |
| `budget.xlsx`, `resources.xlsx`, `raid.xlsx`, `timeline-milestones.xlsx` | ✅ | ❌ **No** — parses its category, but with no `Projects` rows the keys never match, so findings come back empty. |
| `scope-wbs.xlsx`, `time-used.xlsx`, `decisions.xlsx` | ⚠️ ignored | ❌ No — no record type / agent rule for these yet. |
| `meeting-minutes.docx` | ✅ | ✅ Yes — ORB-1002 minutes (LLM #4 path). |

> **Combining separate category files in one run** (e.g. `budget.xlsx` + `resources.xlsx` together)
> needs the multi-file merge, which is **not built yet** (`openspec/changes/add-multi-file-analyze`).
> Until then, use `orbit-sample.xlsx` for a full analysis.

## Files

**Uploadable — `.xlsx` (parsed by `ExcelProjectParser`, which reads sheets by name):**

| File | Sheet | PRD input category | Typed record | Parsed today? |
|------|-------|--------------------|--------------|:---:|
| `orbit-sample.xlsx` | all 8 below | whole portfolio | — | ✅ (5 of 8 sheets) |
| `projects.xlsx` | `Projects` | Project Identifier (+ % complete, last update) | `ProjectRecord` | ✅ |
| `timeline-milestones.xlsx` | `Milestones` | Project Timeline (baseline vs adjusted) | `MilestoneRecord` | ✅ |
| `budget.xlsx` | `Budget` | Project Budget (planned/forecast/actual) | `BudgetLineRecord` | ✅ |
| `resources.xlsx` | `Resources` | Project Resources (occupancy=allocation, group=role) | `AssignmentRecord` | ✅ |
| `raid.xlsx` | `RAID` | Risks + Issues merged (`Type`=`Risk`/`Issue`) | `RaidItemRecord` | ✅ |
| `scope-wbs.xlsx` | `ScopeWbs` | Project Scope (WBS) | — | ❌ staged |
| `time-used.xlsx` | `TimeUsed` | Project Time Used | — | ❌ staged |
| `decisions.xlsx` | `Decisions` | Project Decisions | — | ❌ staged |

**Uploadable — `.docx`:** `meeting-minutes.docx` (unstructured ORB-1002 minutes).

**Source (human-readable, not uploadable):** the matching `*.csv` files + `meeting-minutes.md`.

All rows share **`project_id`** (`ORB-1001` … `ORB-1006`). The staged categories are carried
verbatim (snake-case columns) until a change adds their record types + agent rules.

## ⚠️ Field provenance — read before writing the real parser

The fixtures are shaped from what is **publicly documented** on orbit.online. Orbit's literal
**XSD element names and GraphQL field names are gated behind an API key / developer portal** and
could not be retrieved. So:

- **Vocabulary — confirmed from orbit.online**: WBS, **phases**, **tasks/activities**,
  **milestones**, **checkpoints** (static/relative execution mode), **deliverables**,
  **dependencies**, **project type**, **customer**; **occupancy rate**, **base estimates** (planned)
  vs **registered hours** (actual), **competencies/certifications**, **professional groups**,
  **blocked days** (holiday/absence).
- **Inferred / not literally confirmed**: exact column spellings, RAG project status (the
  Green/Amber/Red bucketing is *our* PRD-defined scoring), and the whole of the **Budget** data —
  Orbit finance centers on invoicing/customers/cost, so planned/actual/forecast names here are a guess.
- **May not live in Orbit at all** — Risks, Issues, Decisions. Treat as *placeholders* until
  confirmed at kick-off.

**Before Phase 2 (real parser):** reconcile every column against one real (anonymized) Orbit
export or Orbit's published XSD. Do not code the parser against these guessed names.

Sources: [Orbit API & GraphQL](https://www.orbit.online/api-and-graphql/) ·
[Integration](https://www.orbit.online/overview/integration/) ·
[Project planning](https://www.orbit.online/project-planning-software/) ·
[Resource planning](https://www.orbit.online/resource-planning/)

## The dummy portfolio (6 projects)

| project_id | Name | Type | Baked-in signal (for the analysis layer to catch) |
|-----------|------|------|-----------|
| ORB-1001 | Nordic Retail Platform | Implementation | 40% complete but 70% budget burned — the budget-vs-progress flag |
| ORB-1002 | Customer Data Migration | Implementation | Over budget, forecast overrun >15%, **missed critical checkpoint**, unmitigated critical risk, overdue decision |
| ORB-1003 | AI Advisory Discovery | Advisory | Healthy (Green) baseline |
| ORB-1004 | Warehouse Automation Rollout | Implementation | Amber, vendor slippage |
| ORB-1005 | Payments Compliance Upgrade | Implementation | Forecast overrun approaching threshold |
| ORB-1006 | Data Governance Framework | AI PMO | Green |

Cross-cutting: **Anna Berg** is PM / allocated on **5 projects** (1001, 1002, 1004, 1005, 1006) →
PRD "resource on 5+ projects = Red" key-person risk — and has a **blocked-day** holiday (`TimeUsed`
+ minutes). Some risk/issue rows have `last_updated` >21 days before the PRD date (2026-07-09) to
exercise the "stale data" rule.

## How to upload one to test

Analysis is **not a stub** — the orchestrator runs the 9-agent pipeline and groups findings under
the `projectKey` parsed from the file. Upload `orbit-sample.xlsx` for a full run.

```bash
# 1. log in (dev admin is seeded) — saves auth cookies
curl -c cookies.txt -X POST "http://localhost:5081/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@localhost","password":"Admin123!$"}'

# 2. upload the consolidated workbook -> returns { uploadId, fileName }
curl -b cookies.txt -X POST "http://localhost:5081/api/ingest/upload" \
  -F "file=@docs/samples/orbit-sample.xlsx"

# 3. analyze the stored upload -> runs the pipeline, emits cited findings per project
curl -b cookies.txt -X POST "http://localhost:5081/api/analyze/<uploadId-from-step-2>"

# 4. read a project's findings (project keys come from the file: ORB-1001 .. ORB-1006)
curl -b cookies.txt "http://localhost:5081/api/projects/ORB-1002"
```

> On Windows PowerShell, `curl` is an alias for `Invoke-WebRequest`. Either call `curl.exe`
> explicitly (commands above work as-is) or use `Invoke-RestMethod`.

## Regenerating the fixtures

The `.xlsx` and `.docx` files are **derived artifacts** built from the CSVs/markdown with a one-shot
ClosedXML/OpenXml script (kept out of the repo). If the source data changes, rebuild them so the
sheet names/headers still match `ExcelProjectParser` (`Projects`/`Milestones`/`Budget`/`Resources`/
`RAID`) and the minutes `.docx` keeps a leading `Project: <key>` paragraph plus a `yyyy-MM-dd` date.
