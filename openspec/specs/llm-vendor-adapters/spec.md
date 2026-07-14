# llm-vendor-adapters Specification

## Purpose

Provide working vendor adapters behind the `ILlmClient` port so that agents routed to a real
provider reach the model through structured, typed calls rather than free-text parsing. This
capability covers the Anthropic Messages API adapter: it honours the resolved model, key, and token
budget, never leaks the API key on the live path, and respects cancellation.

## Requirements

### Requirement: Anthropic adapter returns typed structured output

The Anthropic adapter SHALL implement `ILlmClient.CompleteAsync<TOutput>` by calling the Anthropic Messages API via the official Anthropic C# SDK and returning a `TOutput` deserialised from the response. The output SHALL be constrained by a JSON Schema derived from `TOutput` and supplied to the request as structured output (`OutputConfig.Format` = `JsonOutputFormat` with that schema); the adapter SHALL NOT parse free text or prompt-and-regex. Deserialisation SHALL populate every field of `TOutput` from the response, not merely return a non-null instance.

#### Scenario: Structured output deserialises into TOutput

- **WHEN** an agent calls `CompleteAsync<ChallengeResult>` and the model returns JSON conforming to the derived schema
- **THEN** the adapter returns a fully-populated `ChallengeResult` (its `Critiques` list and each `Critique`'s fields bound from the response), with no `NotImplementedException`

#### Scenario: Schema is derived from the requested type, not hand-written per call site

- **WHEN** the adapter builds a request for output type `TOutput`
- **THEN** the JSON Schema attached to the request is generated from `TOutput`'s shape, restricted to the structured-output subset the Messages API accepts (objects with `additionalProperties: false`, arrays, and primitive types; no unsupported numeric/length constraints)

### Requirement: Adapter honours resolved model, key, and token budget

The Anthropic adapter SHALL use `LlmProviderOptions.ModelId` as the request model, falling back to `claude-opus-4-8` when it is empty. It SHALL authenticate using `LlmProviderOptions.ApiKey`, supplied only via the environment binding path, passed explicitly to the SDK client. It SHALL apply `LlmProviderOptions.PerAnalysisTokenBudget` to the request output-token cap (`MaxTokens`).

#### Scenario: Model id falls back to the default

- **WHEN** the resolved `LlmProviderOptions.ModelId` is empty
- **THEN** the adapter issues the request with model `claude-opus-4-8`

#### Scenario: Configured model and budget are applied

- **WHEN** the resolved options are `{ ModelId: "claude-sonnet-5", PerAnalysisTokenBudget: 50000 }`
- **THEN** the adapter issues the request with model `claude-sonnet-5` and an output-token cap of 50000

### Requirement: Secret-leak guard on the live path

The Anthropic adapter SHALL NOT expose the configured `ApiKey` in any exception message, log line, or telemetry emitted from the live call path. Typed SDK exceptions (`Anthropic.Exceptions.*`) SHALL be caught and mapped to a domain-appropriate failure whose message names the provider and skill but never the key; a raw SDK exception SHALL NOT propagate unfiltered.

#### Scenario: Provider error does not leak the key

- **WHEN** the Anthropic SDK raises a typed exception (e.g. authentication or rate-limit) during a call
- **THEN** the adapter surfaces a domain failure whose message and any logged diagnostic contain the provider and skill name but do NOT contain the `ApiKey` value

### Requirement: Cancellation is respected

The Anthropic adapter SHALL propagate the supplied `CancellationToken` to the underlying SDK call, so that a cancelled analysis run does not continue issuing or awaiting vendor requests.

#### Scenario: Cancelled token aborts the call

- **WHEN** `CompleteAsync` is invoked with a `CancellationToken` that is cancelled before the call completes
- **THEN** the call terminates with an `OperationCanceledException` rather than returning a result or hanging
