## ADDED Requirements

### Requirement: Per-agent LLM provider selection via configuration

The system SHALL allow each LLM-backed agent (`RiskAndIssue`, `Narrative`, `Challenge`, `Review`) to be independently pointed at a different provider and/or model through configuration alone, with no code change to any agent, prompt, or the orchestrator. Configuration SHALL be expressed as an `Llm.Default` provider block **and** an optional `Llm.Agents.<SkillName>` block per agent, where any field present on the agent block overrides the corresponding `Default` field for that agent only. When no `Llm.Agents.<SkillName>` block exists for a given agent, that agent SHALL use `Llm.Default`.

#### Scenario: Per-agent override selects a different provider

- **WHEN** configuration sets `Llm.Default.Provider = "fake"` and `Llm.Agents.Narrative.Provider = "openai"` with a valid `ModelId` and `ApiKey`
- **THEN** LLM calls made by the Narrative agent are dispatched to the OpenAI provider, and calls made by RiskAndIssue, Challenge, and Review are dispatched to the fake provider

#### Scenario: Missing agent block falls back to Default

- **WHEN** configuration sets `Llm.Default.Provider = "anthropic"` and defines no `Llm.Agents.Challenge` block
- **THEN** LLM calls made by the Challenge agent are dispatched to the Anthropic provider using `Llm.Default`'s `ModelId` / `ApiKey`

#### Scenario: Partial override merges with Default

- **WHEN** `Llm.Default = { Provider: "anthropic", ModelId: "claude-sonnet-5", ApiKey: "K1" }` and `Llm.Agents.Review = { ModelId: "claude-opus-4-8" }`
- **THEN** the Review agent uses `Provider = "anthropic"`, `ModelId = "claude-opus-4-8"`, `ApiKey = "K1"`

### Requirement: Routing on `LlmRequest.SkillName`

The system SHALL route each `ILlmClient.CompleteAsync` call to the inner provider client selected by the caller's `LlmRequest.SkillName`. Routing SHALL be performed inside a single `RoutingLlmClient` adapter registered as the sole `ILlmClient` in the DI container. Agent code SHALL continue to depend only on `ILlmClient` and SHALL NOT know about routing, provider selection, or per-agent config. The routing decision SHALL be constant-time and MUST NOT rebuild inner clients per request.

#### Scenario: Agent code is unaware of routing

- **WHEN** any of the four LLM-backed agents (`RiskAndIssue`, `Narrative`, `Challenge`, `Review`) resolves its `ILlmClient` dependency
- **THEN** it receives the `RoutingLlmClient` instance and calls `CompleteAsync` on it exactly as before — the agent's source code is unchanged from the pre-routing state

#### Scenario: SkillName drives inner client selection

- **WHEN** two agents whose config points at different providers each issue an `LlmRequest` bearing their own `SkillName`
- **THEN** each request is handled by that agent's configured inner client, and neither request touches the other agent's client

#### Scenario: Inner clients are built once

- **WHEN** an agent issues N `LlmRequest`s within a run
- **THEN** its inner `ILlmClient` is resolved at most once at startup and reused for every subsequent request without factory invocations on the hot path

### Requirement: Provider factory with `fake`, `anthropic`, and `openai` selectors

The system SHALL provide an `ILlmClientFactory` in the Infrastructure layer that maps a `LlmProviderOptions` value to a concrete `ILlmClient`. The factory SHALL recognise the selector strings `fake`, `anthropic`, and `openai` (case-insensitive) in this change. `fake` SHALL construct the existing `FakeLlmClient` with its fixture set. `anthropic` and `openai` SHALL each construct a stub adapter that carries the configured `LlmProviderOptions` but throws `NotImplementedException` from `CompleteAsync`; real HTTP calls arrive in a follow-up change. An unrecognised `Provider` value SHALL cause the factory to throw a configuration error at startup — never at request time.

#### Scenario: Fake provider is fully functional

- **WHEN** the factory is invoked with `{ Provider: "fake" }`
- **THEN** it returns a `FakeLlmClient` seeded with the default fixture set, and calls to `CompleteAsync` return fixture responses as they do today

#### Scenario: Real-vendor stubs fail loudly on invocation only

- **WHEN** an agent whose config resolves to `{ Provider: "anthropic" }` (or `"openai"`) calls `CompleteAsync`
- **THEN** the call throws `NotImplementedException` with a message identifying the provider and skill, but application startup, DI resolution, and factory construction all succeed

#### Scenario: Unknown provider fails at startup

- **WHEN** configuration sets any agent's `Provider` to a value the factory does not recognise
- **THEN** application startup fails with a configuration error naming the offending agent and the unknown provider string

### Requirement: API key confidentiality per agent

The system SHALL accept a distinct `ApiKey` per agent, supplied only via the environment binding path (`Llm__Default__ApiKey`, `Llm__Agents__<SkillName>__ApiKey`). API keys SHALL NOT be committed to configuration files under source control. The `RoutingLlmClient`, `ILlmClientFactory`, and per-provider adapters SHALL NOT log, surface, or otherwise expose the `ApiKey` value in exceptions, telemetry, or diagnostic output.

#### Scenario: Committed appsettings ship no real keys

- **WHEN** `source/AiPMOInsight.Api/appsettings.json` is inspected
- **THEN** no `ApiKey` field contains a real vendor secret — every agent that would need one either uses the `fake` provider or leaves `ApiKey` empty for env-var override

#### Scenario: Errors do not leak the key

- **WHEN** a stub adapter throws `NotImplementedException` (or any adapter surfaces a provider error)
- **THEN** the exception message and any logged diagnostic output contain the provider name and skill name but do NOT contain the `ApiKey` value

### Requirement: Backwards-compatible options binding

The system SHALL bind `LlmOptions` from the `Llm` configuration section such that pre-existing flat-shape settings (`Llm.Provider`, `Llm.ModelId`, `Llm.ApiKey`, `Llm.PerAnalysisTokenBudget`) continue to work as the `Llm.Default` block. When a config supplies BOTH the flat keys and a `Llm.Default` block, the `Llm.Default` block SHALL win. Adopting per-agent overrides SHALL be purely additive from the old shape.

#### Scenario: Legacy flat config still resolves

- **WHEN** configuration sets only `Llm.Provider = "fake"` and `Llm.PerAnalysisTokenBudget = 100000` (no `Llm.Default`, no `Llm.Agents`)
- **THEN** every LLM-backed agent resolves to the fake provider using the token budget from the legacy keys, exactly as before this change

#### Scenario: Explicit Default wins over legacy keys

- **WHEN** configuration sets `Llm.Provider = "fake"` **and** `Llm.Default.Provider = "anthropic"`
- **THEN** every agent without a specific override uses the Anthropic provider from the `Llm.Default` block, and the legacy `Llm.Provider = "fake"` key is ignored
