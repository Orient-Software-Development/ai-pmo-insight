## Context

This is **Phase 3** of the AI PMO Insight POC, building on the archived walking skeleton. Today `AnalyzeUpload` (`source/AiPMOInsight.Application/Features/Findings/AnalyzeUpload.cs`) is a stub: it resolves the upload and emits one hard-coded `Finding` cited to it under `DUMMY-001`. Everything around it is real (upload store, citation invariant, Level-2 read, React view, EF persistence). The stub call is the seam where the orchestrator plugs in.

The analysis layer is the **9-agent pipeline** (PRD Solution §2; plan item #6, *"Assumption but not decided"*). The agreed brief pins the split: **5 of 6 data & analysis agents are pure C#**; only 4 agents touch the LLM (#4 partial, #7, #8, #9). This slice — **"deterministic layer + trust layer with `FakeLlmClient`"** — ships the deterministic agents fully and wires the LLM agents through a fake, so it demos end-to-end on dummy fixtures with no API key. The real vendor adapter is a one-file swap in the next change once the runtime is chosen (gap §3.1).

Constraints (template + PRD + gap register): ports in Application, adapters in Infrastructure, no SDK leakage upward; in-process CQRS mediator; slices mirror the `Widgets`/`Findings` pattern; secrets via env only. Four schema-touching prerequisites (gap §2.1–§2.3, §2.12) are resolved **in this change's first task group** because bolting them on after agents ship forces a migration.

## Goals / Non-Goals

**Goals:**
- Replace the stub with an orchestrator invoking 9 agents as `IAgentSkill<TInput,TOutput>` skills over a defined data flow.
- Fully implement the deterministic agents (#1 parse→typed records, #2 DQ+confidence, #3 Status, #5 Financial, #6 Resource) with no LLM.
- Wire the LLM agents (#4 minutes, #7 Narrative, #8 Challenge, #9 Review) through `ILlmClient` with `FakeLlmClient` — real shapes, stubbed behavior.
- Thread citations from the parser's record locators through every downstream finding; keep the `Finding.Create` citation invariant.
- Persist and expose findings + narrative + challenge + review; render four sections in the Level-2 view.
- Keep the pipeline fully testable: deterministic agents unit-tested directly, LLM path via the fake, no live content asserted in CI.

**Non-Goals (own future changes, gap register):**
- Hardened real-Orbit parsers (§1.2 — #1 targets the dummy fixtures here).
- The **real `ILlmClient` adapter**, prompt-content tuning, and the evaluation/snapshot harness (§2.7 — next change, after §3.1).
- YAML health-scoring engine (§1.3), Level-1 (§1.4) / Level-3 (§1.5) dashboards, full rich Level-2 (§1.6), duplicate-identity merge (§1.7), PMO roles (§1.8), raw/findings store split (§1.9), Orbit GraphQL pull (§1.10), scheduled runs (§2.9), audit log (§2.10), RAG (§4).

## Decisions

**1. New capability `analysis-pipeline`; `project-findings` modified; `orbit-ingest` untouched.**
Parsing happens at *analyze* time (agent #1 reads the stored bytes), so ingest keeps storing opaque bytes and needs no change. The pipeline is a substantial capability (9 agents, a port, a registry, a data flow) and earns its own spec. Alternative — fold parsing into ingest — rejected: it couples upload to parse and breaks the upload≠analyze seam.

**2. Agents are `IAgentSkill<TInput,TOutput>` skills; one orchestrator, not N services.** (PRD decision.)
Each skill has typed input/output. The orchestrator sequences #1→#2, fans #3–#6 out in parallel, then runs #7→#8→#9. Collapsing #3–#6 into fewer skills for the POC (gap §3.6) is registry config, not code. Alternative — one mediator handler per agent — rejected: multiplies wiring for what are role variations and contradicts the PRD.

**3. `ILlmClient` port + `FakeLlmClient` only this slice; structured JSON output always.**
The port exposes schema-constrained completion; every LLM call uses tool-use / `response_format` so there is no free-text parsing. The only registered implementation is `FakeLlmClient` (fixture responses keyed by agent/skill). The real adapter (Anthropic / Azure OpenAI / OpenAI direct — kick-off §3.1) lands next change without touching Application. Alternative — build a real adapter now — rejected: blocked on §3.1 and needs an API key to demo; the fake unblocks the whole orchestrator today.

**4. Typed records are transient; only findings + narrative/challenge/review persist.**
`Project`, `Milestone`, `BudgetLine`, `Assignment`, `MinuteEntry`, `RaidItem` are run-scoped Application models (PRD: *"parsed intermediate structures may be cached during a run but are not part of the durable domain model"*). Each record carries a **source locator** so the finding derived from it cites the exact origin — this is how a deterministic math finding (e.g. Financial over a `BudgetLine`) gets a real citation.

**5. `Finding` schema — additive provenance (resolves gap §2.1–§2.3, §2.12).**
Add `Confidence`, `PromptVersion` (prompt content hash; null for deterministic agents), `RunId`, producing-agent, and a finding `Kind` (analysis / narrative / challenge / review). Extend `Citation` with a nullable structured excerpt + text snippet atop `UploadId`/`Locator`. Re-analysis **appends** under a new `RunId` (never silent overwrite) — the finding lifecycle decision (§2.3). One additive EF migration, auto-applied in Dev via `DbInitializer`.
- **Confidence methodology (§2.1) — POC decision:** a shared `ConfidencePolicy` derives High/Medium/Low from Data Quality's signals (last-update age, missing-field count, source-consistency) for deterministic findings; LLM agents may self-report but are **capped by the underlying data's DQ confidence**, so the scale is comparable across agents. Final formula confirmed at kick-off; the *field and policy seam* land now.

**6. Challenge and Review persist their outputs; neither is a keep/drop gate.**
Per the brief, Challenge attaches a critique (reads #7 + findings) and Review attaches audience-grouped anticipated questions (reads #7 + #8 + findings). **All outputs persist** — the trust story is that the reader *sees* the critique and likely questions, not that findings are silently dropped. Modeled as findings of `Kind = challenge/review` referencing the findings/narrative they concern (single aggregate, one read query). Alternative — separate entities — deferred; the discriminator is cheaper and the read stays one query.

**6b. Narrative (#7) is hybrid — template-first, LLM fallback.**
~60–70% of narratives fit a small set of recurring shapes (single-signal RED, two-signal RED with a clear primary/secondary, DQ-driven "Needs PM Review", routine GREEN) that render deterministically from templates; only the ~15% genuinely complex cases (multi-signal cross-referencing, minute-extracted signals) call the LLM. This cuts LLM calls and latency materially with no loss of quality on the common cases, and makes most narratives unit-testable without the fake. A classifier decides template-vs-LLM from the merged findings' signal shape; the LLM path stays the safety net. #7 remains one of the four LLM-touching agents — it just reaches for the LLM less often.

**7. Prompt registry = files in repo, versioned by content hash.**
Prompts for the 4 LLM agents live as files under the Analysis feature; the registry keys each by the hash of its content, and that hash is the `PromptVersion` stamped on findings — so a prompt tweak is traceable to the findings it produced. No DB-stored prompts (PRD).

**8. Synchronous, same endpoint/contract.** `POST /api/analyze/{uploadId}` stays synchronous; internals deepen. The cost/latency budget (below) makes synchronous acceptable at POC scale, and the separate endpoint already allows an async move later without a contract change.

## Cost & latency (illustrative, portfolio of 20 projects, real LLM — not this slice)

| Layer | LLM calls / cycle | Cost | Latency |
|---|---|---|---|
| #1–#3, #5, #6 | 0 | $0 | ms |
| #4 (minutes only — 0 if no minutes) | ~20 | ~$0.30 | ~5s parallel |
| #7 Narrative (hybrid) | ~6 (templates cover ~60–70%) + 1 portfolio | ~$0.10 | ~2s parallel |
| #8 Challenge | ~20 | ~$0.30 | ~5s parallel |
| #9 Review | ~20 | ~$0.15 | ~3s parallel |
| **Total** | **~67** | **~$0.85** | **~12s wall clock** |

This slice runs the fake, so cost is $0 and latency is negligible; the table sizes the seam a per-analysis token budget (`Llm` config) will guard once the real adapter lands.

## Success-criteria linkage (PRD)

| Criterion | Depends on |
|---|---|
| #1 Time saved 50–90% | #7 (draft), #9 (meeting prep) |
| #2 Status quality | #7 (narrative quality) |
| #3 Risk detection | #4 (minutes extraction — "risks not in reports") |
| #4 Data quality | #2 (direct) |
| #5 Trust 80%+ | #8 (adversarial self-critique) |
| #6 Actionability | #7 (recommendation with owner/deadline) |

Skipping any of #4, #7, #8 forfeits a success criterion — hence all are wired (via the fake) in this slice even though their content is tuned later.

## Risks / Trade-offs

- **EPPlus is commercial-licensed since v5** → **Decision: use ClosedXML (MIT) for Excel** — license-friendly, high-level API, no per-seat cost. EPPlus rejected on licensing; raw `DocumentFormat.OpenXml` rejected as unnecessarily low-level for tabular fixtures. `System.Xml` (Orbit XML) and OpenXml (`.docx`) are unencumbered and used directly.
- **A weak fake gives false confidence** → Mitigation: the fake exercises *control flow, shape, and citation propagation* — exactly what unit/integration tests should cover; content quality is the next change's evaluation harness (§2.7).
- **Confidence methodology not finalized (§2.1)** → Mitigation: land the field + `ConfidencePolicy` seam now with a documented POC default; final weights are a kick-off number, swappable without a migration.
- **Deterministic agents drifting toward scoring logic (Phase 4 overlap)** → Mitigation: agents emit *findings*, not the weighted RAG score; the YAML scoring engine stays a separate change (§1.3).
- **Challenge/Review-as-findings could overload the `Finding` aggregate** → Mitigation: a `Kind` discriminator + references; revisit separate entities only if the read query or schema strains.
- **Real-LLM latency/cost at portfolio scale** → Deferred to the real-adapter change; the `Llm` token-budget config seam is added now.

## Migration Plan

One additive EF migration for the `Finding` provenance columns, extended `Citation`, and narrative/challenge/review persistence — auto-applied in Development via `DbInitializer`, a deliberate deploy step in production. No destructive change; existing skeleton findings remain valid (new columns nullable / defaulted). Rollback = revert the change. This slice adds parsing NuGet deps only (no LLM package); the `Llm` config section is added inert for the next change.

## Open Questions

- **Confidence formula (§2.1)** — exact weighting of DQ signals vs. LLM self-report; POC default proposed above, confirmed at kick-off.
- **Skill collapse (§3.6)** — do #3/#5/#6 stay three skills or fewer for the POC? Registry config.
- **#4 scope (§3.7)** — minutes-heavy vs. RAID-heavy depends on what lives in Orbit; the hybrid handles both, split tuned at kick-off.
- ~~**Excel library** — ClosedXML vs. EPPlus (license) vs. raw OpenXml~~ — **RESOLVED: ClosedXML (MIT)** (see Risks / Trade-offs).
- **Narrative/Challenge/Review persistence shape** — `Kind`-tagged findings (chosen) vs. dedicated tables; revisit if it strains.
- **LLM runtime (§3.1)** — Anthropic / Azure OpenAI / OpenAI direct; gates only the real adapter (next change).
