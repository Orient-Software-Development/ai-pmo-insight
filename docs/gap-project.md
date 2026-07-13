# POC — Feature Gap Register

Tracks divergence between the **PRD** ([poc-ai-pmo-insight.md](prds/poc-ai-pmo-insight.md)) and
what has actually been designed & built in this repo. Companion to [gap.md](gap.md), which
covers the auth-implementation gaps only.

> **Legend** — ✅ done · 🟠 open — deferred to a future OpenSpec change (already scoped) · 🔴 open —
> undesigned (needs PRD/design work before any change can be written) · 🟡 open — decision pending
> kick-off · ⚪ post-POC / by-design deferred.

---

## Section 0 — In-flight: current change `add-ingest-findings-skeleton`

This is the walking skeleton. `tasks.md` sections 1–4 are done (domain, persistence, ingest slice, findings slice). Remaining tasks:

| # | tasks.md ref | Status | Note |
|---|---|---|---|
| 0.1 | 5.1 — Orbit-shaped dummy fixture | 🟠 | Shape must mirror Orbit's export columns/XSD; treat as the spec for the later real parser |
| 0.2 | 5.2 — Fixture referenced from tests as golden-file input | 🟠 | Blocks 7.x |
| 0.3 | 6.1 — Read-only React page calling `GET /api/projects/{projectKey}` | 🟠 | Keeps skeleton end-to-end (UI included) |
| 0.4 | 6.2 — Wire view into SPA routing/nav | 🟠 | |
| 0.5 | 7.1–7.4 — Domain unit + integration tests | 🟠 | Prove citation invariant + endpoint contracts |
| 0.6 | 8.1–8.2 — End-to-end verify + `openspec validate` | 🟠 | Definition of done for this change |

**Do these before opening any new change.** Continue with `/opsx:apply`.

---

## Section 1 — Deferred by design (planned future changes, already scoped)

Explicitly out-of-scope in the current change; belong in their own OpenSpec changes.

| # | Feature | Where PRD promises it | Blocking dependency |
|---|---|---|---|
| 1.1 | 9-agent orchestrator + `ILlmClient` + prompt registry | Solution §2; Implementation Decisions | 🟠 **In flight** — `add-analysis-agent-pipeline` shipped the deterministic layer + trust layer via `FakeLlmClient`; real runtime adapter is the next change (§3.1). Undesigned gaps §2.1–§2.3 resolved. |
| 1.2 | Real parsers — Orbit CSV/XML/Excel + meeting-minutes `.docx` | Solution §1; Modules to add | Still deferred. #1 Data Collector parses the **dummy Orbit-shaped fixtures** (ClosedXML/System.Xml/OpenXml); hardened real-Orbit parsing (full XSD, anonymized exports, parse-status) remains a later change. |
| 1.3 | Health-scoring engine — YAML rules + weighted score + overrides | Implementation Decisions | Rule structure can be built without final weights; weights themselves are §3.3 |
| 1.4 | Level-1 Executive dashboard | Solution §3; user story #3 | Portfolio-level data model (§2.5) |
| 1.5 | Level-3 Data Quality dashboard | Solution §3; user story #8 | DQ synthesis model (§2.6) |
| 1.6 | Full Level-2 view — G/A/R + explanation, deviations, milestones, decisions, recommendation, confidence | Solution §3; user story #5 | Scoring (§1.3) + agents (§1.1) both live |
| 1.7 | Duplicate-identity detection | User story #2 | Data Quality Agent live (§1.1); currently `projectKey` is opaque string, no `Project` entity |
| 1.8 | PMO roles seed — `pmoAdmin`, `pmoUser`, `executive` | Implementation Decisions | Kick-off audience clarification (§3.5) |
| 1.9 | Raw ↔ findings storage split + retention TTL | Implementation Decisions | Retention decision (§3.2). Citation link keeps this a data move, not a rewrite. |
| 1.10 | Orbit GraphQL pull (automated data load) | Solution §1; plan item #13 | ⚪ Post-POC by definition |

---

## Section 2 — Undesigned (need PRD/design work before a change can be written)

These are **non-obvious** gaps. They aren't in the current change AND haven't been drafted for a future one. Writing the future change without settling them = decisions baked into code without being explicit.

| # | Gap | Why it matters |
|---|---|---|
| 2.1 | ✅ **Confidence-score methodology.** Resolved in `add-analysis-agent-pipeline`: shared `ConfidencePolicy` derives High/Medium/Low deterministically from DQ signals (missing-field-count + staleness + source-consistency); LLM self-report is `Cap`ped by DQ confidence so the scale is comparable across agents. POC defaults are documented and swappable without a schema change (gap §2.1). | Appears on every finding & every recommendation (user stories #4, #5, #6). |
| 2.2 | ✅ **Prompt-version / input-snapshot tag on `Finding`.** Resolved: `Finding` now carries `PromptVersion` (prompt **content hash**), `ProducingAgent`, `Kind`, `RunId`, and `Confidence`; `Citation` extended with structured excerpt + text snippet. | Re-analysis after prompt changes is now traceable per finding. |
| 2.3 | ✅ **Findings lifecycle.** Resolved: re-analyzing the same upload **appends** under a new `RunId`; prior run's findings are retained (never overwritten or silently duplicated). | Defined before agents shipped. |
| 2.4 | 🔴 **Ingest partial-success semantics.** A fixture has 100 rows, 3 malformed. Fail whole? Succeed with row-level warnings? Store an "ingest report"? | User story #1 asks for *"which parsed cleanly and which need mapping"* — implies row-level status. Not designed anywhere. |
| 2.5 | 🔴 **Portfolio-level data model.** L1 is inherently cross-project. Findings are grouped by `projectKey`. There is no "portfolio finding" / aggregate type. | Blocks §1.4. Choice: aggregate at read time (query), or materialize portfolio findings (write). Different perf/consistency trade-offs. |
| 2.6 | 🔴 **Data Quality (L3) synthesis model.** The PRD's L3 examples — *"no risk update in 21 days"*, *"budget actuals missing"* — are **derived** signals, not raw DQ-Agent findings. | Blocks §1.5. Undefined → we'll rebuild the same derivation logic twice (once for scoring's data-quality weight, once for L3). |
| 2.7 | 🔴 **Evaluation harness design.** PRD calls for *"small evaluation harness with snapshot suite"* to score the *"PMs agree with 80%+"* success criterion. What are the inputs, ground-truth format, scoring rubric? | Still deferred (own change, after the real `ILlmClient` adapter). CI deliberately does **not** assert live LLM content today — the pipeline is tested end-to-end via `FakeLlmClient` only. This is the **gate for calling the POC successful** (success criterion #5). |
| 2.8 | 🔴 **LLM cost budget + observability.** Per-analysis token cap? Cost tracking? LLM-specific OTEL spans? | LLM calls at portfolio-scale can burn budget silently. Template has OpenTelemetry but no LLM span conventions or cost/error metrics. |
| 2.9 | 🔴 **Analysis triggering / scheduling.** On-demand only (current)? Scheduled portfolio runs (nightly / weekly)? Both? | The plan doc's *"portfolio cycle"* implies scheduled. Affects whether we need a job runner (Hangfire / EF-tracked queue / cron). |
| 2.10 | 🔴 **Audit log.** Who uploaded what, when. Read access log. | Named individuals in meeting minutes + potentially absenteeism data → GDPR / traceability question. PRD earlier had this concept; it was dropped. Revisit before agents land. |
| 2.11 | ✅ **Prompt versioning on disk.** Resolved in `add-analysis-agent-pipeline`: prompt files live in the repo under `Features/Analysis/Prompts/`, keyed and versioned by **content hash**; that hash is the `PromptVersion` stamped on LLM findings. | No database; re-analysis after prompt edits is traceable via the hash. |

---

## Section 3 — Kick-off decisions (block Tier 2 planning)

Already tracked in the PRD's open-questions list. Each gates a specific piece of design work above.

| # | Decision | Gates |
|---|---|---|
| 3.1 | 🟡 **LLM runtime** — Claude Agent SDK vs Microsoft Semantic Kernel vs Azure OpenAI direct | `ILlmClient` adapter shape; prompt registry format (§1.1, §2.11) |
| 3.2 | 🟡 **Raw-upload retention + storage split policy** | §1.9; TTL default (30 days proposed) |
| 3.3 | 🟡 **Final scoring weights + override rules** — plan doc marks the example table `EXAMPLE!` | Scoring engine constants (§1.3 rule *structure* can be built first) |
| 3.4 | 🟡 **Hosting region + data residency** | Deploy target; encryption / access-log posture (§2.10) |
| 3.5 | 🟡 **Audience "Other?"** — plan doc lists *"Executive, Project Management, Other?"* | Roles seed (§1.8) |
| 3.6 | 🟡 **AI Skills catalogue** — plan item #9 is a bare *"What to do?"* | Which of the 9 agents get built, which collapse (§1.1) |
| 3.7 | 🟡 **What lives in Orbit vs outside** — specifically risks / issues / decisions / meeting minutes | Determines LLM-vs-deterministic split; parser scope (§1.2) |
| 3.8 | 🟡 **Why not Orbit-native AI + Power BI?** — pin down NextWave's differentiator | Scope-clarity — affects whether L1 competes with Orbit's own outputs |
| 3.9 | 🟡 **Absence handling** — plan doc says *"clarify if possible"* | Resource concentration × absence signal (user story #7) |
| 3.10 | 🟡 **Activity signal thresholds** — plan doc's *"No moving forward / very slow, medium, okay?"* | Scoring input |

---

## Section 4 — Post-POC / by-design deferred

Not gaps against the POC — listed here so they don't creep in.

| Item | Source | Note |
|---|---|---|
| ⚪ Vector search / RAG | PRD Out of Scope | Add pgvector on existing Postgres when meeting-minute volume outgrows LLM context, or when cross-portfolio Q&A becomes a feature. `ILlmClient` port unchanged. |
| ⚪ Automated data load (Orbit GraphQL pull) | PRD Out of Scope; plan item #13 | Distinct later activity, same engagement |
| ⚪ Multi-tenancy | PRD Out of Scope | Single-client engagement |
| ⚪ Orchestration layer (beyond agent pipeline) | Plan doc Enhancement Options | |
| ⚪ Project-type library (Advisory / Implementation / AI PMO / AI Friction) | Plan doc Enhancement Options | |
| ⚪ Lessons-Learned exchange (client ↔ NextWave / other customers) | Plan doc Enhancement Options | |
| ⚪ Machine learning over time | Plan doc Enhancement Options | |
| ⚪ Admin UI to edit scoring rules | Implementation Decisions | YAML + git in POC; DB-backed UI is post-POC |
| ⚪ Real-time collaboration on findings | | Not requested |
| ⚪ Mobile app / dedicated mobile layout | | SPA responsive baseline only |

---

## Suggested next step

1. **Finish the skeleton** — apply Section 0 tasks (`/opsx:apply` continues from task 5.1).
2. **Design-pass on Section 2 gaps §2.1–§2.3** — confidence methodology, prompt-version tagging, findings lifecycle. These are the three that touch the `Finding` schema and would force a migration if bolted on later.
3. **Then** open `/opsx:explore` for the next change (`add-agent-orchestrator`), armed with the §2 decisions.
4. Kick-off — resolve Section 3, especially §3.1 (LLM runtime) and §3.7 (what's in Orbit) — before any real-parser change can be scoped.
