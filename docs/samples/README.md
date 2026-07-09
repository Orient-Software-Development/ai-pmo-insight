# Sample fixtures — dummy Orbit-shaped export

These are **dummy fixtures**, not real client data. They test the ingest → analyze →
cited-finding → read walking skeleton (`openspec/changes/add-ingest-findings-skeleton`) and double
as golden-file inputs. Each file maps to **one PRD input category**; columns use **Orbit's real
terminology** where it could be confirmed (see *Field provenance* below).

| File | PRD input category |
|------|--------------------|
| `projects.csv` | Project Identifier (+ type, customer, status) |
| `scope-wbs.csv` | Project Scope (Orbit WBS: phases, tasks, deliverables, dependencies) |
| `timeline-milestones.csv` | Project Timeline (milestones / checkpoints, baseline vs adjusted) |
| `budget.csv` | Project Budget |
| `resources.csv` | Project Resources (occupancy, base estimate, competency) |
| `time-used.csv` | Project Time Used (registered hours, blocked days) |
| `risks.csv` | Project Risks |
| `issues.csv` | Project Issues |
| `decisions.csv` | Project Decisions |
| `meeting-minutes.md` | Meeting minutes (**unstructured** blob) |

All files share **`project_id`** (`ORB-1001` … `ORB-1006`). When real Orbit data arrives, this
becomes the Orbit project id — a value change, not a redesign.

## ⚠️ Field provenance — read before writing the real parser

The fixtures are shaped from what is **publicly documented** on orbit.online. Orbit's literal
**XSD element names and GraphQL field names are gated behind an API key / developer portal** and
could not be retrieved. So:

- **Vocabulary — confirmed from orbit.online** (used as our column names): WBS, **phases**,
  **tasks/activities**, **milestones**, **checkpoints** (static/relative execution mode),
  **deliverables** (a task can't complete until a document is uploaded), **dependencies**,
  **project type**, **customer**; **occupancy rate**, **base estimates** (planned) vs
  **registered hours** (actual), **competencies/certifications**, **professional groups**,
  **blocked days** (holiday/absence).
- **Inferred / not literally confirmed** (plausible but our own naming): exact column spellings,
  RAG `project_status` (Orbit shows "project status"; the Green/Amber/Red bucketing is *our*
  scoring, PRD-defined, not necessarily an Orbit field), and the whole of **`budget.csv`** —
  Orbit finance centers on invoicing/customers/cost, so planned/actual/forecast field names here
  are a guess.
- **May not live in Orbit at all** — `risks.csv`, `issues.csv`, `decisions.csv`. The PRD's top
  open question is whether risks/issues/decisions/minutes are in Orbit's RAID/notes, another tool,
  or only in unstructured minutes. Treat these three as *placeholders* until confirmed at kick-off.

**Before Phase 2 (real parser):** reconcile every column against one real (anonymized) Orbit
export or Orbit's published XSD. Do not code the parser against these guessed names.

Sources: [Orbit API & GraphQL](https://www.orbit.online/api-and-graphql/) ·
[Integration](https://www.orbit.online/overview/integration/) ·
[Project planning](https://www.orbit.online/project-planning-software/) ·
[Resource planning](https://www.orbit.online/resource-planning/)

## The dummy portfolio (6 projects)

| project_id | Name | Type | Baked-in signal (for the analysis layer to catch later) |
|-----------|------|------|-----------|
| ORB-1001 | Nordic Retail Platform | Implementation | 40% complete but 70% budget burned — the "very powerful" budget-vs-progress flag |
| ORB-1002 | Customer Data Migration | Implementation | Over budget, forecast overrun >15%, **missed critical checkpoint**, unmitigated critical risk, overdue decision |
| ORB-1003 | AI Advisory Discovery | Advisory | Healthy (Green) baseline |
| ORB-1004 | Warehouse Automation Rollout | Implementation | Amber, vendor slippage |
| ORB-1005 | Payments Compliance Upgrade | Implementation | Forecast overrun approaching threshold |
| ORB-1006 | Data Governance Framework | AI PMO | Green |

Cross-cutting: **Anna Berg** is PM / allocated on **5 projects** (1001, 1002, 1004, 1005, 1006) →
PRD "resource on 5+ projects = Red" key-person risk — and has a **blocked-day** holiday
(`time-used.csv` + minutes). Some risk/issue rows have `last_updated` >21 days before the PRD date
(2026-07-09) to exercise the "stale data" rule.

## How to upload one to test

The current endpoint stores the file as **opaque bytes** and the analyze step is a stub, so **any
single file** proves the wiring. Pick any structured CSV or the minutes blob.

```bash
# 1. log in (dev admin is seeded) — saves auth cookies
curl -c cookies.txt -X POST "http://localhost:5080/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@localhost","password":"Admin123!$"}'

# 2. upload a fixture -> returns { uploadId, fileName }
curl -b cookies.txt -X POST "http://localhost:5080/api/ingest/upload" \
  -F "file=@docs/samples/projects.csv"

# 3. analyze the stored upload -> emits one finding citing that upload
curl -b cookies.txt -X POST "http://localhost:5080/api/analyze/<uploadId-from-step-2>"

# 4. read the findings (Level-2 read endpoint; stub groups under DUMMY-001)
curl -b cookies.txt "http://localhost:5080/api/projects/DUMMY-001"
```

> On Windows PowerShell, `curl` is an alias for `Invoke-WebRequest`. Either call `curl.exe`
> explicitly (commands above work as-is) or use `Invoke-RestMethod`.
