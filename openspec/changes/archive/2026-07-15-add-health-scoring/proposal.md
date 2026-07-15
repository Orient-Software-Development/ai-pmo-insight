## Why

The 9-agent pipeline (Phase 3) produces individual cited findings, but nothing rolls them up into the
single **Red / Amber / Green (RAG) health rating per project** the PRD requires (throughout this change,
"RAG" means the Red/Amber/Green health colour — never retrieval-augmented generation) (dashboards Level-1 is literally
"green/amber/red counts"). The roadmap frames Phase 4 as *"a query over the findings store"* — but the
findings store cannot support it today: a `Finding` carries a free-text `Summary`, `Confidence`,
`ProducingAgent`, and `Kind`, but **no structured health *area* (Schedule/Budget/Risk/…) and no *severity*
(RAG)**. The deterministic agents already compute the severity (budget overrun %, days late, allocation
concentration) and then discard it into prose; the raw typed records that hold the numbers are transient
and never persisted. So scoring is blocked until findings become self-describing.

## What Changes

- **Enrich `Analysis` findings with structured `Area` and `Severity`** — the deterministic agents (#2, #3,
  #5, #6) stop melting the RAG signal into free text and stamp it onto each finding as typed fields. The
  human-readable `Summary` stays. This enrichment also directly serves Phase 5 dashboards (RAG counts).
- **New `health-scoring` capability** — a stateless service that computes a per-project health score by
  reading the **latest analysis run's** `Analysis` findings, grouping by `Area`, applying **YAML-driven**
  weights + thresholds to bucket into RAG, then applying **override rules** (e.g. critical milestone missed
  → minimum Amber). Exposed as a query — decoupled from the analysis pipeline, so re-tuning weights
  re-scores the whole portfolio **without re-running (re-paying for) LLM analysis**.
- **Override precedence = worst-case floor** — when multiple overrides fire, the most severe floor wins
  (minimum Red beats minimum Amber). *(The PRD does not specify precedence; this change decides it.)*
- **Auditable score** (PRD user story #10) — the result records `rawScore`, `rawBucket`, the list of
  `appliedOverrides`, and the `finalBucket`, so "raw score was Green but override forced Amber" is always
  visible.
- **Confidence override needs no new data** — the "data confidence very low → Needs PM Review" rule reads
  the already-persisted `Confidence`.
- **Ship the engine, not the numbers** — every weight, threshold, and override in the PRD is marked
  **"EXAMPLE!"**. This change ships the engine plus a **default YAML config carrying the example values as
  an explicit placeholder**; the real numbers are agreed with the client's PMO at kickoff before scoring
  goes live.

Not in scope: the executive/portfolio dashboard views that consume the score (Phase 5); real client-agreed
weights; any change to how uploads are ingested or analyzed.

## Capabilities

### New Capabilities

- `health-scoring`: The per-project RAG health score — the weighted-area scoring model, YAML-driven
  configuration (weights, RAG thresholds, override rules), override-precedence semantics (worst-case
  floor), the "latest run" scoping rule, the auditable score result shape, and the query surface that
  returns a project's current score. Owns the failure modes for missing/low-confidence data.

### Modified Capabilities

- `project-findings`: The durable finding shape widens — `Analysis`-kind findings additionally carry a
  structured health `Area` and `Severity` (RAG). Existing citation/provenance requirements are unchanged;
  this is additive to the finding record.
- `analysis-pipeline`: The deterministic data-quality and analysis agents requirement widens — agents #2,
  #3, #5, #6 SHALL emit the structured `Area` + `Severity` on the `Analysis` findings they produce
  (surfacing the RAG signal they already compute internally) rather than only encoding it in the summary
  text.

## Impact

- **Code:**
  - `source/AiPMOInsight.Domain/Findings/Finding.cs` — add `HealthArea? Area` and `Severity? Severity`
    (nullable; only `Analysis` findings populate them). New `HealthArea` + `Severity` enums in Domain.
  - `source/AiPMOInsight.Application/Features/Analysis/Agents/` — `StatusSkill`, `FinancialSkill`,
    `ResourceSkill`, `DataQualitySkill` stamp `Area`/`Severity` when creating findings (via `FindingFactory`).
  - New `source/AiPMOInsight.Application/Features/HealthScoring/` — the scoring service, YAML options
    binding, override engine, and a query handler (`ScoreProject`) over `IFindingRepository`.
  - `source/AiPMOInsight.Api/Endpoints/` — a read endpoint exposing a project's current health score.
  - Persistence: EF mapping for the new `Finding` columns (+ migration; project is pre-live so the DB can
    be reset rather than back-filled).
- **Config:** a default `health-scoring.yaml` (or `HealthScoring` appsettings section) shipping the PRD
  "EXAMPLE!" weights/thresholds/overrides as a clearly-labelled placeholder.
- **Tests:** table-driven tests for weights, each override, override precedence, latest-run scoping, and
  the audit-trail shape; agent tests assert the emitted `Area`/`Severity`.
- **Docs:** a `## Health scoring` note in `CLAUDE.md`; a roadmap Phase 4 status flip.
- **Dependencies:** a YAML parser (e.g. `YamlDotNet`) if config is YAML rather than appsettings JSON —
  decision in design.md.
- **Deferred / blocked:** real client-agreed numbers (PMO kickoff); Phase 5 dashboard consumption.
