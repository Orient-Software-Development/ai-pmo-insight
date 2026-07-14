## Context

The routing seam from #23/#24 is complete. `LlmClientFactory` (`source/AiPMOInsight.Infrastructure/
Analysis/Llm/LlmClientFactory.cs:25-26`) already constructs `AnthropicLlmClient` / `OpenAiLlmClient`;
both currently inherit `NotWiredLlmClient`, carry the resolved `LlmProviderOptions`, and throw
`NotImplementedException` on `CompleteAsync`. DI (`AddInfrastructure`) builds one inner client per
agent eagerly and registers `RoutingLlmClient` as the sole `ILlmClient`.

The port (`source/AiPMOInsight.Application/Abstractions/ILlmClient.cs`) is:

```csharp
Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken ct) where TOutput : notnull;
```

and its XML doc is explicit: "Every call requests structured JSON output conforming to `TOutput` —
the system declares the contract and deserialises into it, and never parses free text." The four
real `TOutput` types are flat records: `MinuteRiskExtraction`, `NarrativeResult`,
`ChallengeResult` (`ChallengeResult(IReadOnlyList<Critique>)`, `Critique(string,string,string,string)`),
`ReviewResult`. No recursion, no unions, no numeric/length constraints.

The SDK type names in issue #27 were verified against the official Anthropic C# SDK reference:
`OutputConfig`/`JsonOutputFormat` (schema is `Dictionary<string, JsonElement>`), `ThinkingConfigAdaptive`,
`Messages.CreateStreaming`, and `Anthropic.Exceptions.*` all exist. Two claims do **not** hold: the
C# SDK has **no** `TOutput`→schema derivation, and **no** `.finalMessage()` streaming helper.

## Goals / Non-Goals

**Goals**

- `AnthropicLlmClient.CompleteAsync<TOutput>` performs a real Messages API call and returns a
  deserialised `TOutput`.
- Output is constrained by a JSON Schema derived from `TOutput` — structured output, not free text.
- `ModelId`, `ApiKey`, `PerAnalysisTokenBudget` from the resolved `LlmProviderOptions` are honoured.
- The #24 secret-leak guard holds on the live path: no exception/log/telemetry contains `ApiKey`.
- Typed SDK exceptions are caught and mapped; `CancellationToken` is respected.
- Change stays confined to the adapter(s) + tests + a DI/docs note.

**Non-Goals**

- OpenAI adapter — split into its own follow-up; `OpenAiLlmClient` keeps throwing.
- Cumulative per-analysis budget enforcement across requests (see Decision 3).
- Streaming, unless the non-streaming timeout is shown to bite at the configured budget (Decision 4).
- Retry/backoff chains, cost telemetry, `IOptionsMonitor` runtime reconfiguration.

## Decisions

### Decision 1 — Schema derivation: minimal reflection generator constrained to the API subset

The SDK wants a raw JSON-Schema `Dictionary<string, JsonElement>`; nothing derives it from `TOutput`.
Options weighed:

| Option | + | − |
|---|---|---|
| (a) Reflection generator, subset-constrained | generic for the port; one place | must respect API's structured-output subset |
| (b) Hand-rolled schema per known type | total control | 4 types to keep in sync by hand |
| (c) Off-the-shelf lib (NJsonSchema / JsonSchema.Net.Generation) | no bespoke code | new dep; output needs post-processing to fit the subset |

**Choice: (a) + a per-type override registry.** A small generator walks a record's public
properties and emits only `object`/`array`/`string`/`integer`/`number`/`boolean`, sets
`additionalProperties: false` on every object, and lists all declared properties in `required`. It
stays generic (the port is generic) but never emits schema features the Messages API rejects.

**Implementation finding (revised from "4 flat shapes"):** three of the four types are clean
records (`MinuteRiskExtraction`, `NarrativeResult`, `ChallengeResult`, with one level of record
nesting — `ExtractedRisk`, `Recommendation`, `Critique`). The fourth, `ReviewResult`, holds an
`IReadOnlyDictionary<string, IReadOnlyList<string>>` (`QuestionsByAudience`) — a dynamic-key object.
The Messages API structured-output subset **forbids `additionalProperties` being anything other than
`false`**, so a dynamic-key dictionary cannot be expressed as a constrained schema, and a generic
reflection generator cannot handle it. `ReviewSkill`'s doc comment shows the audiences are actually a
known set (executive, sponsor, data lead, peer PM), so:

- The generic generator handles the three record types (and their nested records) — objects,
  string properties, `IReadOnlyList<T>` → arrays, `additionalProperties: false`, all-required.
- A **per-type schema override** for `ReviewResult` emits a fixed-property object keyed by the four
  known audiences (`additionalProperties: false`, all-required, each `→ array of string`). This
  binds natively to the `IReadOnlyDictionary` via `System.Text.Json` on deserialisation. The
  `ReviewResult` record and `ReviewSkill` stay **untouched** (respects the issue's agents-untouched
  constraint); the fixed audience set lives only in the adapter's override.

The generator throws for a shape it can't express (dynamic dict with no override, or an unsupported
member type) so a new output type fails loudly at first call rather than emitting an invalid schema.

### Decision 2 — Deserialisation must round-trip, not just "not throw"

Records are PascalCase positional records; the schema property names and the model's JSON output must
bind through `JsonSerializer.Deserialize<TOutput>`. Emit PascalCase property names in the generated
schema **and** deserialise with `PropertyNameCaseInsensitive = true` as a belt-and-braces guard. The
failure mode is silent (a `TOutput` with default/null fields, no exception), so the primary test
asserts a **fully-populated** round-trip, not merely a non-null result.

### Decision 3 — Budget maps to `MaxTokens` as a per-request ceiling (documented gap)

`PerAnalysisTokenBudget` is per-*analysis* (one run = many agents × many requests); `MaxTokens` is a
per-*request* output cap. Setting `MaxTokens = PerAnalysisTokenBudget` on each call does not enforce
a per-analysis total — N requests each get the full budget. True cumulative enforcement needs a
counter shared across requests, which the adapter (constructed once per agent, reused) could hold,
but that is a larger design and out of scope. **This change maps budget→`MaxTokens` as a ceiling and
documents that cumulative enforcement is deferred.** (C# task budgets are beta and would force the
`client.Beta.Messages` path — avoided here.)

### Decision 4 — Non-streaming first; streaming only if the timeout bites

There is no `.finalMessage()` in the C# SDK, so "stream" means accumulating `content_block_delta`
text via `TryPickContentBlockDelta` and concatenating before deserialising — materially more code.
Start with the non-streaming `Messages.Create`. If the configured budget (default 100k output
tokens) reliably trips the ~10-min HTTP timeout, add manual delta accumulation as a follow-up step
within this change rather than up front.

### Decision 5 — Secret-leak guard on the live path

The `AnthropicClient` is constructed with the `ApiKey`. Wrap every SDK call in a try/catch over
`Anthropic.Exceptions.*`, and re-throw a domain-appropriate exception whose message names the
provider and skill but never the key. Never let a raw SDK exception (or its `ToString()`) propagate
unfiltered. Extend the #24 secret-leak test to the live (mocked-handler) path where feasible.

### Decision 6 — OpenAI split out

Doing both vendors doubles untested surface and breaks the red-green loop ([[feedback-apply-spec-tdd]]).
`OpenAiLlmClient` stays a throwing `NotWiredLlmClient`; a sibling issue tracks its real wiring.

## Risks / Open questions

- **Testability seam (blocking the task plan):** confirm a mock `HttpClient`/handler or base-URL
  override can be injected into `AnthropicClient`. If not, the live path can only be tested against a
  live key (excluded from CI), and CI coverage falls back to `fake` — which weakens the TDD story.
- **Schema/deserialise drift:** if a future `TOutput` adds a non-flat shape (nested optional,
  enum, collection-of-collection), the subset generator must grow. Guard with a test per real type.
