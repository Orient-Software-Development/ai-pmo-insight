# PRD: AI PMO Insight — Proof of Concept

> **Status**: Draft. Seeds `/opsx:explore` and the client kick-off conversation.
> **Source of truth**: [`docs/inputs/Project Plan with Activity Descriptions.md`](../inputs/Project%20Plan%20with%20Activity%20Descriptions.md) (converted from the original `.docx`). The wireframe (`docs/inputs/pmo-wireframe.html`) is a **draft sketch**, not a source of truth — nothing in this PRD depends on it.
> **What this PRD is**: the POC deliverable inside the wider NextWave engagement. It quotes the plan doc where things are locked, and flags every "EXAMPLE" / "not decided" call-out for kick-off.

## Problem Statement

NextWave's client currently produces status reporting by hand from fragmented project data. The plan doc lists the input categories expected in scope:

- Project Identifier (ideally project type, if multiple exist)
- Project Scope
- Project Timeline
- Project Budget
- Project Resources (ideally per scope item)
- Project Time Used (ideally per resource)
- Project Risks
- Project Issues
- Project Decisions
- Minutes from project meetings (unstructured data expected)

Consolidating these into portfolio-level insight is slow, inconsistent between projects, and misses signals that only surface when categories are cross-referenced. Leadership therefore receives status that is late, subjective, and hard to trust.

Framed by the doc:

> *"We do not just automate status reporting. We turn fragmented project data into portfolio-level management insight, decision support, and early-warning signals."*

The specific input files (spreadsheet names, sheet layouts, meeting-minute templates) are **not yet known** — the plan explicitly asks the client *"can we get data examples, e.g., 3 months back?"* Ingest work cannot be built without those samples.

## Solution

A single-tenant web service (this repository, extended from the .NET Clean Architecture + React template) that:

1. **Ingests** the client's project data via **manual upload** of agreed template files. Automated data load is a **later activity in the same engagement (plan item #13)**, not part of POC scope.
2. **Analyses** the ingested data through a **suggested 9-agent pipeline**. The plan doc lists this pipeline explicitly as *"Assumption but not decided"* — the exact split (and whether some agents collapse into one for the POC) is confirmed at kick-off. The 9 suggested agents: **Data Collector**, **Data Quality**, **Status Analyst**, **Risk & Issue**, **Financial Analyst**, **Resource**, **Narrative**, **Challenge**, **Review**.
3. **Publishes** findings across the three dashboard levels the plan doc defines:
   - **Level 1 — Executive Portfolio Summary**: overall portfolio health (G/A/R count), projects needing intervention (top with reason), financial exposure, resource bottlenecks, decision backlog, client/commercial risk, recommended actions.
   - **Level 2 — Individual Project Status**: overall status (G/A/R + explanation), this-period progress, key deviations (budget/time/scope/risks/resources), top risks & issues, upcoming milestones (next 2–4 weeks), decisions needed (owner/deadline/consequence), AI recommendation, confidence level.
   - **Level 3 — Data Quality**: examples listed in the doc — "no risk update in 21 days", "budget actuals missing", "milestone dates not updated", "resource plan does not match time entries".

## User Stories

Grounded in the plan doc's audience list (*"Executive, Project Management, Other?"*) and the success criteria.

1. As a **PMO admin**, I want to upload a set of project files and see which parsed cleanly and which need mapping, so that I know the analysis is running on complete inputs.
2. As a **PMO admin**, I want the system to flag when the same project appears under two different identifiers and prompt me to confirm the merge, so that hours and costs aren't split across duplicate entities. *(Plan doc, agent #2: "Data Quality Agent — checks missing data, inconsistent project IDs, old updates.")*
3. As an **executive**, I want a Level-1 portfolio view showing green/amber/red counts, financial exposure, decision backlog, resource bottlenecks, and top intervention candidates, so that I can spend my time on projects that actually need me.
4. As an **executive**, I want each recommended action to name an owner, deadline, and confidence level, so that the recommendation is actionable rather than advisory. *(Plan doc success criterion #6: "Every red/amber project has a clear recommended action.")*
5. As a **project manager**, I want to open a single project (Level 2) and see this-period progress, key deviations, top risks and issues, upcoming milestones, decisions needed, and the AI's recommendation — each cited to a source — so that I can verify the reasoning before acting.
6. As a **project manager**, I want a confidence level on each project status, so that I know when the AI is reasoning from stale or missing data.
7. As a **portfolio manager**, I want key-person risk to combine allocation concentration with absence, so that a resource on many projects who is also frequently absent is flagged higher than one with the same concentration but low absence. *(Plan doc rules: "Project Resources: Red — resource allocated 5 or more projects, Amber 3–4, Green <3"; "Absence, e.g. holiday or illness — clarify if possible".)*
8. As a **data lead**, I want a Level-3 Data Quality view listing missing/inconsistent items and remediation actions, so that I know exactly what to fix to lift confidence back above the target threshold.
9. As a **project manager**, I want to see the Challenge and Review agent outputs alongside the finding, so that I trust the conclusion has been argued against and stress-tested before publication. *(Plan doc agents #8 and #9.)*
10. As a **PMO admin**, I want to know when the health-scoring rules produced a rating that was later modified by an override rule (e.g. "critical milestone missed → minimum amber"), so that the scoring is transparent and auditable.

## KPIs from the plan doc

The doc supplies a KPI catalogue per area. This is the **starting set**; kick-off will trim/expand.

**Data Quality:** completeness score, last update age, missing KPI count, source consistency score, confidence level.

**Schedule / MS Project:** milestone adherence, schedule variance, delay severity, upcoming milestone risk, dependency risk.

**Budget:** budget variance, forecast variance, burn rate, budget-consumption-vs-progress (the doc calls this out as *"very powerful"* — e.g. 40% done / 70% burned = high financial risk), financial exposure.

**Resource:** resource allocation variance, capacity pressure, missing critical roles, time burn variance, role-level bottleneck.

**Scope:** scope change count, unapproved scope items, change request value, scope stability score, open scope decisions.

**Risk / Issue:** open high risks, risk trend, unmitigated risk count, issue age, escalation need.

**Decision:** decisions overdue, decisions due soon, blocked-by-decision count, decision impact.

Additional rules listed in the doc:
- **Budget:** Green ≤ +5%, Amber >5–15%, Red >15%.
- **Project Allocation:** Red if no allocation, Green if found.
- **Project Resources (concentration):** Red = resource on 5+ projects, Amber = 3–4, Green = <3.
- **Time usage:** lack of time used per allocation.
- **Absence:** e.g. holiday/illness — *"clarify if possible"*.
- **Activity:** no moving forward / very slow / medium / okay.
- **Risks:** increasing → Amber; no risks → Red; check all risks have mitigation plan.
- **Issues:** growing path + mitigation.

## Health scoring (from the plan doc, marked "EXAMPLE!")

Weighted score across areas, then bucketed:

| Area | Weight |
|---|---|
| Schedule | 20% |
| Budget | 20% |
| Scope | 15% |
| Resources | 15% |
| Risks / issues | 15% |
| Decisions / dependencies | 10% |
| Data quality | 5% |

Score → Status: **80–100 Green**, **60–79 Amber**, **0–59 Red**.

Override rules (still example):

| Condition | Result |
|---|---|
| Critical milestone missed | Minimum Amber |
| Forecast overrun >15% | Minimum Amber or Red |
| Critical unmitigated risk | Minimum Red |
| Key decision overdue and blocking work | Minimum Amber |
| Data confidence very low | Status marked "Needs PM Review" |

**Every number and rule in this section is an EXAMPLE in the source doc.** Final weights, thresholds, and overrides are agreed with the client's PMO at kick-off — before scoring goes live.

## Implementation Decisions (PRD, extending the doc)

The plan doc leaves implementation details open. The bets below **extend it** — they are this PRD's design choices for this repo, not restatements from the source.

- **Single tenant.** One customer, one auth realm, one PostgreSQL database. No `TenantId` in the domain. Auth uses the template's cookie-transported JWT + Identity + role-based authorization.
- **Roles.** Seed `admin`, `pmoAdmin`, `pmoUser`, `executive`. Confirmed at kick-off — the plan doc lists the audience as *"Executive, Project Management, Other?"* and the "Other?" is deliberately open.
- **Agent pipeline = one orchestrator + N skills, not N services.** The 9 agents in the plan doc are conceptual. In code they become skill/prompt definitions with typed input/output contracts, invoked by a single orchestrator in the Application layer. Agents 3–6 (Status, Risk, Financial, Resource) may collapse to fewer skills for the POC and expand later. Challenge and Review remain distinct — they are the trust story.
- **Raw data is separated from findings from day one.** The plan doc says *"ideally only capture findings, not client data."* We implement this as a **landing zone** for uploaded files (retention TBD — proposed default 30 days) and a **findings store** for structured findings + citations (retained per client policy). Domain aggregates are separable so retention can be flipped without a rewrite.
- **Data Quality Agent surfaces duplicate-identity candidates for confirmation.** The plan doc's agent #2 is *"checks missing data, inconsistent project IDs, old updates."* For the POC we surface duplicate candidates to a PMO admin — never silently merge. Whether this warrants a first-class "Entities" module depends on how much the client's real data exhibits duplication.
- **Health-scoring rules live in a YAML file** in the repo (path TBD — `openspec/config` or `docs/config`), loaded by a `HealthScoringService`. Structure: weights per area, RAG thresholds, override rules (condition → minimum RAG). Migration to a DB-backed admin UI is a post-POC decision.

### Modules to add on top of the template

- **Ingest** — upload endpoints, parser adapters per file type (one per input category once sample templates arrive). Landing-zone storage abstraction.
- **Analysis** — the agent orchestrator, per-skill prompt registry, LLM client abstraction, findings-store persistence, evidence citation model.
- **Health scoring** — YAML-driven weighted score + override engine, exposed as a query over the findings store.
- **Dashboards** — three levels in the React SPA (Executive / Project / Data Quality). Every finding cites its evidence source.
- **Auth extensions** — additional roles + role-scoped endpoint authorization.

### Data model philosophy (from the doc)

Persist **findings and citations**, not client operational data — a direct read of the plan doc's *"ideally only capture findings, not client data."* Raw uploaded files live in the landing zone with a TTL. Parsed intermediate structures may be cached during a single analysis run but are not part of the durable domain model. Every finding record includes: source-file reference, sheet/row or meeting-date locator, agent that produced it, confidence score, and the input snapshot used (so re-analysis after prompt changes is traceable).

### LLM & agent runtime

The plan doc says *"Identify AI Skills — What to do?"* — deliberately open. Two candidates for the POC: **Claude Agent SDK** or **Microsoft Semantic Kernel**. Decision deferred to kick-off; both fit the "orchestrator + skills" shape. Prompt versioning belongs in the repo, not in a database, for the POC.

## Testing Decisions (PRD, not in doc)

- **Ingest parsers** — golden-file tests per input template using anonymized real sample files provided by the client. Assert extracted findings, not raw parsed rows.
- **Health scoring** — table-driven tests over the YAML rules: weighted-score cases, each override rule, precedence when multiple overrides fire.
- **Agent pipeline** — mock the LLM client at the abstraction boundary; assert the orchestrator's control flow (which skills run when, how citations propagate). Do not assert LLM output content in unit tests — that belongs in a small evaluation harness with a snapshot suite.
- **Dashboard end-to-end** — integration tests that upload a known input set through `TestWebAppFactory`, then assert the L1/L2/L3 endpoints return the expected findings + citations. Reuse the cookie/auth pattern from `AuthEndpointsTests`.
- **Trust criterion** — evaluation harness that runs a fixed set of client-provided historical project snapshots, compares AI verdicts against PM-labelled ground truth, and reports the *"PMs agree with 80%+ of AI conclusions"* metric from the doc.

## Out of Scope

Explicitly listed in the plan doc's *"Enhancement Options (Out of Scope in POC)"*:

- **Orchestration layer** (beyond the single agent-pipeline orchestrator).
- **Documentation** as a first-class deliverable.
- **Project-type library** — Advisory, Implementation, AI PMO, AI Friction.
- **Lessons-Learned exchange** — client → NextWave, and NextWave → client (from other customers).
- **Machine learning over time.**

Implied by scope framing, not stated in the doc:

- **Multi-tenancy** — the engagement is a single-client POC.

Not out-of-scope forever, but **not in POC**:

- **Automated data load** — plan item #13, a distinct later activity in the same engagement (*"Clarify technology and build data load"*). POC uses manual upload only.

## Further Notes

### Success criteria (from the plan doc)

1. **Time Saved** — reduce PMO reporting time by 50–90%.
2. **Status Quality** — leadership says AI-produced reports are more useful than the current manual reports.
3. **Risk Detection** — AI finds risks not clearly visible in current status reports.
4. **Data Quality** — AI identifies missing / inconsistent project data.
5. **Trust** — project managers agree with 80%+ of AI-generated conclusions.
6. **Actionability** — every red/amber project has a clear recommended action.

### Resource estimate (from the plan doc)

Approx. **~25 person-days**:

- AI Solution Architect — 3–4 days
- Agent Designer — 3–5 days
- AI Prompt / AI Designer — 5–7 days
- Data Model Builder — 3–5 days
- Dashboard Designer — 3–5 days
- Data Governance Specialist — 1–2 days

Delivered through the plan's activity blocks: scope → landscape → POC scope+criteria → kick-off workshop → health-status logic → KPIs → health scoring → environment + agents → identify skills → dashboard design → prototype build → manual-input POC run → automated data load → go-live → hypercare + evaluation.

### Open questions from the plan doc (resolved at kick-off)

- **Sample data availability** — the plan explicitly asks *"can we get data examples, e.g., 3 months back?"* Ingest parsers cannot be built without real (anonymized) template files. **Highest-priority blocker.**
- **Audience clarification** — target listed as *"Executive, Project Management, Other?"* — the "Other?" needs to be settled.
- **Health-scoring weights and override rules** — the doc explicitly marks its example table as *"EXAMPLE!"*. Final numbers with the PMO before scoring goes live.
- **Absence handling** — the doc says *"clarify if possible"*.
- **Activity signal thresholds** — the doc's *"No moving forward / very slow, medium, okay?"* is open.
- **AI Skills catalogue** — plan item #9 is a bare *"What to do?"*.

### Open questions added by this PRD (not in the doc)

- **LLM runtime choice** — Claude Agent SDK vs. Semantic Kernel.
- **Retention of raw uploads** — proposed default 30-day landing-zone TTL; final policy depends on the client's data-governance stance.
- **Hosting region and data-residency constraints** — the input set includes named individuals (meeting minutes), potentially absenteeism data, and commercially sensitive budget/resource data; hosting jurisdiction should be confirmed with the client.

### Next artefact

`/opsx:explore` on the walking skeleton: **upload → parse one input category (once a sample arrives) → landing-zone + findings-store split → single-project detail endpoint**. Explicitly **no LLM in the first slice** — that proves the data-model separation before adding intelligence on top.
