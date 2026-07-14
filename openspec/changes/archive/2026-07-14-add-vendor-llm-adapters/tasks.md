## 0. Prerequisites (resolve before writing tests)

- [x] 0.1 Confirm the testability seam: **resolved — seam exists.** `AnthropicClient` exposes
      settable `HttpClient` and `BaseUrl` (confirmed in `Anthropic.dll` 12.35.1: `set_HttpClient`,
      `set_BaseUrl`, `baseUrlOverride`, `HttpClientPassthroughHandler`). Tests inject
      `new AnthropicClient { ApiKey = "…", HttpClient = new HttpClient(mockHandler) }`; the adapter
      takes an `internal` test ctor accepting a pre-built `AnthropicClient` (Infrastructure already
      has `InternalsVisibleTo AiPMOInsight.Api.Tests`). Live path is CI-testable.
- [x] 0.2 **Done.** Added `Anthropic` 12.35.1 to `source/AiPMOInsight.Infrastructure` (pulls
      `Microsoft.Extensions.AI.Abstractions` 10.5.1); restores clean on net10.0. Type names to be
      confirmed against the compiler while implementing.

## 1. Schema generation from TOutput (TDD)

- [x] 1.1 Red: add a schema-generator test asserting the JSON Schema for each real output type
      (`MinuteRiskExtraction`, `NarrativeResult`, `ChallengeResult`, `ReviewResult`) is an `object`
      with `additionalProperties: false`, PascalCase property names, all properties in `required`, and
      arrays/primitives mapped correctly — with no unsupported constraints (`minimum`/`maxLength`/etc.).
- [x] 1.2 Add the reflection-based schema generator alongside `AnthropicLlmClient` (design §Decision 1),
      constrained to the structured-output subset.
- [x] 1.3 Green.

## 2. Anthropic adapter — happy path (TDD)

- [x] 2.1 Red: `AnthropicLlmClientTests` with a mock handler returning a canned JSON body; assert
      `CompleteAsync<ChallengeResult>` returns a **fully-populated** result (round-trip, not just
      non-null) — design §Decision 2.
- [x] 2.2 Implement `AnthropicLlmClient.CompleteAsync<TOutput>` (non-streaming `Messages.Create`):
      build request with derived schema in `OutputConfig.Format`, `ThinkingConfigAdaptive`, model +
      `MaxTokens` from options; deserialise the returned text block with
      `PropertyNameCaseInsensitive = true`.
- [x] 2.3 Green.

## 3. Model, key, budget honoured (TDD)

- [x] 3.1 Red: assert empty `ModelId` falls back to `claude-opus-4-8`; a set `ModelId` is used
      verbatim; `PerAnalysisTokenBudget` is applied as the request `MaxTokens`.
- [x] 3.2 Wire the mapping (default-model fallback, budget→`MaxTokens`). Green.

## 4. Errors, cancellation, secret-leak guard (TDD)

- [x] 4.1 Red: mock handler raises a typed `Anthropic.Exceptions.*`; assert the adapter throws a
      domain failure naming provider + skill, whose message does **not** contain the `ApiKey`.
- [x] 4.2 Red: a pre-cancelled `CancellationToken` makes `CompleteAsync` throw `OperationCanceledException`.
- [x] 4.3 Implement the try/catch mapping and token propagation. Green. Extend the #24 secret-leak
      test to the live (mocked-handler) path.

## 5. Factory + wiring

- [x] 5.1 Red: update `LlmClientFactoryTests` so the `anthropic` selector returns a working adapter
      (no `NotImplementedException` on a mocked call) and the `openai` selector still throws
      `NotImplementedException`.
- [x] 5.2 `AnthropicLlmClient` no longer inherits `NotWiredLlmClient`; `OpenAiLlmClient` keeps
      inheriting it (stays a stub). No changes to `LlmClientFactory` switch, `RoutingLlmClient`,
      `LlmOptions`, `ResolvedFor`, DI wiring, agents, or the orchestrator.
- [x] 5.3 Green; all pre-existing tests still pass.

## 6. Streaming (conditional — design §Decision 4)

- [x] 6.1 **Decision: non-streaming suffices — streaming skipped.** `PerAnalysisTokenBudget` is a
      *cap*, not the expected output size; the real agent contracts (`ChallengeResult`,
      `NarrativeResult`, `MinuteRiskExtraction`, `ReviewResult`) are a handful of short items that
      complete in seconds, well inside the SDK's 10-minute default `HttpClient` timeout. The C# SDK
      does not enforce streaming for large `MaxTokens` (unlike Python/TS). Manual
      `content_block_delta` accumulation (there is no `.finalMessage()` in C#) adds real complexity
      for no benefit at the actual response sizes. Revisit only if a future contract produces very
      large outputs.

## 7. Docs + validate + hand off

- [x] 7.1 Update the `## LLM routing` paragraph in `CLAUDE.md`: `anthropic` is now a real adapter;
      `openai` is still a throwing stub pending its own follow-up.
- [x] 7.2 Run `dotnet test` end-to-end; confirm the `AnalysisOrchestrator` integration tests still
      resolve `ILlmClient` and produce findings from the `fake` fixtures (no live key in CI).
- [x] 7.3 Run `openspec validate add-vendor-llm-adapters --strict`; fix any reported issue.
- [x] 7.4 (Hand-off — user action) Open the PR: state OpenAI is split into its own follow-up and link
      that issue; confirm only the adapter(s) + tests + DI/docs note changed. Also create the sibling
      "OpenAI vendor adapter" follow-up issue.
