## Why

The walking skeleton (`add-ingest-findings-skeleton`) proved the architecture: upload → analyze → cited finding → read walks every layer, and the finding→citation invariant is real. But `AnalyzeUpload` is a stub — it emits one hard-coded finding under `DUMMY-001`, the only fake part of the flow. This change replaces that stub with the real analysis layer: the **9-agent pipeline** (PRD Solution §2; plan item #6, *"Assumption but not decided"*) that turns uploaded project data into **cited findings + a narrative + adversarial-review outputs**, including the Challenge/Review trust layer that is NextWave's differentiator.

**Architecture (from the agreed brief):** the 9 agents split by role and LLM footprint. **Only 4 of 9 touch the LLM** (#4 partial, #7 Narrative, #8 Challenge, #9 Review); **5 of the 6 data & analysis agents are pure C#**. This slice ships the deterministic layer fully and wires the LLM agents through a **`FakeLlmClient`** — so the orchestrator demos end-to-end on dummy fixtures **without an API key**. The real `ILlmClient` adapter is a one-file swap in the *next* change, once the LLM runtime is chosen at kick-off (gap §3.1).

## What Changes

- Add an **analysis orchestrator** in Application driving a defined data flow: `#1 Data Collector → #2 Data Quality → parallel(#3 Status, #4 Risk & Issue, #5 Financial, #6 Resource) → merged findings → #7 Narrative → #8 Challenge → #9 Review → persist all`. Sequential where dependencies exist (#7→#8→#9), parallel where independent (#3–#6).
- Add skill interfaces `IAgentSkill<TInput, TOutput>`; all 9 agents are **skills in Application driven by one orchestrator — not N services** (PRD decision).

**Data & analysis layer — pure C#, fully implemented this slice:**
- **#1 Data Collector** — parse uploaded files into **typed records** (`Project`, `Milestone`, `BudgetLine`, `Assignment`, `MinuteEntry`, `RaidItem`) via OpenXml/EPPlus (Excel), `System.Xml` (Orbit XML), OpenXml (`.docx` minutes). Each record carries a **source locator** so downstream findings can cite it. Parses the **dummy Orbit-shaped fixtures** — hardened real-Orbit parsers stay deferred (see Out of scope). Records are transient run models, not persisted.
- **#2 Data Quality** — rules + fuzzy string match: missing fields, stale updates, inconsistent IDs → DQ findings **plus a confidence signal** consumed downstream.
- **#3 Status** — milestone adherence, schedule variance, delay severity, upcoming/dependency risk (math over dates).
- **#5 Financial** — budget/forecast variance, burn rate, budget-vs-progress cross-signal, financial exposure (math).
- **#6 Resource** — allocation variance, capacity pressure, missing roles, concentration × absence (math).

**Trust + hybrid layer — wired via `FakeLlmClient` this slice (shape real, behavior stubbed):**
- **#4 Risk & Issue (hybrid)** — deterministic RAID-record filtering **+** LLM extraction of risks from unstructured meeting minutes.
- **#7 Narrative (LLM)** — prose synthesis: overall status + recommendation (owner / deadline / rationale).
- **#8 Challenge (LLM, hybrid)** — adversarial critique of findings + narrative (weak claims, unsupported numbers, alt interpretations, missing caveats) with deterministic checks for broken evidence links / stale data. Reads #7 + findings.
- **#9 Review (LLM, hybrid)** — predicts stakeholder questions **grouped by audience** (executive / sponsor / data lead / peer PM). Reads #7 + #8 + findings. **Not a keep/drop gate — all outputs persist.**

**Ports, schema, surface:**
- Add **`ILlmClient` port** (Application) — vendor SDK behind the port, **structured JSON output for every LLM call** (tool-use / `response_format`), no free-text parsing. Ship **`FakeLlmClient`** (fixture responses) only; no real adapter this slice.
- Add a **prompt registry** — prompt files in the repo, **versioned by content hash**.
- **`Finding` schema — additive**, resolving the four schema-touching prerequisites (gap §2.1–§2.3, §2.12): `Confidence`, `PromptVersion` (prompt content hash), `RunId` (re-analysis **appends** under a new run id — never silent overwrite), producing-agent + finding `kind` (analysis / narrative / challenge / review), and an **extended `Citation`** (structured excerpt + text snippet, both nullable, atop `UploadId`/`Locator`).
- **Replace** the stub in `AnalyzeUpload` so `POST /api/analyze/{uploadId}` drives the orchestrator (still synchronous, still a separate seam). `projectKey` = parsed project id (deterministic fallback when absent).
- Extend `GET /api/projects/{projectKey}` to return **findings + narrative + challenge + review**, and the **React Level-2 view** to render four sections (KPI / Narrative / Challenge / Review).

## Capabilities

### New Capabilities
- `analysis-pipeline`: The orchestrator + `IAgentSkill<>` skills — the pure-code data & analysis layer (#1 parse→typed records, #2 DQ + confidence, #3/#5/#6 deterministic analysis, #4 hybrid RAID + minutes), the LLM trust layer (#7 Narrative, #8 Challenge, #9 Review), the `ILlmClient` port + `FakeLlmClient` + prompt registry (content-hash versioned), the defined data flow, structured-JSON output contract, and citation/provenance propagation through every stage.

### Modified Capabilities
- `project-findings`: The analyze requirement changes from a hard-coded stub to driving the `analysis-pipeline`; findings gain provenance (producing agent, confidence, kind, prompt version, run id) and re-analysis appends under a new run id; the Level-2 read returns findings + narrative + challenge + review; the extended citation shape is recorded.

## Impact

- **New Application slice** `Features/Analysis/` — orchestrator, `IAgentSkill<TInput,TOutput>`, the 9 agent skills (5 deterministic done, #4/#7/#8/#9 via fake), transient typed-record models, prompt registry; `Abstractions/ILlmClient.cs`. `AnalyzeUpload` handler rewired to the orchestrator.
- **New Infrastructure** — file parsers (OpenXml/EPPlus Excel, `System.Xml` Orbit XML, OpenXml `.docx`); **`FakeLlmClient`** returning fixture responses (registered app-wide this slice; real adapter next change). EF configs + migration for `Finding` provenance + extended `Citation` + narrative/challenge/review persistence.
- **Domain** — additive `Finding` fields (producing agent, confidence, kind, `PromptVersion`, `RunId`); **`Citation` extended** (structured excerpt + text snippet, nullable). Typed records live in Application as run-scoped models, not EF entities. `Finding.Create` still enforces the citation invariant.
- **Configuration** — an `Llm` section (provider, model id, per-analysis token budget) + API key via env/secret only (mirrors `Jwt`); unused by the fake but wired for the next change.
- **API / Client** — no new endpoints; `POST /api/analyze/{uploadId}` deepens (same contract); `GET /api/projects/{projectKey}` and the React Level-2 view gain the narrative/challenge/review sections.
- **Dependencies** — OpenXml SDK (+ EPPlus — **flagged: EPPlus is commercial-licensed since v5; ClosedXML is a license-friendly alternative**, decided in tasks). **No LLM runtime package this slice** (fake only).
- **Tests** — deterministic agents unit-tested directly (no LLM); orchestrator control-flow + citation propagation against `FakeLlmClient`; integration test via `TestWebAppFactory` (fake) asserting fixture upload → analyze → findings + narrative/challenge/review on the read endpoint. Live LLM content never asserted in CI (evaluation/snapshot harness is a later change, gap §2.7).
- **Docs** — `docs/roadmap.md` (Phase 3 in progress, deterministic-first + fake LLM this slice; real adapter next) and `docs/gap-project.md` (§1.1 in flight; §2.1–§2.3 + §2.12 resolved here; real parsers §1.2 and eval harness §2.7 still deferred).

## Out of scope (own future changes, per `docs/gap-project.md`)

Real/hardened Orbit parsers (§1.2 — #1 uses dummy fixtures here); the **real `ILlmClient` adapter** + prompt-content tuning + evaluation/snapshot harness (§2.7 — next change, after §3.1); YAML health-scoring engine (§1.3); Level-1 (§1.4) and Level-3 (§1.5) dashboards; the full rich Level-2 view (§1.6); duplicate-identity merge (§1.7); PMO roles (§1.8 — reuse `.RequireAuthorization()`); raw/findings store split (§1.9); Orbit GraphQL pull (§1.10); scheduled/portfolio runs (§2.9); audit log (§2.10); vector search / RAG (§4). Kick-off blockers §3.1 (runtime), §3.6 (skill collapse), §3.7 (Orbit vs outside) gate only the real-LLM agents — the deterministic 5 + orchestrator + fake start now.
