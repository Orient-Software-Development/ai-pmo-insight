## Why

Today a single `ILlmClient` is registered for the whole app, so the four LLM-backed agents
(`#4 RiskAndIssue`, `#7 Narrative`, `#8 Challenge`, `#9 Review`) all resolve to the same provider
and model. Different agents have different needs — Narrative wants a strong writer, Challenge
wants an aggressive critic, extraction (Risk & Issue over minutes) wants a cheap fast model —
and the PMO needs to A/B providers and models per agent without a redeploy. We already put
`SkillName` on `LlmRequest` for exactly this routing; this change turns that seam on.

## What Changes

- Extend `LlmOptions` to carry a `Default` provider config **and** an `Agents` map keyed by skill
  name (`RiskAndIssue`, `Narrative`, `Challenge`, `Review`), each entry overriding provider/model/
  key/token-budget as needed. Missing agent entry → uses `Default`.
- Introduce a `RoutingLlmClient` in Infrastructure that implements `ILlmClient` and dispatches
  each call to a per-skill inner `ILlmClient` chosen from `LlmRequest.SkillName`. Inner clients
  are built once at startup and cached; the routing is not per-request work.
- Introduce an `ILlmClientFactory` in Infrastructure (single-file provider switch) that maps a
  `LlmProviderOptions` to a concrete `ILlmClient`. In this change it wires `fake` (existing
  `FakeLlmClient`) and registers **stub adapters** (`OpenAiLlmClient`, `AnthropicLlmClient`) that
  throw `NotImplementedException` on `CompleteAsync` — real HTTP calls are a follow-up change so
  this one stays purely about routing.
- Update `Infrastructure/DependencyInjection.cs` to register `RoutingLlmClient` as the sole
  `ILlmClient`. Existing agent code, prompts, and the orchestrator are **untouched**.
- Update `appsettings.json` to demonstrate the new shape (all four agents on `fake` by default,
  so the no-key demo keeps working). API keys still arrive per-agent via env vars, e.g.
  `Llm__Agents__Narrative__ApiKey`.
- Add tests covering: options binding (per-agent overrides, missing agent falls back to default),
  routing (correct inner client resolved by `SkillName`), and stub adapters throwing predictably.

Non-breaking: existing single-provider configs continue to work because `Default` is populated
from the existing top-level `Provider`/`ModelId`/`ApiKey` keys.

## Capabilities

### New Capabilities

- `llm-routing`: Per-agent LLM provider routing — how the system picks which `ILlmClient` handles
  a given agent's call, how per-agent config overrides work, and the guarantees around provider
  switching without code change.

### Modified Capabilities

None. `analysis-pipeline` (still in-flight in `add-analysis-agent-pipeline`) already defines the
`ILlmClient` port and its structured-output contract; nothing there changes. This change adds a
separate capability that composes with it.

## Impact

- **Code:** `source/AiPMOInsight.Application/Abstractions/LlmOptions.cs` (extend), new
  `source/AiPMOInsight.Infrastructure/Analysis/Llm/RoutingLlmClient.cs`,
  `ILlmClientFactory.cs` + `LlmClientFactory.cs`, stub adapters
  `OpenAiLlmClient.cs`, `AnthropicLlmClient.cs`, updated
  `source/AiPMOInsight.Infrastructure/DependencyInjection.cs`.
- **Config:** `source/AiPMOInsight.Api/appsettings.json` — shape changes to `Llm.Default` +
  `Llm.Agents.<SkillName>`. Env vars: `Llm__Default__ApiKey`,
  `Llm__Agents__<SkillName>__ApiKey`.
- **Tests:** `tests/AiPMOInsight.Api.Tests/LlmOptionsTests.cs` extended; new
  `RoutingLlmClientTests.cs`, `LlmClientFactoryTests.cs` under an infrastructure test project (or
  same test project — decide in design).
- **Dependencies:** none added in this change. Vendor SDKs (Anthropic, OpenAI) land with the
  real-adapter follow-up.
- **Ops:** deploy pipelines must set env vars per agent that opts into a non-fake provider.
- **Follow-up:** a subsequent change replaces the stub adapters' `NotImplementedException` with
  real HTTP calls + structured-output plumbing.
