## 1. Schema prerequisites (gap §2.1–§2.3, §2.12) — do first, one migration

- [x] 1.1 Extend `Citation` (Domain): add nullable `StructuredExcerpt` (sheet/row/column or field path) and `TextSnippet` atop `UploadId`/`Locator`; keep guards on the mandatory fields (gap §2.12)
- [x] 1.2 Extend `Finding` (Domain): add `Confidence`, `PromptVersion` (nullable, prompt content hash), `RunId`, `ProducingAgent`, and `Kind` (analysis / narrative / challenge / review); keep `Finding.Create` enforcing a non-null citation (gap §2.2)
- [x] 1.3 Define an `AnalysisRun` identity + append-on-re-analysis semantics (new `RunId` per run, prior findings retained) (gap §2.3)
- [x] 1.4 Add a shared `ConfidencePolicy` (Application): High/Medium/Low from DQ signals for deterministic findings; LLM self-report capped by DQ confidence (gap §2.1 — POC default, documented)
- [x] 1.5 EF snake_case configs for the new columns + narrative/challenge/review persistence; one migration `AddAnalysisProvenance` (`--project source/AiPMOInsight.Infrastructure --startup-project source/AiPMOInsight.Api`); commit generated files; confirm Dev auto-migrate via `DbInitializer`

## 2. Skill + LLM abstractions

- [x] 2.1 Define `IAgentSkill<TInput, TOutput>` in Application (typed input/output contract)
- [x] 2.2 Define `ILlmClient` port (Application): schema-constrained completion; every call declares a JSON output contract (tool-use / `response_format`) — no free-text parsing
- [x] 2.3 Add `Llm` options + config section (provider, model id, per-analysis token budget); API key via env/secret only (mirror `Jwt`) — inert this slice, wired for the next change
- [x] 2.4 Add prompt registry (Application): prompt files under `Features/Analysis/Prompts/`, keyed by content hash; the hash is the `PromptVersion` stamped on findings

## 3. Data & analysis layer — deterministic, no LLM (agents #1, #2, #3, #5, #6)

- [x] 3.1 Typed records (transient Application models) with source locators: `Project`, `Milestone`, `BudgetLine`, `Assignment`, `MinuteEntry`, `RaidItem`
- [x] 3.2 **#1 Data Collector** — parse the dummy Orbit-shaped fixtures into typed records: Excel (**ClosedXML, MIT — decided**), Orbit XML (`System.Xml`), `.docx` minutes (DocumentFormat.OpenXml). Parsers live in Infrastructure; skill in Application
- [x] 3.3 **#2 Data Quality** — missing fields, stale updates, inconsistent IDs (fuzzy match) → DQ findings + a confidence signal for downstream
- [x] 3.4 **#3 Status** — milestone adherence, schedule variance, delay severity, upcoming/dependency risk (math over dates) → cited findings
- [x] 3.5 **#5 Financial** — budget/forecast variance, burn rate, budget-vs-progress cross-signal, financial exposure → cited findings
- [x] 3.6 **#6 Resource** — allocation variance, capacity pressure, missing roles, concentration × absence → cited findings
- [x] 3.7 Every deterministic finding cites the record locator it derives from; confidence set via `ConfidencePolicy`

## 4. FakeLlmClient + LLM-wired agents (#4 partial, #7, #8, #9)

- [ ] 4.1 Implement `FakeLlmClient` returning fixture responses keyed by agent/skill; register app-wide this slice (no real adapter)
- [ ] 4.2 **#4 Risk & Issue (hybrid)** — deterministic RAID-record filtering + LLM minutes extraction via the port; LLM path invoked only when minutes content is present (no minutes → 0 LLM calls, fully deterministic); both paths cite their source
- [ ] 4.3 **#7 Narrative (hybrid)** — prose status + recommendation (owner / deadline / rationale) over merged findings. Template-first: a classifier renders recurring shapes (single-signal RED, two-signal RED, DQ-driven "Needs PM Review", routine GREEN) deterministically; LLM fallback (structured-JSON) only for complex/multi-signal/minute-extracted cases
- [ ] 4.4 **#8 Challenge** — adversarial critique of findings + narrative (weak claims, unsupported numbers, alternatives, missing caveats) + deterministic evidence-link/stale-data checks; reads #7 + findings; persists critique (does not delete findings)
- [ ] 4.5 **#9 Review** — anticipated stakeholder questions grouped by audience (executive / sponsor / data lead / peer PM); reads #7 + #8 + findings; persists output (not a keep/drop gate)

## 5. Orchestrator + endpoint

- [ ] 5.1 `AnalysisOrchestrator` (Application): data flow `#1 → #2 → parallel(#3,#4,#5,#6) → merge → #7 → #8 → #9`; sequential where dependent, parallel where independent
- [ ] 5.2 Derive `projectKey` from parsed records; deterministic fallback `upload:<uploadId>` when absent (replaces `DUMMY-001`)
- [ ] 5.3 Persist all outputs under one `RunId`; reject any uncited finding before persist
- [ ] 5.4 Rewire `AnalyzeUpload.Handler` to invoke the orchestrator; keep synchronous; keep `null`→404 for unknown uploads

## 6. Read surface + client

- [ ] 6.1 Extend `GetProjectFindings` + `GET /api/projects/{projectKey}` to return findings + narrative + challenge + review
- [ ] 6.2 Extend the React Level-2 view with four sections: KPI (findings) / Narrative / Challenge / Review

## 7. Tests

- [ ] 7.1 Unit-test the deterministic agents (#1 parse, #2 DQ, #3/#5/#6 math) directly — no LLM; assert findings, locators, confidence
- [ ] 7.2 Orchestrator control-flow tests against `FakeLlmClient`: agent order, parallel fan-out, citation + provenance propagation, `projectKey` fallback
- [ ] 7.3 Integration test via `TestWebAppFactory` (fake LLM): upload fixture → analyze → `GET /api/projects/{projectKey}` returns cited findings + narrative + challenge + review
- [ ] 7.4 Integration test: analyze unknown `uploadId` → 404; unauthenticated analyze/read → 401; re-analysis appends a new `RunId` (prior findings retained)
- [ ] 7.5 Do NOT assert live LLM content in CI (evaluation/snapshot harness is a later change, gap §2.7)

## 8. Docs + verify

- [ ] 8.1 Update `docs/roadmap.md` (Phase 3 in progress: deterministic layer + trust layer via fake this slice; real adapter next) and `docs/gap-project.md` (§1.1 in flight; §2.1–§2.3, §2.12 resolved; §1.2 real parsers and §2.7 eval harness still deferred)
- [ ] 8.2 Run the app, exercise upload → analyze → read end-to-end (API + React 4-section view) on dummy fixtures with `FakeLlmClient`; confirm citations, narrative, challenge, and review render
- [ ] 8.3 Run `openspec validate add-analysis-agent-pipeline` and the full test suite
