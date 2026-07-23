---
name: "Check doc drift"
description: Verify a prose doc in docs/ against the code it describes. Reports mismatched claims, missing implementations, undocumented behavior.
category: Spec-drift
tags: [spec-drift, docs, verification, llm-check]
---

Check `docs/{{ARGUMENT}}.md` for drift against the code it describes. Prose docs can't be
auto-verified by the Layer 1/2/1.5 sensor stack (which covers `openapi.json` structural drift
only), so this command uses your own reading to catch semantic drift between the doc and the
implementation.

**Input:** the short doc name, e.g. `/check-doc-drift authentication`. Resolves to
`docs/authentication.md`. If the exact path was passed (e.g. `docs/authentication.md`, or
`CLAUDE.md` for the repo-root guidance file — not under `docs/`), use it as-is.

## Doc → code mapping

Read the target doc first. Then read the code files it describes, using this mapping. If the
argument doesn't match, infer from the doc's own content — look for filenames, class names,
endpoint paths, config keys, or symbol references inside the doc, and read those.

| Doc argument | Code files to read |
|---|---|
| `authentication` | `source/AiPMOInsight.Api/Security/*.cs`, `source/AiPMOInsight.Infrastructure/Security/*.cs`, `source/AiPMOInsight.Api/Endpoints/AuthEndpoints.cs`, plus the JWT / Identity / cookie sections of `source/AiPMOInsight.Api/Program.cs` |
| `database` | `source/AiPMOInsight.Infrastructure/Persistence/**/*.cs`, `source/AiPMOInsight.Infrastructure/Migrations/**/*.cs` |
| `analysis-pipeline` | `source/AiPMOInsight.Application/Features/Analysis/**/*.cs` |
| `dashboard-output-formats` | `source/AiPMOInsight.Application/Features/{ExecutivePortfolio,HealthScoring,DataQuality,Findings,Progress}/**/*.cs`, `source/AiPMOInsight.Api/Endpoints/*.cs` |
| `spec-drift` | `tests/AiPMOInsight.Api.Tests/OpenApiDriftTest.cs`, `tests/AiPMOInsight.Api.Tests/OpenApiRuntimeContractTest.cs`, `.github/workflows/ci.yml`, `source/AiPMOInsight.Api/Program.cs` (schema-name transformer + `AddOpenApi` block) |
| `CLAUDE.md` | Focus on §3 "Architecture & critical control points" — the only section with concrete file/symbol claims. `source/AiPMOInsight.Api/Program.cs`, `source/AiPMOInsight.Application/Messaging/*.cs`, `source/AiPMOInsight.Application/Abstractions/*.cs`, `source/AiPMOInsight.Application/DependencyInjection.cs`, `source/AiPMOInsight.Infrastructure/DependencyInjection.cs`, `source/AiPMOInsight.Api/Endpoints/*.cs`, `source/AiPMOInsight.Api/Security/*.cs`, `source/AiPMOInsight.Infrastructure/Security/*.cs`, `source/AiPMOInsight.Infrastructure/Persistence/*.cs` (top-level only, not `Migrations/`), `source/AiPMOInsight.Application/Features/HealthScoring/HealthScoringService.cs` + `HealthScoringOptions.cs`, `source/AiPMOInsight.Infrastructure/Analysis/Llm/RoutingLlmClient.cs` + `ILlmClientFactory.cs`, `tests/AiPMOInsight.Api.Tests/TestWebAppFactory.cs`. Sections 1, 2, 4-7 are prose/commands/process guidance — skip unless a specific quoted claim there is directly contradicted by the code. |
| _anything else_ | Grep the doc for backtick-quoted symbols (class names, methods, paths, config keys) and read the files those live in. |

## Rubric

For each specific, testable claim in the doc, classify:

- **MATCH** — doc claim confirmed by code
- **DRIFT** — doc claim contradicts code, or code has behavior not documented
- **CANNOT VERIFY** — depends on runtime / infra / env not visible in source (say so, don't guess)

Rate drift severity honestly:

- **🔴 MAJOR** — broken contract, security regression, or a claim that would mislead an
  implementer into building the wrong thing
- **🟠 MODERATE** — outdated example, inconsistent terminology within the doc, wrong claim
  name / type / path, alternative solution chosen but not documented
- **🟡 MINOR / MISSING NUANCE** — incomplete doc, missing intentional exception, deferred item
  not marked as deferred

## Rules

- **Quote exact doc text** with section reference (e.g. `§2, payload example`), and **cite the
  exact code `file:line`** that contradicts it.
- **Don't fabricate.** If a claim can't be found in the code, say "not found in the mapped
  files", don't guess. Missing evidence is not the same as drift.
- **Focus on load-bearing invariants** — security, contracts, TTLs, schemas, algorithm choices.
  Skip prose narrative, motivation paragraphs, and "why we chose X" rationale unless the
  rationale itself contradicts current behavior.
- **Be honest about limitations** — you can't verify infra-level claims (HSTS, TLS
  termination, Secrets Manager wiring). Mark those `CANNOT VERIFY`.
- **Suggest a fix** per drift item: update doc / update code / accept as documented divergence.

## Output format

```markdown
# Doc-drift check: `docs/{{ARGUMENT}}.md`

**Match rate:** approximately X% of testable claims confirmed. Y drift items found.

## 🔴 MAJOR DRIFT
For each item:
- **Claim (§X):** `"exact quote from doc"`
- **Actual code:** what the code does — `<file>:<line>`
- **Fix:** update doc | update code | accept + document divergence

## 🟠 MODERATE DRIFT
(same format)

## 🟡 MINOR / MISSING NUANCE
(same format)

## ✅ CONFIRMED MATCH
Spot-check the load-bearing invariants confirmed — one line each.

## ⚫ CANNOT VERIFY
Infra / runtime claims outside source visibility.

## Verdict
1-2 sentences: overall accuracy assessment + biggest risk from any drift found.
```

## Cost + expectations

- One check on a mid-size doc (~500 lines) + 3-5 code files runs in ~30-60s of your time and
  costs ~$0.05-0.20 in tokens.
- Findings are **advisory** — this is Layer 3-style semantic drift detection, non-deterministic
  by nature. Two runs on the same doc may surface slightly different findings.
- Reviewer should treat the report as a starting checklist, not a verdict. False positives are
  possible (esp. on rationale/motivation prose).
- To make findings binding, encode confirmed drift as either a doc update or an integration
  test (see `SharedWorkspaceInvariantTests.cs` / `AuthTokenExposureInvariantTests.cs` for the
  "invariant as test" pattern).
