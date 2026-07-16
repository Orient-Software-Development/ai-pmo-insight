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

The client's project data lives in **Orbit** (`orbit.online`), a WBS-based project-planning SaaS. Orbit exposes a **GraphQL API** (primary read technology, API-key auth), an **XML import** validated against a published **XSD schema**, exports to **Excel / CSV / XML / Word / PowerPoint**, and a **direct Power BI integration**. It also already ships **SAP / Maconomy** (finance), **time-tracking** (Intempus, Timegrip), and **HR** integrations, plus native **OpenAI / Azure AI** hooks.

Knowing the source system collapses the plan doc's *"can we get data examples, e.g., 3 months back?"* blocker: instead of reverse-engineering unknown fragmented files, ingest is written against a known schema, and the client ask shrinks to *"one Orbit export of a few projects."*

**Decision — the POC does not wait on client data.** It is built and demoed on **dummy fixtures shaped like an Orbit export**; real Orbit data is fed in later (export upload first, then the GraphQL pull — plan item #13, out of POC scope). See [POC input data](#poc-input-data-decided-dummy-fixtures). This takes the client off the build's critical path entirely; *what actually lives in Orbit vs. outside it* (see open questions) becomes a data-feed question, not a build blocker, because the fixtures exercise both analysis paths regardless.

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
- **Raw vs findings storage — deferred; POC stores together.** The plan doc says *"ideally only capture findings, not client data."* The eventual target is a **landing zone** for uploaded files (retention TBD — proposed default 30 days) separated from a **findings store** for structured findings + citations. **For the POC we store whatever is convenient (raw and findings may share a store); the raw/findings split, TTL, and retention are a later decision** (see open questions). The one invariant that holds regardless: **every finding keeps its citation** (source-file reference + locator) back to its input, so deferring the split never costs traceability — separating the stores later becomes a data move, not a rewrite.
- **Data Quality Agent surfaces duplicate-identity candidates for confirmation.** The plan doc's agent #2 is *"checks missing data, inconsistent project IDs, old updates."* For the POC we surface duplicate candidates to a PMO admin — never silently merge. Whether this warrants a first-class "Entities" module depends on how much the client's real data exhibits duplication.
- **Health-scoring rules live in a YAML file** in the repo (path TBD — `openspec/config` or `docs/config`), loaded by a `HealthScoringService`. Structure: weights per area, RAG thresholds, override rules (condition → minimum RAG). Migration to a DB-backed admin UI is a post-POC decision.

### Modules to add on top of the template

- **Ingest** — two modes against the Orbit source: **(POC)** manual upload of an Orbit export (CSV/XML/Excel), parsed by adapters written against Orbit's export shape / XSD; **(later, plan item #13, out of scope)** a GraphQL pull directly from the client's Orbit tenant. Same landing-zone storage abstraction behind both. For the POC build the upload is fed **dummy fixtures** (below) so no live Orbit access is required.
- **Analysis** — the agent orchestrator, per-skill prompt registry, LLM client abstraction, findings-store persistence, evidence citation model.
- **Health scoring** — YAML-driven weighted score + override engine, exposed as a query over the findings store.
- **Dashboards** — three levels in the React SPA (Executive / Project / Data Quality). Every finding cites its evidence source.
- **Auth extensions** — additional roles + role-scoped endpoint authorization.

### Data model philosophy (from the doc)

The eventual target is to persist **findings and citations**, not client operational data — a direct read of the plan doc's *"ideally only capture findings, not client data."* **For the POC this separation is deferred** (see the storage decision above and open questions): raw and findings may share a store, and whether raw uploads get a TTL / landing zone is decided later. Parsed intermediate structures may be cached during a single analysis run but are not part of the durable domain model. Every finding record includes: source-file reference, sheet/row or meeting-date locator, agent that produced it, confidence score, and the input snapshot used (so re-analysis after prompt changes is traceable).

### LLM & agent runtime

The plan doc says *"Identify AI Skills — What to do?"* — deliberately open. Two candidates for the POC: **Claude Agent SDK** or **Microsoft Semantic Kernel**. Decision deferred to kick-off; both fit the "orchestrator + skills" shape. Prompt versioning belongs in the repo, not in a database, for the POC.

### POC input data (decided: dummy fixtures)

The POC is built and demoed against **dummy fixtures**, not live client data. Two rules keep the dummy honest:

1. **Shaped like an Orbit export.** Fixtures mirror Orbit's export columns / XSD for the structured categories (identifier, scope/WBS, timeline, budget, resources, time used), so the later swap from dummy → real Orbit data is a *parser change, not a redesign*. A wrongly-shaped dummy would give false confidence.
2. **Includes at least one unstructured meeting-minutes sample.** Even though it is not yet confirmed whether risks/issues/decisions/minutes live inside Orbit, the fixture set carries a free-text minutes blob so the pipeline exercises **both** analysis paths — deterministic scoring over structured data *and* LLM-over-text extraction — before we know where those categories actually live. This de-risks the fork rather than waiting on it.

The fixtures ship in the repo and double as golden-file test inputs (see Testing). Real Orbit data replaces them via the ingest upload mode once the client provides a sample export.

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
- **Vector search / RAG** — not needed at POC scale. Meeting minutes fit in a single LLM context per project, so the pipeline uses schema-constrained LLM *extraction* over full documents rather than embed-and-retrieve. Revisit when minute volume outgrows the context window, or when cross-portfolio Q&A / lessons-learned retrieval (Enhancement Options) becomes a feature. If added later, `pgvector` on the existing Postgres is the natural fit — no new infra, and the `ILlmClient` port stays unchanged.

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

- **Sample data availability** — ~~was the highest-priority blocker~~. **Resolved for the build:** source system is **Orbit** and the POC runs on dummy fixtures, so the build no longer waits on client data. A real (anonymized) Orbit export of a few projects is still wanted to validate the parsers, but it is no longer on the critical path.
- **What lives in Orbit vs. outside it** — specifically **risks / issues / decisions / meeting minutes**. Structured categories (schedule, budget, resources, time) are near-certainly in Orbit; these four may be in Orbit's RAID/notes, in another tool, or only in unstructured minutes. **This is the top decision-driver** for how much of the pipeline is deterministic scoring vs. LLM-over-text. Not a build blocker (fixtures cover both paths), but the first thing to settle at kick-off.
- **Is the data actually "fragmented"?** — the problem statement assumes scattered data. If Orbit already consolidates the structured KPIs (and Power BI already charts them), the AI's value shifts to unstructured extraction, cross-referencing (e.g. "40% done / 70% burned"), early-warning, and the Challenge/Review trust layer — *not* re-computing dashboards Orbit gives for free.
- **Why not Orbit-native AI + Power BI?** — Orbit already integrates OpenAI/Azure AI and exports to Power BI. Pin down NextWave's differentiator (portfolio-level cross-project early-warning + adversarial trust layer) so the POC doesn't rebuild what the client already has.
- **Audience clarification** — target listed as *"Executive, Project Management, Other?"* — the "Other?" needs to be settled.
- **Client / commercial risk — how is it defined?** — the plan doc lists a Level-1 panel *"Client/commercial risk → projects at risk of damaging commitments"* (line 241) but gives **no KPI, rule, or threshold** for it, and no structured commercial/SLA/contract field exists in the input categories. A **customer-exposure proxy** (at-risk projects grouped by `customer`) is buildable now from existing data and can ship labelled as relationship-exposure. The **true commercial signal** (contract value at risk / margin erosion / SLA-penalty exposure) **needs the client** — what counts as "commercial risk", and does Orbit or another system carry contract/margin/SLA data? Until then that part stays a flagged placeholder, never a fabricated number. See `docs/l1-executive-portfolio-followups.md` (#6).
- **Health-scoring weights and override rules** — the doc explicitly marks its example table as *"EXAMPLE!"*. Final numbers with the PMO before scoring goes live.
- **Absence handling** — the doc says *"clarify if possible"*.
- **Activity signal thresholds** — the doc's *"No moving forward / very slow, medium, okay?"* is open.
- **AI Skills catalogue** — plan item #9 is a bare *"What to do?"*.

### Open questions added by this PRD (not in the doc)

- **LLM runtime choice** — Claude Agent SDK vs. Semantic Kernel.
- **Raw vs findings storage split** — **deferred for the POC** (we store together for now). To decide later: whether raw client data is separated from findings into a distinct landing zone, and how strictly the plan doc's *"only capture findings, not client data"* is enforced. Trade-off = data-governance/privacy posture vs. build simplicity. The finding→citation link is preserved regardless, so this can be revisited without a rewrite.
- **Retention of raw uploads** — proposed default 30-day landing-zone TTL; final policy depends on the client's data-governance stance (tied to the storage-split question above).
- **Hosting region and data-residency constraints** — the input set includes named individuals (meeting minutes), potentially absenteeism data, and commercially sensitive budget/resource data; hosting jurisdiction should be confirmed with the client.

### Next artefact

`/opsx:explore` on the walking skeleton: **upload a dummy Orbit-shaped fixture → landing-zone + findings-store split → a stub analysis that emits one finding *citing its source* → single-project (Level 2) read endpoint**. Explicitly **no LLM and no real parser in the first slice** — the parser is stubbed and the input is a dummy fixture, so the slice proves the *architecture* (the landing-zone / findings-store separation and the citation link) rather than the ingest. The citation is the one thing that must be real from commit one. With dummy data as the decided input, this slice has no undecided fork left in it and is ready to become the first OpenSpec change.
