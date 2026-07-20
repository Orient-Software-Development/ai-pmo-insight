# Roadmap — AI PMO Insight POC

Status of the POC build. Grounds truth against the code in this repo, the PRD
(`docs/prds/poc-ai-pmo-insight.md`), and the first OpenSpec change
(`openspec/changes/add-ingest-findings-skeleton`).

**Legend:** ✅ done · 🟡 partial / stubbed · ⬜ not started · 🔒 out of POC scope

> Last reviewed: 2026-07-20.

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
> blocked on the client confirming the real export shape (`docs/kickoff-questions.md` §A1). So this
> phase is partly delivered via agent #1, partly deferred.

Turn the opaque-bytes stub into a real parser written against Orbit's export shape.

- ⬜ Parse structured Orbit exports (CSV / Excel / XML against the published XSD)
- ⬜ Map to the input categories in `docs/samples/` (identifier, scope/WBS, timeline, budget,
  resources, time used, risks, issues, decisions)
- ⬜ Real `projectKey` = Orbit project id (replaces the `DUMMY-001` grouping)
- ⬜ Parse-status reporting ("which files parsed cleanly, which need mapping" — PRD user story #1)
- ⬜ Golden-file tests per template using anonymized real Orbit samples

---

## Phase 3 — Analysis: agent pipeline + LLM ✅ (agents + both LLM providers) / 🟡 (2 items below)

The suggested 9-agent pipeline (PRD marks the exact split as "Assumption but not decided") — grown
to **11 agents** since first shipped (Decision and Scope added later, both deterministic).

> **Approach:** OpenSpec change `add-analysis-agent-pipeline` first sliced this as "deterministic
> layer + trust layer with `FakeLlmClient`"; the real `ILlmClient` adapters landed later. Only 4
> agents touch the LLM (#4 partial, #7 Narrative, #8 Challenge, #9 Review); the 7 deterministic
> agents (#1 parse, #2 data quality, #3 status, #5 financial, #6 resource, Decision, Scope) are pure
> C# and ship fully. Data flow: `#1 → #2 → parallel(#3,#4,#5,#6,Decision,Scope) → #7 → #8 → #9 → persist`.

**Shipped — full deterministic layer + trust layer + both real LLM providers:**
- ✅ `ILlmClient` port with **three registered adapters**: `fake` (fixture responses, no API key —
  the demo/test path), `anthropic` (`AnthropicLlmClient`, official Anthropic SDK, structured JSON
  output), `openai` (`OpenAiLlmClient`, official OpenAI SDK, structured JSON output). Provider is
  selected **per-agent via config alone** (`Llm.Default` + `Llm.Agents.<SkillName>` overrides),
  dispatched by `RoutingLlmClient` — no code change to swap providers. Model/provider choice per
  agent is the remaining open item (`docs/kickoff-questions.md` §G1).
- ✅ `AnalysisOrchestrator` + per-skill prompt registry (prompts on disk, **content-hash versioned**;
  one orchestrator + N skills, not N services)
- ✅ Deterministic agents (pure C#, no LLM): Data Collector (#1), Data Quality (#2 + confidence
  signal — later extended with per-risk staleness, duplicate-identity, budget-actuals, resource-vs-
  time, completeness-grid checks), Status (#3 — later extended with milestone slip/critical
  escalation), Financial (#5), Resource (#6 — later extended with cross-project concentration),
  Decision, Scope (display-only POC, excluded from scoring)
- ✅ Narrative (#7) — hybrid template-first, LLM fallback; recommendation persisted structured
  (`owner`/`deadline`/`action`/`rationale` on `MetricDetail`, #48)
- ✅ **Challenge** (#8) + **Review** (#9) agents (the adversarial trust layer — PRD user story #9)
- ✅ Citations + provenance (producing agent, confidence, kind, prompt version, run id) propagate
  through every finding; re-analysis **appends** under a new `RunId` (prior findings retained)
- ✅ Risk & Issue (#4) LLM-over-text path for meeting minutes — fires only when minutes are present;
  runs against a real provider now (extraction *quality* assessment is the evaluation-harness item
  below, not an adapter gap)
- ✅ **Duplicate-identity detection** (PRD user story #2) — a POC similarity heuristic in the Data
  Quality agent, surfaced on the L3 view with a Merge/Keep-separate control that only records the
  choice (client-side, this POC) and never auto-merges (US-2)

**Still open:**
- ⬜ Hardened real-Orbit parsers (`#1` targets dummy fixtures for now) — blocked on the client
  confirming the real export shape (`docs/kickoff-questions.md` §A1)
- ⬜ Evaluation / snapshot harness for LLM output quality — the gate for the PRD's "PMs agree 80%+"
  success criterion; not yet built (drafted as a follow-on GitHub issue)

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
- **Since this phase first shipped:** `HealthArea` grew from 5 to 7 areas — **Decision** (scored,
  weight 10 EXAMPLE) and **Scope** (display-only POC, deliberately *not* scored) were added later.
  See `docs/poc-data-rules-v0.md` §1.1 for the current weight table.

---

## Phase 5 — Dashboards ✅ (all three levels + their follow-on registers closed)

- ✅ **Level 2 — Individual Project Status (rich view)** — `add-project-status-dashboard`. The project
  view now consumes `GET /api/projects/{key}/health` alongside the findings surface and renders the RAG
  status banner (FinalBucket + score), per-area breakdown, aggregate confidence, applied-override audit
  trail, and the "Needs PM Review" flag above the four cited sections. Presentation-only (no backend
  change) at the time this shipped. Dated milestones, per-decision owner-deadline, and the explicit AI
  recommendation it flagged as follow-ons then are **all built now** — see the L2 register-closure
  bullet below.
- ✅ **Level 1 — Executive Portfolio Summary** — `add-executive-portfolio-dashboard`. New portfolio-wide
  read: `IFindingRepository.DistinctProjectKeysAsync` (opaque-key discovery, no `Project` entity) + a
  `ScorePortfolio` slice fanning out over the pure `HealthScoringService`, exposed at `GET /api/portfolio`
  (zeroed 200 on empty store). L1 React view (`/portfolio`) built to the v2 wireframe with a shared design
  system. **Backed & live:** G/A/R counts, aggregate confidence + "Needs PM Review" count, worst-first
  intervention list (status/confidence/reason + cited finding). What this shipment flagged as follow-on
  (€ financial exposure, key-person concentration, decision backlog, owned/dated recommendations) is
  **all built now** (`add-l1-portfolio-signals`, #70/#71) — G/A/R strip, financial exposure roll-up,
  key-person table, decision-backlog count, customer-exposure proxy, and a worst-first recommended-
  actions roll-up (owner/deadline/action) are all live.
- ✅ **Analyze flow UI + L2 retrofit** — `add-analyze-flow-and-l2-retrofit` (#38). New `/upload` cold-start
  page (`Upload.jsx`) extracts the upload → analyze flow out of the L2 view — drop zone, this-upload
  panel, coarse request-lifecycle pipeline stepper — and becomes the post-login landing route. The L2
  view (`/projects`) is retrofitted onto the shared Phase 5 design system: project header (key, RAG chip,
  confidence, score-overridden indicator, project switcher) + styled cited sections; the `?key=` hand-off
  from `/upload` auto-loads the analyzed project. Presentation-only (no backend/data-path change;
  `ProjectStatusDashboardDataTests` stays green). **Still flagged as follow-on:** per-file parse status
  and live per-agent progress (US-9) on `/upload`. **Since built:** duplicate-identity merge (US-2, L3
  register #69) and dated milestones / per-decision detail (L2 register #68) — both flagged here at
  the time, both closed since.
- ✅ **Auth UI rebuild + token-base retrofit** — `add-phase5-auth-ui` (#33). The three auth surfaces
  rebuilt to the wireframe: `Login.jsx` (centered card, tab toggle, register-mode rules hint, red-stripe
  error), `ChangePassword.jsx` (settings card, green success reveal restating fresh-session behaviour,
  `navigate(-1)` cancel with `/` fallback), and `NavMenu.jsx` (avatar-chip trigger opening a **disclosure**
  panel — not ARIA menu — with header + Change password + danger-styled Log out; closes on outside
  mousedown / Escape / route change; nav tabs hidden on `/login` via `useLocation`). The wireframe token
  base (`--paper` / `--ink*` / `--panel*` / `--rule*` / `--accent*` / `--sev-*` / `--font-display` +
  `--font-ui` + `--font-mono`) lands as CSS custom properties in `styles.scss`, both themes, hybrid over
  Pico (Pico keeps form/reset/button primitives). L1 and L2 retrofitted in the same change to consume
  the new tokens — no JSX or data-path change; `ProjectStatusDashboardDataTests` +
  `ExecutivePortfolioEndpointsTests` + `AuthEndpointsTests` all stay green. Presentation-only — no
  `/api/auth/*`, cookie/JWT, or Identity change.
- ✅ **History rich detail** — `add-history-rich-detail` (#36). `/history` rebuilt as a master-detail audit
  surface (US-9/US-10): master list + detail (run-provenance header, four cited sections, score audit reusing
  `GET /api/projects/{key}/health` per project — labelled current, per-run historical audit a follow-on).
  Presentation-only (no backend change). Flagged follow-ons: uploader, LLM model, project count, multi-file,
  live status.
- ✅ **Level 3 — Data Quality** — `add-data-quality-dashboard` (#35). New portfolio-wide read: a
  `SummarizeDataQuality` slice reusing `DistinctProjectKeysAsync` (enumeration) + the pure
  `HealthScoringService` (confidence), exposed at `GET /api/data-quality/summary` (zeroed 200 on empty
  store). L3 React view (`/data-quality`) built to the v2 wireframe on the shared design system.
  **Backed & live:** confidence hero (mean confidence + configured publish threshold `ConfidenceFloor` +
  below-target flag) and the worst-first **cited** missing/inconsistent items table (project · issue ·
  severity). What this shipment flagged as follow-on (per-item age, suggested remediation, confidence-lift
  ordering, the areas-completeness grid, duplicate-identity candidates) is **all built now** — see the L3
  register-closure bullet below.
- ✅ **L2 follow-on register closed** (register #68, PRs #72/#73). `FindingView` now exposes each
  finding's `Area`/`Severity`/`MetricValue`/`MetricUnit`/`MetricDetail` — the shared enabler every panel
  below consumes. **Decisions needed**: dedicated worst-first table (owner/deadline/consequence) from
  `DecisionSkill`. **Key deviations**: grouped by area (Budget/Time/**Scope**/Resources), with Risks &
  Data quality as their own sections. **Scope**: `HealthArea.Scope` + `ScopeSkill`, a POC
  "unapproved-creep" rule, **display-only** (excluded from scoring). **Upcoming milestones**: dedicated
  dated panel, window widened 14→28 days; milestones carry `BaselineDate`/`IsCritical` — a critical
  milestone in trouble escalates to Red; slip is display-only info. **This-period progress**:
  `SummarizeProgress` slice + `GET /api/projects/{key}/progress` — run-over-run score delta, a
  qualitative pace label (POC thresholds), and moved-forward/moved-backward lists. All POC numbers
  flagged `IsPlaceholder`/"to be confirmed" per `docs/poc-data-rules-v0.md`.
- ✅ **L3 follow-on register closed** (register #69, all 8 items). `DataQualityOptions`
  (config-bound, `Validate()`d at startup — mirrors `HealthScoringOptions`) externalises the new POC
  thresholds. **Age** + **suggested remediation**: real `MetricValue`/`MetricUnit` + a static
  check-type → fix rule-map (no LLM). **Duplicate-identity candidates** (US-2): POC similarity
  heuristic, Merge/Keep-separate only **records** the choice (client-side, this POC) and never
  auto-merges. **Per-risk staleness**: a RAID item not updated within 21 days (config) is flagged.
  **Budget-actuals-missing**: `BudgetLineRecord.Actual` now nullable, a missing actual is a DQ gap.
  **Confidence-lift ordering**: items ranked by a **global** (portfolio-wide) counterfactual
  re-evaluation of `ConfidencePolicy` — a design decision, not a client input. **Areas-completeness
  grid**: 8 input categories (not the 5 `HealthArea` buckets), POC mandatory-field set, informational
  only. **Resource-plan vs. time-entries**: a new POC `TimeEntryRecord` + parser "Time" sheet backs a
  cross-source consistency check. Confidence level surfaced per project (PRD user story #6) is ✅ —
  live since the original L2 build (`ConfidencePolicy` / `HealthScore.Confidence`).

---

## Phase 6 — Auth / role extensions ⬜

- ⬜ Additional roles: `pmoAdmin`, `pmoUser`, `executive` (audience "Other?" — see
  `docs/kickoff-questions.md` §J1/§J2)
- ⬜ Role-scoped authorization on the new endpoints

---

## Out of POC scope 🔒

- 🔒 Automated data load — Orbit **GraphQL pull** (plan item #13; POC uses manual upload only)
- 🔒 Raw/findings **store split**, landing-zone TTL / retention (deferred; may share a store — see
  the retention/storage-split GitHub issue draft for the post-POC design)
- 🔒 A first-class `Project` entity (findings group by opaque `projectKey` for now)
- 🔒 Multi-tenancy (single-client POC)
- 🔒 Project-type library, Lessons-Learned exchange, ML-over-time, orchestration layer

---

## Open questions carried from the PRD

These block *decisions*, not the current build. Full detail (current POC placeholder + what to ask)
now lives in **`docs/kickoff-questions.md`** — kept there, not duplicated here, so the two don't
drift apart. Index, by section:

- **§A** — what Orbit data actually looks like: export format/tab-naming (top question, blocks
  hardened parsing + `add-multi-file-analyze`), milestone baseline/critical flags, scope-change log,
  decision fields, customer/currency columns, per-period exports.
- **§B–§F** — the health-scoring model, per-agent thresholds, Scope's real rule, progress-pace
  thresholds, and time windows — all shipped as POC placeholders pending PMO sign-off.
- **§G** — LLM provider **per agent** (Anthropic vs. OpenAI — both adapters are built; this is a
  model/config choice, not a runtime-framework choice) + data-scoping model.
- **§H/§I** — L1 commercial-risk data, L3 duplicate-relevance + mandatory-field set.
- **§J** — product/strategy: PMO roles, audience, analysis scheduling policy, retention, hosting
  region, the AI-skills catalogue, and the differentiator vs. Orbit-native tooling.
