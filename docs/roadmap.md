# Roadmap — AI PMO Insight POC

Status of the POC build. Grounds truth against the code in this repo, the PRD
(`docs/prds/poc-ai-pmo-insight.md`), and the first OpenSpec change
(`openspec/changes/add-ingest-findings-skeleton`).

**Legend:** ✅ done · 🟡 partial / stubbed · ⬜ not started · 🔒 out of POC scope

> Last reviewed: 2026-07-09.

---

## Phase 0 — Template foundation ✅

Inherited from the .NET Clean-Architecture + React template; already working.

- ✅ Clean Architecture layers (Domain / Application / Infrastructure / Api)
- ✅ In-process CQRS mediator (`AiPMOInsight.Application/Messaging`, no MediatR)
- ✅ EF Core + PostgreSQL, snake_case configs, `docker-compose` local DB
- ✅ Auth: JWT in httpOnly cookies, RBAC via ASP.NET Core Identity (`docs/authentication.md`)
- ✅ Roles seeded: `admin`, `user`; optional dev admin
- ✅ Health checks (`/health/live`, `/health/ready`), OpenTelemetry wiring
- ✅ React + Vite SPA shell (login, layout, theme toggle, change password)
- ✅ GitHub Actions CI + reference `Widgets` vertical slice

---

## Phase 1 — Walking skeleton (upload → analyze → cited finding → read) ✅ / 🟡

The current slice. Proves the *architecture* end-to-end; the *intelligence* is deliberately stubbed.

**Done — the plumbing walks every layer:**
- ✅ Upload endpoint — `POST /api/ingest/upload` stores raw bytes, returns `uploadId`
  (`IngestEndpoints.cs`, `UploadFixture.cs`, `EfUploadRepository.cs`, `Upload.cs`)
- ✅ Analyze endpoint (separate seam, synchronous) — `POST /api/analyze/{uploadId}`
  (`FindingsEndpoints.cs`, `AnalyzeUpload.cs`)
- ✅ Level-2 read endpoint — `GET /api/projects/{projectKey}` (`GetProjectFindings.cs`)
- ✅ Domain: `Finding` aggregate + mandatory `Citation` value object
  (`Finding.cs`, `Citation.cs`) — every finding cites its source upload
- ✅ Persistence: `Upload` + `Finding` EF entities + `InitialCreate` migration, auto-migrated in Dev
- ✅ Minimal read-only React view — `components/ProjectFindings.jsx`
- ✅ Dummy Orbit-shaped sample fixtures — `docs/samples/` (one file per input category)

**Stubbed on purpose (this is the *only* fake part of the flow):**
- 🟡 Analysis logic — emits **one hard-coded finding** grouped under `DUMMY-001`; no parsing,
  no LLM (`AnalyzeUpload.cs`). File content is stored but not interpreted.

**Not yet in this slice:**
- ⬜ Golden-file / integration test asserting fixture upload → cited finding on the read endpoint

---

## Phase 2 — Real Orbit ingest / parsing ⬜

Turn the opaque-bytes stub into a real parser written against Orbit's export shape.

- ⬜ Parse structured Orbit exports (CSV / Excel / XML against the published XSD)
- ⬜ Map to the input categories in `docs/samples/` (identifier, scope/WBS, timeline, budget,
  resources, time used, risks, issues, decisions)
- ⬜ Real `projectKey` = Orbit project id (replaces the `DUMMY-001` grouping)
- ⬜ Parse-status reporting ("which files parsed cleanly, which need mapping" — PRD user story #1)
- ⬜ Golden-file tests per template using anonymized real Orbit samples

---

## Phase 3 — Analysis: agent pipeline + LLM ⬜

The suggested 9-agent pipeline (PRD marks the exact split as "Assumption but not decided").

- ⬜ LLM client abstraction (runtime TBD: Claude Agent SDK vs. Semantic Kernel)
- ⬜ Orchestrator + per-skill prompt registry (one orchestrator + N skills, not N services)
- ⬜ Agents: Data Collector, Data Quality, Status, Risk & Issue, Financial, Resource, Narrative
- ⬜ **Challenge** + **Review** agents (the adversarial trust layer — PRD user story #9)
- ⬜ Citations propagate through every agent-produced finding
- ⬜ LLM-over-text path for unstructured meeting minutes
- ⬜ Duplicate-identity detection surfaced for PMO confirmation (PRD user story #2)

---

## Phase 4 — Health scoring ⬜

- ⬜ YAML-driven weighted score + RAG bucketing (path TBD)
- ⬜ Override-rule engine (e.g. critical milestone missed → minimum Amber)
- ⬜ Exposed as a query over the findings store
- ⬜ Table-driven tests (weights, each override, precedence)
- ⚠️ All weights/thresholds/overrides in the PRD are marked **"EXAMPLE!"** — final numbers are
  agreed with the client's PMO at kick-off before scoring goes live.

---

## Phase 5 — Dashboards ⬜ / 🟡

- 🟡 **Level 2 — Individual Project Status** — read endpoint + minimal view exist; the rich
  view (progress, deviations, milestones, decisions, AI recommendation, confidence) is not built
- ⬜ **Level 1 — Executive Portfolio Summary** (G/A/R counts, financial exposure, intervention list)
- ⬜ **Level 3 — Data Quality** (missing/inconsistent items + remediation)
- ⬜ Confidence level surfaced per project (PRD user story #6)

---

## Phase 6 — Auth / role extensions ⬜

- ⬜ Additional roles: `pmoAdmin`, `pmoUser`, `executive` (audience "Other?" confirmed at kick-off)
- ⬜ Role-scoped authorization on the new endpoints

---

## Out of POC scope 🔒

- 🔒 Automated data load — Orbit **GraphQL pull** (plan item #13; POC uses manual upload only)
- 🔒 Raw/findings **store split**, landing-zone TTL / retention (deferred; may share a store)
- 🔒 A first-class `Project` entity (findings group by opaque `projectKey` for now)
- 🔒 Multi-tenancy (single-client POC)
- 🔒 Project-type library, Lessons-Learned exchange, ML-over-time, orchestration layer

---

## Open questions carried from the PRD

These block *decisions*, not the current build:

- What lives in Orbit vs. outside it — especially risks / issues / decisions / minutes
  (top kick-off decision: how much is deterministic scoring vs. LLM-over-text)
- LLM runtime choice (Claude Agent SDK vs. Semantic Kernel)
- Health-scoring weights & override rules (client PMO sign-off)
- Raw-upload retention policy / storage-split posture
- Hosting region / data-residency (minutes contain named individuals)
