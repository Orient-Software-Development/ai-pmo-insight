# Sample fixtures — dummy Orbit-shaped export

These are **dummy fixtures**, not real client data. They exist to test the ingest → analyze →
cited-finding → read walking skeleton (`openspec/changes/add-ingest-findings-skeleton`) and to
double as golden-file test inputs. Each file maps to **one Orbit / PRD input category**.

| File | Orbit / PRD input category |
|------|----------------------------|
| `projects.csv` | Project Identifier (+ type, timeline summary, status) |
| `scope-wbs.csv` | Project Scope (Orbit is WBS-based) |
| `timeline-milestones.csv` | Project Timeline (milestones, baseline vs forecast vs actual) |
| `budget.csv` | Project Budget (planned / actual / forecast) |
| `resources.csv` | Project Resources (allocation per scope item) |
| `time-used.csv` | Project Time Used (hours per resource) |
| `risks.csv` | Project Risks |
| `issues.csv` | Project Issues |
| `decisions.csv` | Project Decisions |
| `meeting-minutes.md` | Minutes from project meetings (**unstructured** blob) |

All files share the key **`project_id`** (`ORB-1001` … `ORB-1006`) so they cross-reference into one
portfolio. When real Orbit data arrives, this `project_id` becomes the Orbit project id — a value
change, not a redesign.

## The dummy portfolio (6 projects)

| project_id | Name | Type | Baked-in signal (for the analysis layer to catch later) |
|-----------|------|------|-----------|
| ORB-1001 | Nordic Retail Platform | Implementation | 40% complete but 70% budget burned — the "very powerful" budget-vs-progress flag |
| ORB-1002 | Customer Data Migration | Implementation | Over budget, forecast overrun >15%, **critical milestone missed**, unmitigated critical risk, overdue decision |
| ORB-1003 | AI Advisory Discovery | Advisory | Healthy (Green) baseline |
| ORB-1004 | Warehouse Automation Rollout | Implementation | Amber, vendor slippage |
| ORB-1005 | Payments Compliance Upgrade | Implementation | Forecast overrun approaching threshold, tight deadline |
| ORB-1006 | Data Governance Framework | AI PMO | Green |

Cross-cutting signals: **Anna Berg** is PM / allocated on **5 projects** (1001, 1002, 1004, 1005,
1006) → PRD "resource on 5+ projects = Red" key-person risk — and the minutes note she is on two
weeks' holiday. Some risk/issue rows have `last_updated` >21 days before the PRD date (2026-07-09)
to exercise the "stale data" rule.

## How to upload one to test

The current endpoint stores the file as **opaque bytes** and the analyze step is a stub, so **any
single file** here proves the wiring. Pick any structured CSV or the minutes blob.

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
