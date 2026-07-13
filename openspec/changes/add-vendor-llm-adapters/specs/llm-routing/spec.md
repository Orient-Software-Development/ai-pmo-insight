## MODIFIED Requirements

### Requirement: Provider factory with `fake`, `anthropic`, and `openai` selectors

The system SHALL provide an `ILlmClientFactory` in the Infrastructure layer that maps a `LlmProviderOptions` value to a concrete `ILlmClient`. The factory SHALL recognise the selector strings `fake`, `anthropic`, and `openai` (case-insensitive). `fake` SHALL construct the existing `FakeLlmClient` with its fixture set. `anthropic` SHALL construct a **working** adapter that performs real Anthropic Messages API calls (see the `llm-vendor-adapters` capability). `openai` SHALL construct a stub adapter that carries the configured `LlmProviderOptions` but throws `NotImplementedException` from `CompleteAsync`; real OpenAI HTTP wiring is a deliberate follow-up change. An unrecognised `Provider` value SHALL cause the factory to throw a configuration error at startup — never at request time.

#### Scenario: Fake provider is fully functional

- **WHEN** the factory is invoked with `{ Provider: "fake" }`
- **THEN** it returns a `FakeLlmClient` seeded with the default fixture set, and calls to `CompleteAsync` return fixture responses

#### Scenario: Anthropic selector constructs a working adapter

- **WHEN** the factory is invoked with `{ Provider: "anthropic", ModelId: "claude-opus-4-8", ApiKey: "K1" }`
- **THEN** it returns an adapter whose `CompleteAsync` performs a real Anthropic Messages API call (no `NotImplementedException`), constructed with the supplied model and key

#### Scenario: OpenAI selector remains a throwing stub

- **WHEN** an agent whose config resolves to `{ Provider: "openai" }` calls `CompleteAsync`
- **THEN** the call throws `NotImplementedException` with a message identifying the provider and skill, but application startup, DI resolution, and factory construction all succeed

#### Scenario: Unknown provider fails at startup

- **WHEN** configuration sets any agent's `Provider` to a value the factory does not recognise
- **THEN** application startup fails with a configuration error naming the offending agent and the unknown provider string
