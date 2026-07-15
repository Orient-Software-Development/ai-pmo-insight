## Context

Phase 3 (`add-analysis-agent-pipeline`, archived) shipped the 9-agent pipeline. A `Finding`
([`Finding.cs`](../../../source/AiPMOInsight.Domain/Findings/Finding.cs)) persists `ProjectKey`,
free-text `Summary`, `Kind` (Analysis/Narrative/Challenge/Review), `Confidence` (Low/Medium/High),
`ProducingAgent`, `RunId`, and a mandatory `Citation`. Re-analysis **appends** findings under a new
`RunId` (prior runs retained). The typed records the agents parse (`CollectedData`) are **transient** —
never persisted.

The deterministic agents already compute severity-like signals and discard them into prose:
`FinancialSkill` computes `overPercent = (Forecast-Budget)/Budget*100`; `StatusSkill` has a
`Severity(days)` bucket; `ResourceSkill` counts assignment concentration. None of it survives as a
structured field — only the free-text `Summary`. There is no health *area* tag and no *severity* on a
finding; `Confidence` is *trust*, not *badness*.

The PRD's health scoring (all values marked **"EXAMPLE!"**) is a weighted score across areas
(Schedule 20% … Data quality 5%), bucketed 80–100 Green / 60–79 Amber / 0–59 Red, then adjusted by
override rules (critical milestone missed → min Amber; forecast overrun >15% → min Amber/Red; critical
unmitigated risk → min Red; key decision overdue → min Amber; data confidence very low → "Needs PM
Review"). PRD user story #10 requires the score be auditable when an override changed the rating.

**Terminology:** "RAG" here always means the Red/Amber/Green health colour, never retrieval-augmented
generation.

## Goals / Non-Goals

**Goals:**

- Compute a single per-project RAG health score from the analysis outputs, with the weighting/thresholds/
  overrides all externalised to swappable config (the numbers are the client's, not ours).
- Make findings self-describing (structured `Area` + `Severity`) so scoring — and later dashboards — read
  a signal, not prose.
- Make the score **auditable**: raw score, raw bucket, which overrides fired, final bucket.
- Allow re-tuning the config and re-scoring the whole portfolio **without re-running LLM analysis**.

**Non-Goals:**

- Real client-agreed weights/thresholds/overrides — deferred to PMO kickoff. This change ships the engine +
  an EXAMPLE-valued default config, explicitly a placeholder.
- The Phase 5 dashboard views that *consume* the score (portfolio/exec/project surfaces).
- Any change to ingest or to how the pipeline analyzes — only the finding *shape* and a new read-side
  scoring service.
- Scoring findings from anything but the **latest** run per project.
- Re-deriving area/severity from free text (regex over `Summary`) — rejected as fragile.

## Decisions

### 1. Enrich findings with structured `Area` + `Severity` (don't re-derive from prose)

**Choice:** Add two nullable fields to `Finding`: `HealthArea? Area` and `Severity? Severity`. Only
`Kind == Analysis` findings populate them (Narrative/Challenge/Review leave them null). New Domain enums:
`HealthArea { Schedule, Budget, Risk, Resource, DataQuality, … }` and `Severity { Green, Amber, Red }`.
The deterministic agents (#2 Data Quality, #3 Status, #5 Financial, #6 Resource) stamp them via
`FindingFactory` when they emit a finding — surfacing the RAG signal they already compute internally.

**Rationale:** the severity is computed today and thrown away; persisting it is strictly better modelling
and serves Phase 5 (RAG counts) too. Re-deriving area from `ProducingAgent` + parsing severity out of
`Summary` text would couple scoring to prose wording — brittle and untestable.

**Alternatives considered:**
- *Map `ProducingAgent` → area, parse severity from `Summary`.* Rejected: regex over human text.
- *Score during the run over transient `CollectedData` (a 10th agent).* See Decision 2 — rejected in favour
  of the query, precisely because enrichment removes the reason to.

### 2. Scoring is a stateless query/service, NOT a pipeline agent

**Choice:** A `HealthScoringService` in the Application layer takes a `projectKey`, loads that project's
**latest-run** `Analysis` findings via `IFindingRepository`, and computes the score. It runs on demand
(read endpoint), not inside `AnalysisOrchestrator`.

```
  Analyze (LLM, expensive, once per upload)  ──▶  Findings (persisted, self-describing)
                                                         │
                          YAML config (weights/overrides)│  ← swap & re-score, no LLM
                                                         ▼
                                                 HealthScoringService ──▶ HealthScore
```

**Rationale:** every number is "EXAMPLE!" and will be tuned repeatedly with the PMO. If scoring lived in
the pipeline, each weight change would force a full (paid) re-analysis. As a query, tuning the config
re-scores the entire portfolio instantly. Enrichment (Decision 1) already put every signal scoring needs
onto the persisted findings, so the "transient records" objection to a query no longer applies.

**Alternatives considered:**
- *10th orchestrator step persisting a `HealthScore` per run.* Rejected: couples score cadence to (costly)
  analysis cadence; poor fit for iterative weight tuning. (Could be layered later as a cache if needed.)

### 3. Scoping — always the latest run per project

**Choice:** Since re-analysis appends under a new `RunId`, the service first resolves the newest `RunId`
for the `projectKey` and scores only those findings. Older runs are ignored (they remain for history).

**Rationale:** mixing stale + fresh findings would double-count. The "latest run" rule keeps scoring
consistent with what the Level-2 view already shows.

### 4. Override precedence — worst-case floor wins

**Choice:** Overrides set a *floor* on the bucket. When several fire, the most severe floor wins
(min Red beats min Amber beats the raw bucket). `Needs PM Review` (from very-low confidence) is a distinct
terminal state that supersedes a colour when triggered. Evaluation order is deterministic and defined in
config.

**Rationale:** the PRD lists overrides but not precedence. A worst-case floor is the safe, defensible
reading for a PMO ("never show greener than the worst hard signal"). *(This is a decision this change
introduces, not a PRD fact.)*

### 5. Auditable result shape (PRD story #10)

**Choice:** `HealthScore` carries: `rawScore` (0–100), `rawBucket` (pre-override RAG), `appliedOverrides`
(ordered list of `{ ruleId, floor, reason, citationRef }`), `finalBucket`, `confidence` (aggregate), and
per-area breakdown `{ area, severity, weight, contribution }`. The read endpoint returns all of it.

**Rationale:** "raw was Green but override X forced Amber" must be visible and traceable to the finding
that tripped it — same trust story as citations on findings.

### 6. Config is external and swappable; ships EXAMPLE defaults

**Choice:** Weights, RAG thresholds, and override rules live in a `HealthScoring` config section — a
default file carrying the PRD "EXAMPLE!" numbers, clearly labelled as placeholder. Format decision (YAML
via `YamlDotNet` vs. appsettings JSON) deferred to implementation; JSON keeps dependencies smaller, YAML
matches the roadmap wording and is friendlier for a PMO to edit. Either is bound to a strongly-typed
`HealthScoringOptions` and validated at startup (weights sum to 100, thresholds ordered).

**Rationale:** the deliverable is the *engine*; the numbers are the client's. Externalising them is what
makes re-tuning (Decision 2) possible.

### 7. Area taxonomy maps (roughly) 1:1 to existing agents

**Choice:** `Status → Schedule`, `Financial → Budget`, `Resource → Resource`, `Risk & Issue → Risk`,
`Data Quality → DataQuality`. Agents that can emit more than one area (e.g. a Status agent producing a
scope note) set `Area` per-finding, not per-agent.

**Rationale:** `ProducingAgent` is *almost* an area already; making `Area` explicit per finding avoids
baking an agent→area assumption into the scorer and lets one agent contribute to multiple areas.

## Risks / Trade-offs

- **[R1] EXAMPLE numbers mistaken for decided.** → Default config is header-commented "PLACEHOLDER — replace
  at PMO kickoff"; proposal + `CLAUDE.md` note say the same; a startup log line states the config is the
  example set until overridden.
- **[R2] An agent forgets to stamp `Area`/`Severity` on an `Analysis` finding.** A null area silently drops
  that finding from the weighted score. → `FindingFactory` requires `Area`+`Severity` for `Kind==Analysis`
  (assert non-null at creation, mirroring the citation invariant); tests assert every analysis finding
  carries them.
- **[R3] Weights don't sum to 100 / thresholds overlap in config.** → Validate `HealthScoringOptions` at
  startup; fail fast with a message naming the offending key (never silently normalise).
- **[R4] "Latest run" ambiguity when multiple projects share one multi-file run.** The per-`projectKey`
  latest-run resolution must key on `(projectKey, max RunId)` — a run can span projects (see
  `add-multi-file-analyze`). → Resolve latest run *within* each project key, not globally.
- **[R5] Severity is a 3-value enum but the weighted formula needs a number.** → Config maps
  Green/Amber/Red → numeric per-area scores (e.g. 100/70/30, all EXAMPLE); the mapping is part of the
  swappable config, not hardcoded.
- **[R6] Overrides need signals not every agent emits yet** (e.g. "critical" milestone, "unmitigated"
  risk). → Where the signal is absent, the override simply cannot fire (documented); enriching agents to
  flag criticality/mitigation is a follow-up, not a blocker for the engine.

## Open Questions

- **Config format**: YAML (`YamlDotNet`, matches roadmap, PMO-friendly) vs. appsettings JSON (no new
  dependency). Decide at implementation.
- **Where "Needs PM Review" sits** relative to the RAG colours in the API contract — a separate boolean/
  status field vs. a fourth bucket value. Leaning separate field so colour and review-state are orthogonal.
- **Numbers themselves** — blocked on the client PMO, same posture as the multi-file export convention.
