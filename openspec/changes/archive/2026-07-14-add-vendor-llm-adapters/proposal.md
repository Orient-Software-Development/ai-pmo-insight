## Why

Phase 3.9/3.10 (#23/#24) shipped the per-agent routing seam — config binding, `LlmOptions.ResolvedFor`,
`LlmClientFactory`, `RoutingLlmClient`, startup validation — but the `anthropic` / `openai` selectors
resolve to **stub adapters** (`AnthropicLlmClient`, `OpenAiLlmClient` : `NotWiredLlmClient`) that
throw `NotImplementedException` on first `CompleteAsync`. That was the accepted deliverable of #24;
real vendor HTTP was explicitly deferred to this change (GitHub issue #27). Until it lands, an agent
configured with `Provider = "anthropic"` throws instead of producing findings, so the only working
path is `fake`.

## What Changes

- Replace `AnthropicLlmClient`'s throwing body with a real Anthropic **Messages API** call via the
  official Anthropic C# SDK (`Anthropic` NuGet package), honouring the existing `ILlmClient` port:
  `Task<TOutput> CompleteAsync<TOutput>(LlmRequest, CancellationToken)`.
- **Structured output, not free text.** Derive a JSON Schema from `TOutput` and pass it via
  `OutputConfig { Format = new JsonOutputFormat { Schema = … } }`; deserialise the returned text
  block into `TOutput` with `System.Text.Json`. No prompt-and-regex. The C# SDK has **no**
  `TOutput`→schema derivation (unlike Java's class overload or Python's Pydantic), so a small
  reflection-based schema generator, constrained to the structured-output subset the API accepts,
  is part of this change.
- **Model + params.** `Model` from `LlmProviderOptions.ModelId`, defaulting to `claude-opus-4-8`
  when empty. Adaptive thinking (`ThinkingConfigAdaptive`).
- **Budget.** Map `LlmProviderOptions.PerAnalysisTokenBudget` onto the request `MaxTokens` cap.
  (Semantics caveat in design: this is a per-request ceiling, not cumulative per-analysis
  enforcement.)
- **Auth.** Read the key from `LlmProviderOptions.ApiKey` (env-only) and pass it to the SDK client
  explicitly. Preserve the R3 secret-leak guard — no adapter exception, log line, or telemetry may
  contain the `ApiKey`; SDK exceptions are caught and re-wrapped as a domain failure.
- **Errors + cancellation.** Catch typed `Anthropic.Exceptions.*`, map to a domain-appropriate
  failure; honour `CancellationToken`.
- **OpenAI is split out.** `OpenAiLlmClient` stays the throwing stub in this change; it moves to its
  own follow-up issue. Called out here and in the PR.

Non-breaking: only the two adapter files (one implemented, one left as-is) plus tests and a DI note
change. `RoutingLlmClient`, `LlmClientFactory`, `LlmOptions`, `ResolvedFor`, agents, and the
orchestrator are untouched — the factory already constructs these adapters and DI already builds one
per agent eagerly.

## Capabilities

### Modified Capabilities

- `llm-routing`: the factory's `anthropic` selector now constructs a **working** adapter instead of
  a throwing stub. The `openai` selector remains a throwing stub (deferred).

### New Capabilities

- `llm-vendor-adapters`: the behavioural contract of a real vendor adapter — structured JSON output
  from a `TOutput`-derived schema, model/key/budget honoured from the resolved
  `LlmProviderOptions`, typed-exception mapping, cancellation, and the secret-leak guard on the live
  path.

## Impact

- **Code:** `source/AiPMOInsight.Infrastructure/Analysis/Llm/AnthropicLlmClient.cs` (implement),
  a new schema-generator helper alongside it, `OpenAiLlmClient.cs` (unchanged; keeps throwing).
  `NotWiredLlmClient` stays for the OpenAI stub.
- **Config/DI:** no routing/config/agent code changes. A DI note (and the CLAUDE.md `## LLM routing`
  paragraph) is updated to reflect that `anthropic` is now live and `openai` is still a stub.
- **Tests:** new `AnthropicLlmClientTests` exercising schema shape, deserialisation round-trip,
  budget→`MaxTokens`, error mapping, cancellation, and the secret-leak guard — via a mocked HTTP
  handler (no live key in CI). Existing tests stay green; new tests use `fake` or the mock handler.
- **Dependencies:** adds the `Anthropic` NuGet package to `AiPMOInsight.Infrastructure`.
- **Ops:** agents pointed at `anthropic` now make real, billed calls once a key is set via
  `Llm__Default__ApiKey` / `Llm__Agents__<SkillName>__ApiKey`.
- **Follow-up:** `OpenAiLlmClient` real wiring in its own change.

## Open questions

- **Testability seam:** can a mock `HttpClient`/handler (or base-URL override) be injected into the
  C# `AnthropicClient`? This gates whether the live path is testable in CI at all, and therefore
  what TDD looks like here (see [[feedback-apply-spec-tdd]]). Resolve before finalising the task plan.
- **Streaming:** the C# SDK has **no** `.finalMessage()` helper. At the 100k default budget,
  non-streaming risks the HTTP timeout. Start non-streaming; add manual delta accumulation only if
  the timeout bites.
