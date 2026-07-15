# Roadmap — AI PMO Insight POC

Status of the POC build. Grounds truth against the code in this repo, the PRD
(`docs/prds/poc-ai-pmo-insight.md`), and the first OpenSpec change
(`openspec/changes/add-ingest-findings-skeleton`).

**Legend:** ✅ done · 🟡 partial / stubbed · ⬜ not started · 🔒 out of POC scope

> Last reviewed: 2026-07-10.

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
- ✅ History read surface — `GET /api/uploads` (list, newest-first) + `GET /api/uploads/{id}/findings`
  (latest run, four sections); React `/history` page (`add-upload-history`: `GetUploads.cs`,
  `GetUploadFindings.cs`, `UploadHistoryEndpoints.cs`, `History.jsx`). Shared-workspace, view-only.
- ✅ Domain: `Finding` aggregate + mandatory `Citation` value object
  (`Finding.cs`, `Citation.cs`) — every finding cites its source upload
- ✅ Persistence: `Upload` + `Finding` EF entities + `InitialCreate` migration, auto-migrated in Dev
- ✅ Minimal read-only React view — `components/ProjectFindings.jsx`
- ✅ Dummy Orbit-shaped sample fixtures — `docs/samples/` (one file per input category)

**Superseded by Phase 3 (`add-analysis-agent-pipeline`):**
- ✅ The `DUMMY-001` hard-coded-finding stub is gone — `AnalyzeUpload` now drives the real
  orchestrator, and `projectKey` derives from the parsed source (deterministic `upload:{id}` fallback).
- ✅ Integration test asserting fixture upload → analyze → cited findings + narrative/challenge/review
  on the read endpoint (`FindingsFlowTests.cs`).

---

## Phase 2 — Real Orbit ingest / parsing 🟡 partial

> **Approach (2026-07-10):** deterministic **parsing is agent #1 (Data Collector)** of the Phase 3
> pipeline, built against the **dummy Orbit-shaped fixtures** in `add-analysis-agent-pipeline`.
> That covers CSV/Excel/XML/`.docx` → typed records with source locators. **Hardened real-Orbit
> parsing** (full XSD, anonymized real exports, parse-status reporting) stays a later change
> (gap `docs/gap-project.md` §1.2). So this phase is partly delivered via agent #1, partly deferred.

Turn the opaque-bytes stub into a real parser written against Orbit's export shape.

- ⬜ Parse structured Orbit exports (CSV / Excel / XML against the published XSD)
- ⬜ Map to the input categories in `docs/samples/` (identifier, scope/WBS, timeline, budget,
  resources, time used, risks, issues, decisions)
- ⬜ Real `projectKey` = Orbit project id (replaces the `DUMMY-001` grouping)
- ⬜ Parse-status reporting ("which files parsed cleanly, which need mapping" — PRD user story #1)
- ⬜ Golden-file tests per template using anonymized real Orbit samples

---

## Phase 3 — Analysis: agent pipeline + LLM 🟡 in progress

The suggested 9-agent pipeline (PRD marks the exact split as "Assumption but not decided").

> **Approach (2026-07-10):** OpenSpec change `add-analysis-agent-pipeline`, sliced as
> **"deterministic layer + trust layer with `FakeLlmClient`"**. Only 4 of 9 agents touch the LLM
> (#4 partial, #7 Narrative, #8 Challenge, #9 Review); the 5 deterministic agents (#1 parse,
> #2 data quality, #3 status, #5 financial, #6 resource) are pure C# and ship fully. The LLM
> agents are wired through a **fake client** so it demos end-to-end on fixtures with no API key;
> the **real `ILlmClient` adapter is the next change** (after the runtime is chosen at kick-off,
> gap §3.1). Data flow: `#1 → #2 → parallel(#3,#4,#5,#6) → #7 → #8 → #9 → persist`.

**Shipped this slice (`add-analysis-agent-pipeline`) — deterministic layer + trust layer via `FakeLlmClient`:**
- 🟡 `ILlmClient` port defined; only `FakeLlmClient` (fixture responses) registered — no API key
  needed. **Real runtime adapter is the next change** (after §3.1; runtime TBD: Claude Agent SDK vs. Semantic Kernel)
- ✅ `AnalysisOrchestrator` + per-skill prompt registry (prompts on disk, **content-hash versioned**;
  one orchestrator + N skills, not N services). Data flow `#1 → #2 → parallel(#3,#4,#5,#6) → #7 → #8 → #9 → persist`
- ✅ Deterministic agents (pure C#, no LLM): Data Collector (#1), Data Quality (#2 + confidence signal),
  Status (#3), Financial (#5), Resource (#6)
- ✅ Narrative (#7) — hybrid template-first, LLM fallback via the fake client
- ✅ **Challenge** (#8) + **Review** (#9) agents (the adversarial trust layer — PRD user story #9) — via fake client
- ✅ Citations + provenance (producing agent, confidence, kind, prompt version, run id) propagate
  through every finding; re-analysis **appends** under a new `RunId` (prior findings retained)
- 🟡 Risk & Issue (#4) LLM-over-text path for meeting minutes — wired via the fake client (fires only
  when minutes are present; real extraction quality pending the real adapter)

**Deferred to later changes:**
- ⬜ Hardened real-Orbit parsers (gap §1.2 — #1 targets dummy fixtures for now)
- ⬜ Evaluation / snapshot harness for LLM output quality (gap §2.7)
- ⬜ Duplicate-identity detection surfaced for PMO confirmation (PRD user story #2; gap §1.7)

---

## Phase 4 — Health scoring ✅ (engine; EXAMPLE numbers client-pending)

> **Delivered (2026-07-14, `add-health-scoring`):** the scoring **engine** ships and is tested;
> only the **numbers** remain client-pending (see the ⚠️ below).

- ✅ Config-driven weighted score + RAG bucketing — **appsettings JSON**, not YAML (`HealthScoringOptions`,
  bound + validated at startup, failing fast on bad weights/thresholds)
- ✅ Override-rule engine — worst-case floor precedence (e.g. critical milestone missed → minimum Amber;
  min Red beats min Amber; a floor never lowers; absent signals don't fire)
- ✅ Exposed as a query over the findings store — re-runnable without re-analysis
  (`ScoreProject` slice, `GET /api/projects/{projectKey}/health`)
- ✅ Findings enriched with structured `HealthArea` + `Severity` (agents #2/#3/#5/#6 + RAID); persisted
  (`AddFindingAreaSeverity` migration)
- ✅ Auditable result (`rawScore`/`rawBucket`/`appliedOverrides`/`finalBucket`/`confidence`/area breakdown)
  + "Needs PM Review" on low confidence
- ✅ Table-driven tests (weights, bucketing boundary, each override, precedence, confidence, audit shape)
- ⚠️ All shipped weights/thresholds/overrides are the PRD's **"EXAMPLE!"** placeholders
  (`IsPlaceholder:true`, startup warning) — final numbers are agreed with the client's PMO at kick-off
  before scoring goes live.

---

## Phase 5 — Dashboards ⬜ / 🟡

- ✅ **Level 2 — Individual Project Status (rich view)** — `add-project-status-dashboard`. The project
  view now consumes `GET /api/projects/{key}/health` alongside the findings surface and renders the RAG
  status banner (FinalBucket + score), per-area breakdown, aggregate confidence, applied-override audit
  trail, and the "Needs PM Review" flag above the four cited sections. Presentation-only (no backend
  change). Dated milestones / per-decision owner-deadline / explicit AI recommendation exceed the current
  finding shape and are flagged in-view as a follow-on, not built.
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
- **Upload spreadsheet shape + tab naming** — one workbook with multiple named tabs, or one
  workbook per category? And what exact sheet/tab names (or naming rule) does the parser map to
  each category? Names may be localized / PM-free-typed, so exact-match is brittle. Shapes the
  Data Collector / parser contract and the `add-multi-file-analyze` merge model. See
  `docs/gap-project.md` §2.12 (design) + §3.11 (client decision).
- LLM runtime choice (Claude Agent SDK vs. Semantic Kernel)
- Health-scoring weights & override rules (client PMO sign-off)
- Raw-upload retention policy / storage-split posture
- Hosting region / data-residency (minutes contain named individuals)
