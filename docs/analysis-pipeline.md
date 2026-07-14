# Analysis Pipeline

How one upload is turned into findings. Describes the runtime shape of
[`AnalysisOrchestrator`](../source/AiPMOInsight.Application/Features/Analysis/AnalysisOrchestrator.cs);
edit in place as the pipeline changes.

## The 9 agents

Every run walks the same nine agents. The `#` numbering is the canonical order used across
CLAUDE.md, the code, and the OpenSpec changes.

| # | Agent | Kind | Reads | Writes |
|---|---|---|---|---|
| 1 | Data Collector | deterministic | Upload bytes | Parsed `CollectedData` |
| 2 | Data Quality | deterministic | One project slice | Quality signal + findings |
| 3 | Status | deterministic | Slice + quality | Findings |
| 4 | Risk & Issue | **LLM** | Slice + quality | Findings |
| 5 | Financial | deterministic | Slice + quality | Findings |
| 6 | Resource | deterministic | Slice + quality | Findings |
| 7 | Narrative | **LLM** | Slice + quality + merged findings | Narrative finding |
| 8 | Challenge | **LLM** | Slice + quality + findings + narrative | Challenge finding |
| 9 | Review | **LLM** | Slice + findings + narrative + challenge | Review finding |

Four of the nine call `ILlmClient` (#4, #7, #8, #9). The other five are pure code — no tokens
consumed, no vendor call.

## Flow

```
                  #1 Data Collector  (runs ONCE per upload — file is parsed once)
                         │
                         ▼
                 ┌────────────────────────────────┐
                 │ for each project in the file:  │
                 │                                 │
                 │   #2 Data Quality              │
                 │        │                        │
                 │        ▼                        │
                 │   parallel(#3 Status,          │
                 │            #4 Risk & Issue,   ← LLM
                 │            #5 Financial,       │
                 │            #6 Resource)        │
                 │        │                        │
                 │        ▼                        │
                 │   merge findings                │
                 │        │                        │
                 │        ▼                        │
                 │   #7 Narrative                ← LLM (depends on merged)
                 │        │                        │
                 │        ▼                        │
                 │   #8 Challenge                ← LLM (depends on #7)
                 │        │                        │
                 │        ▼                        │
                 │   #9 Review                   ← LLM (depends on #7, #8)
                 └────────────────────────────────┘
                         │
                         ▼
                 persist all findings
```

## Fan-out — how many LLM calls per upload?

**Ceiling: `4 × (number of projects)`. Real number is usually lower** — every LLM agent has a
skip-the-LLM path when its input is trivial.

The orchestrator loops over `projectKeys` (see
[AnalysisOrchestrator.cs:54](../source/AiPMOInsight.Application/Features/Analysis/AnalysisOrchestrator.cs#L54))
and runs the full #2–#9 pipeline once per project. Data Collector (#1) is the exception — it runs
once per upload, before the loop, and its output is sliced per project.

### Per-agent skip rules

| Agent | LLM call happens when… | LLM call is skipped when… |
|---|---|---|
| #4 Risk & Issue | Project has meeting minutes (extract from unstructured text) | Project has no minutes — RAID rows go through the deterministic path only |
| #7 Narrative | 3+ analysis findings **or** any finding produced by minute extraction | Fewer than 3 non-minute findings — a template path handles routine GREEN, "needs review", and 1–2-signal RED |
| #8 Challenge | Any analysis finding exists | Zero analysis findings — a placeholder line replaces the critique |
| #9 Review | Any analysis finding exists | Zero analysis findings — a placeholder line replaces the questions |

References:
[RiskAndIssueSkill.cs:47-50](../source/AiPMOInsight.Application/Features/Analysis/Agents/RiskAndIssueSkill.cs#L47-L50),
[NarrativeSkill.cs:41-46](../source/AiPMOInsight.Application/Features/Analysis/Agents/NarrativeSkill.cs#L41-L46),
[ChallengeSkill.cs:44-47](../source/AiPMOInsight.Application/Features/Analysis/Agents/ChallengeSkill.cs#L44-L47),
[ReviewSkill.cs:40-43](../source/AiPMOInsight.Application/Features/Analysis/Agents/ReviewSkill.cs#L40-L43).

### Worked examples

| Projects | Project shape | LLM calls |
|---|---|---|
| 1 | clean project (no findings, no minutes) | 0 |
| 1 | 1 RAID row, no minutes | 0 (Narrative uses template; Challenge/Review skip) |
| 1 | 3 findings + minutes | 4 (all four LLM agents fire) |
| 6 | worst case — every project has minutes and 3+ findings | 24 |
| 6 | typical — mix of clean, small-signal, and complex projects | ~6–12 |

Fallback: if the parser finds zero project keys, a single synthetic key `upload:{id}` is used so
the pipeline still fires once
([AnalysisOrchestrator.cs:48-51](../source/AiPMOInsight.Application/Features/Analysis/AnalysisOrchestrator.cs#L48-L51)).

## Parallelism model

- **Inside one project** — `Task.WhenAll(#3, #4, #5, #6)`
  ([lines 79-83](../source/AiPMOInsight.Application/Features/Analysis/AnalysisOrchestrator.cs#L79-L83)).
  The Risk & Issue LLM call (#4) runs at the same wall-clock as the three deterministic agents.
- **Between #7 → #8 → #9** — sequential. Each depends on the previous, so no overlap possible.
- **Across projects** — sequential `foreach`
  ([line 54](../source/AiPMOInsight.Application/Features/Analysis/AnalysisOrchestrator.cs#L54)).
  Project 2 waits for project 1 to finish end-to-end.

Wall-clock time for an upload therefore scales roughly linearly with project count. For a
6-project file, wall-clock ≈ 6 × (single-project time). Parallelising the top-level `foreach` is
safe for correctness (projects are independent) but multiplies vendor rate-limit and cost pressure
by the same factor — not the default.

## Cost implications

Every extra project multiplies the four LLM calls, so cost scales with project count and — more
importantly — with the *most expensive* agent's configuration.

At the current default `appsettings.Development.json` (Risk & Issue, Narrative, Review on
`gpt-4o-mini`; Challenge on Anthropic Sonnet with adaptive thinking), the Challenge agent
dominates the per-project cost. To keep costs bounded on multi-project files:

- **Cheapest:** route Challenge to Haiku 4.5 with `EnableExtendedThinking: false`. Loses adaptive
  thinking but keeps a capable adversarial critic.
- **Middle:** keep Challenge on Sonnet 5 but set `EnableExtendedThinking: false`. Thinking is
  the expensive component; the underlying Sonnet response is still high quality.
- **Highest quality:** current defaults. Best for a nightly PMO batch; probably too costly for
  per-user-request runs on large files.

See [../CLAUDE.md](../CLAUDE.md) "LLM routing" for how per-agent overrides are resolved.

## Improvements considered (not yet implemented)

Documented opportunities, not commitments. Weigh each against the current defaults before pulling
the trigger.

### 1. Parallelise across projects

The top-level project loop
([AnalysisOrchestrator.cs:54](../source/AiPMOInsight.Application/Features/Analysis/AnalysisOrchestrator.cs#L54))
is a serial `foreach`. Projects are independent — swapping it for `Parallel.ForEachAsync` with a
bounded `MaxDegreeOfParallelism` (2 or 3) would roughly halve wall-clock on multi-project uploads.

Trade-offs:
- **Cost:** unchanged (same number of calls, just concurrent).
- **Rate limits:** 6 projects × 4 concurrent calls = up to 24 in flight instead of 4. OpenAI
  gpt-4o-mini's per-minute limits absorb this easily; Anthropic tier-1 limits (~40–50 RPM on
  Sonnet, more on Haiku) may briefly 429 on bursts. `MaxDegreeOfParallelism = 2` is a safe start.
- **Debugging:** interleaved log output — the [OTel wiring](../CLAUDE.md) already stamps each log
  with the current trace/span id, so correlation survives.

### 2. Anthropic prompt caching for Challenge / Narrative / Review

Each of these agents sends the *same* instructional prompt (from `PromptRegistry`) once per
project, with only the project-specific findings/narrative text changing. Anthropic offers
`cache_control` markers on the input; a cached prefix cuts input-token cost by ~90% for calls
within the 5-minute cache TTL.

For a 6-project upload where Challenge fires on all six, that's 1 × full + 5 × cached input — a
meaningful cut when the prompt itself is large (thousands of tokens of instruction).

Trade-offs:
- **Adapter change:** `AnthropicLlmClient` would need to mark the prompt prefix with
  `cache_control: { type: "ephemeral" }` on the Anthropic SDK's message parts. Small, contained.
- **Prompt ordering:** already cache-friendly — the current code puts `prompt.Content` first, then
  the volatile per-project data. No prompt-registry changes needed.
- **OpenAI-side:** OpenAI has a separate automatic prompt-caching mechanism for calls with the same
  prefix. It's opt-in nothing on the client side; already applies transparently.

### 3. Batch multiple projects into one call per agent

Alternative: instead of 6 calls to Risk & Issue, one call listing all 6 projects and asking for
per-project structured output. Cuts vendor calls by up to 6x.

Trade-offs — **not recommended** without a specific driver:
- Loses the project-scope invariant (findings would need explicit project labels; wrong labels are
  now a correctness bug).
- Individual project failures would poison the whole batch instead of failing just that project.
- Wall-clock savings are the same as #1 but with much more risk.

### 4. Combine Narrative → Challenge → Review into one prompt

They're sequentially dependent, so 3 round-trips per project. In principle one prompt could request
all three outputs at once ("produce a narrative, then critique it, then predict review questions").

Trade-offs — **not recommended:**
- Loses the adversarial dynamic — the "challenge critiques the narrative" pattern collapses to
  the model self-generating both at once.
- Output shape becomes a nested record; schema and downstream persistence change.
- Small wall-clock win — the three calls fire back-to-back within a few seconds already.

## Correctness invariants

- **Citations required.** Any finding produced by any agent must carry a citation. The orchestrator
  rejects the whole run before persist if it finds even one uncited finding
  ([lines 60-63](../source/AiPMOInsight.Application/Features/Analysis/AnalysisOrchestrator.cs#L60-L63)) —
  defence-in-depth atop the invariant already enforced by `Finding.Create`.
- **Run identity.** Every run gets a fresh `AnalysisRun` id. Re-analysing the same upload appends
  under a new id; findings from prior runs are never overwritten.
- **Project scoping.** Every finding is tagged with a `projectKey`. The 6-project fan-out therefore
  produces six independent finding groups, not one merged blob.
