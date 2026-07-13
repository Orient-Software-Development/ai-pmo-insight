## 1. Options binding (TDD)

- [x] 1.1 Extend `tests/AiPMOInsight.Api.Tests/LlmOptionsTests.cs` (red): `Default` block binds; `Agents.Narrative` overrides `Default`; missing agent falls back to `Default`; case-insensitive skill key match; legacy flat keys fold into `Default`; explicit `Default` wins over legacy keys.  *(Legacy-fold branch deferred to §4.2 — fold lives in DI wiring per design §2.)*
- [x] 1.2 In `source/AiPMOInsight.Application/Abstractions/`, add `LlmProviderOptions.cs` (Provider, ModelId, ApiKey, PerAnalysisTokenBudget). *Design correction:* `Provider` defaults to `""` and `PerAnalysisTokenBudget` defaults to `0` so that field-level "inherit from Default" merge works (see design §2 update).
- [x] 1.3 In the same folder, extend `LlmOptions.cs`: add `Default : LlmProviderOptions` and `Agents : IReadOnlyDictionary<string, LlmProviderOptions>` (OrdinalIgnoreCase); keep legacy `Provider` / `ModelId` / `ApiKey` fields marked as legacy in XML doc.
- [x] 1.4 Add a `LlmOptions.ResolvedFor(string skillName)` helper that returns the effective `LlmProviderOptions` for an agent (field-level merge: agent override string wins iff non-empty, budget wins iff non-zero, else Default).
- [x] 1.5 Green: all `LlmOptionsTests` pass; existing tests still pass.

## 2. Provider factory (TDD)

- [x] 2.1 Add `tests/AiPMOInsight.Api.Tests/LlmClientFactoryTests.cs` (red): `"fake"` returns a working `FakeLlmClient`; unknown / empty provider throws `InvalidOperationException` naming the skill and provider string; provider match is case-insensitive. *(#23 scope covers `fake` + unknown only; `anthropic` / `openai` stub assertions defer to #24. File placed at test-project root to match the existing flat convention.)*
- [x] 2.2 Add `source/AiPMOInsight.Infrastructure/Analysis/Llm/ILlmClientFactory.cs` with `Create(string skillNameForDiagnostics, LlmProviderOptions options) : ILlmClient`.
- [x] 2.3 Add `source/AiPMOInsight.Infrastructure/Analysis/Llm/LlmClientFactory.cs` implementing the switch. *(#23 wires `fake` and unknown-throws. `anthropic` / `openai` cases land in #24.)*
- [x] 2.4 Add `source/AiPMOInsight.Infrastructure/Analysis/Llm/AnthropicLlmClient.cs` — stub implementing `ILlmClient`, throws `NotImplementedException` with a message containing the provider name and skill name (never the ApiKey).
- [x] 2.5 Add `source/AiPMOInsight.Infrastructure/Analysis/Llm/OpenAiLlmClient.cs` — same shape as Anthropic stub.
- [x] 2.6 Add a test asserting the stub-adapter exception `Message` does NOT contain the configured `ApiKey` value (secret-leak guard).
- [x] 2.7 Green: all factory tests pass.

## 3. Routing adapter (TDD)

- [x] 3.1 Add `tests/AiPMOInsight.Api.Tests/RoutingLlmClientTests.cs` (red): routes by `LlmRequest.SkillName` to the matching inner client; unknown/missing skill routes to `Default`; inner client resolved once (build count = 1 across N calls); constructor validates non-null `Default`.
- [x] 3.2 Add `source/AiPMOInsight.Infrastructure/Analysis/Llm/RoutingLlmClient.cs` implementing `ILlmClient` with a `Dictionary<string, ILlmClient>` (`StringComparer.OrdinalIgnoreCase`) + `_default`.
- [x] 3.3 Green: all routing tests pass.

## 4. DI wiring

- [x] 4.1 Add integration test `tests/AiPMOInsight.Api.Tests/LlmRoutingDependencyInjectionTests.cs`: `AddInfrastructure` resolves `ILlmClient` as `RoutingLlmClient`; unknown provider on `Default` fails at `AddInfrastructure`; unknown provider on an `Agents` entry fails naming the skill and provider; legacy flat keys fold into `Default`; explicit `Default` wins over legacy keys. *(Anthropic-throws assertion from the original task text defers to #24.)*
- [x] 4.2 Update `source/AiPMOInsight.Infrastructure/DependencyInjection.cs`: bind `LlmOptions`, run legacy-fold if `Default.Provider` is empty and legacy keys are set, use `ILlmClientFactory` to build one `ILlmClient` per agent override + `Default` **eagerly** (so unknown provider fails at startup), register `RoutingLlmClient` as the singleton `ILlmClient`. *Empty/absent config now fails loudly instead of silently falling back to fake — this matches design §2 loud-fail intent and spec R3 startup-fail scenario.*
- [ ] 4.3 Add a startup assertion: every key in `Llm.Agents` matches one of `RiskAndIssue`, `Narrative`, `Challenge`, `Review` (case-insensitive); unknown key → throw naming the offending name. Cover with a test.
- [ ] 4.4 Emit an `ILogger` info line at startup listing the resolved provider per agent (name → provider). Do not log `ApiKey`. Cover with a log-capture test.
- [ ] 4.5 Green: DI tests pass; the existing analysis integration tests still pass.

## 5. Config + docs

- [x] 5.1 Update `source/AiPMOInsight.Api/appsettings.json` to the new shape: `Llm.Default = { Provider: "fake", ModelId: "", PerAnalysisTokenBudget: 100000 }` and an empty `Llm.Agents: {}`. Keep the `//` comment lines documenting env-var override paths (`Llm__Default__ApiKey`, `Llm__Agents__<SkillName>__ApiKey`). *(Landed alongside §1 to unblock the host-boot test that asserts `Default.Provider = "fake"`.)*
- [ ] 5.2 Update the `## Auth` / LLM section of `CLAUDE.md` to describe the routing seam in one paragraph, matching the tone of the existing sections (provider selection is per-agent; missing agent block falls back to `Default`; stubs for anthropic/openai are wired but throw until the follow-up).
- [ ] 5.3 Run `dotnet test` end-to-end. Confirm the pre-existing `AnalysisOrchestrator` integration tests still resolve `ILlmClient` and produce findings from the fixtures.

## 6. Validate + hand off

- [ ] 6.1 Run `openspec validate add-per-agent-llm-routing --strict`; fix any reported issue.
- [ ] 6.2 Summarise the change in the PR description, calling out that vendor HTTP wiring is a deliberate follow-up (link to this change).
