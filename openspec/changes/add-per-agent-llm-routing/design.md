## Context

Four of the nine agents in `AnalysisOrchestrator` reach the model via `ILlmClient`
(`RiskAndIssue` #4, `Narrative` #7, `Challenge` #8, `Review` #9). Today `Infrastructure/
DependencyInjection.cs` registers a single `FakeLlmClient` for that port, so every agent
resolves to the same client and — once real adapters land — the same provider/model.

`LlmRequest` already carries the caller's `SkillName` (see
[`ILlmClient.cs`](../../../source/AiPMOInsight.Application/Abstractions/ILlmClient.cs)); the
comment on that field says explicitly "used for routing / fixtures". The seam is designed for
this change; nothing needs to move in Application. The user is expected to swap providers per
agent via config alone, and to be able to demo without any API keys.

## Goals / Non-Goals

**Goals:**

- Each LLM-backed agent can be pointed at a distinct provider + model via `appsettings.json` +
  env-var overrides, with **zero code changes** to Application or the agents themselves.
- Provider selection is a startup-time decision. The hot path (per `CompleteAsync` call) is a
  single dictionary lookup on `LlmRequest.SkillName`.
- Existing single-provider deployments keep working without touching their config (legacy
  flat-shape binding still valid).
- The "no API key needed" demo path still works: `fake` is a first-class provider selector.
- Unknown-provider misconfigurations fail loudly at **startup**, never mid-request.

**Non-Goals:**

- Real HTTP/SDK integration with OpenAI or Anthropic. The stubs throw
  `NotImplementedException`; wiring them to actual vendor SDKs is a follow-up change with its
  own OpenSpec proposal (structured-output plumbing, retries, budget enforcement, cost
  telemetry).
- Runtime, per-tenant provider switching. Config binding is startup-scoped. `IOptionsMonitor`
  is out of scope.
- Fallback / redundancy chains (retry on second provider). Out of scope.
- Cost or token budget enforcement across per-agent providers. Out of scope; the existing
  `PerAnalysisTokenBudget` remains a single value on `Default` for now.

## Decisions

### 1. Routing lives in `Infrastructure` behind the existing `ILlmClient` port

**Choice:** Add a `RoutingLlmClient : ILlmClient` in
`source/AiPMOInsight.Infrastructure/Analysis/Llm/`. It owns
`IReadOnlyDictionary<string, ILlmClient>` (skill name → inner client) and a fallback
`_default`. `CompleteAsync` looks up by `request.SkillName`, falls back to `_default`, calls
through.

**Alternatives considered:**

- **Keyed services (.NET 8 `AddKeyedSingleton`)**, with agents injecting
  `[FromKeyedServices("Narrative")] ILlmClient`. Rejected: forces every agent to hard-code its
  key at the constructor, leaks routing awareness into Application, and breaks the "no agent
  code changes" goal.
- **Per-agent factory injected into each agent** (`ILlmClientFactory` used by the agent to
  resolve its own client). Rejected: same leak problem; also forces four constructor changes
  and four DI edits.
- **Router at the orchestrator** (orchestrator picks a client per agent, hands it to the
  agent). Rejected: makes the orchestrator provider-aware and defeats the port. The port
  exists precisely to keep the orchestrator ignorant.

The routing-adapter choice keeps agents dependent only on `ILlmClient` (they already are), so
the diff is contained to Infrastructure + DI + config + tests.

### 2. Config shape: `Llm.Default` + `Llm.Agents.<SkillName>`

**Choice:**

```csharp
public sealed class LlmProviderOptions
{
    public string Provider { get; init; } = "fake";
    public string ModelId { get; init; } = string.Empty;
    public string ApiKey  { get; init; } = string.Empty;
    public int    PerAnalysisTokenBudget { get; init; } = 100_000;
}

public sealed class LlmOptions
{
    public const string SectionName = "Llm";
    public LlmProviderOptions Default { get; init; } = new();
    public IReadOnlyDictionary<string, LlmProviderOptions> Agents { get; init; }
        = new Dictionary<string, LlmProviderOptions>(StringComparer.OrdinalIgnoreCase);
    // Legacy flat keys — kept for one release for back-compat, folded into Default in binding step.
    public string Provider { get; init; } = string.Empty;
    public string ModelId  { get; init; } = string.Empty;
    public string ApiKey   { get; init; } = string.Empty;
}
```

Env-var keys follow standard `IConfiguration` binding: `Llm__Default__Provider`,
`Llm__Agents__Narrative__ApiKey`, etc. Agent keys use the exact `SkillName` from each
`IAgentSkill.Name` (`RiskAndIssue`, `Narrative`, `Challenge`, `Review`) — comparison is
case-insensitive so operators are not tripped by casing.

**Back-compat rule** — resolved in the DI wiring, not in the options type: if `Default.Provider`
is empty and any legacy flat key is set, treat the legacy keys as `Default`. If both are set,
`Default` wins. Emit a startup warning when the fold happens; drop the legacy keys in a future
change.

**Alternatives considered:**

- **Flat top-level per agent** (`Llm.Narrative.Provider`). Rejected: no natural place for the
  shared default; every provider setting has to be repeated four times.
- **Array of agent entries** (`Llm.Agents: [{ Name, Provider, … }]`). Rejected: dictionary
  form is more ergonomic in `appsettings.json` and lets env-var overrides target one agent
  cleanly (`Llm__Agents__Narrative__ApiKey`).

### 3. Provider selection via `ILlmClientFactory` (single-file switch)

**Choice:**

```csharp
public interface ILlmClientFactory
{
    ILlmClient Create(string skillNameForDiagnostics, LlmProviderOptions options);
}
```

Implementation lives in `Infrastructure/Analysis/Llm/LlmClientFactory.cs`. Switches on
`options.Provider` (case-insensitive):

- `"fake"` → `new FakeLlmClient(FakeLlmFixtures.Default())`
- `"anthropic"` → `new AnthropicLlmClient(options)` — stub that throws on `CompleteAsync`
- `"openai"` → `new OpenAiLlmClient(options)` — stub that throws on `CompleteAsync`
- anything else → `throw new InvalidOperationException(...)` naming both the skill and the
  provider string; this bubbles from `AddInfrastructure` and fails app startup.

**Rationale:** one file to grep when a new provider lands. The follow-up change replaces the
stub bodies with real HTTP calls — no signatures move. `skillNameForDiagnostics` is threaded
purely for error messages; adapters never key behaviour on it (per-agent variance is a config
concern, not an adapter concern).

**Alternatives considered:**

- **Reflection / plugin discovery** for providers. Rejected: two providers today, three
  tomorrow. Not worth the indirection.
- **Registering each provider as its own DI service** and having the factory resolve by key.
  Rejected: same information ends up in DI, only with more moving parts.

### 4. Stub adapters throw at `CompleteAsync`, not at construction

**Choice:** `OpenAiLlmClient` and `AnthropicLlmClient` in this change store `LlmProviderOptions`
in the ctor and throw `NotImplementedException($"Provider '{provider}' is not yet wired for skill '{skill}'")`
inside `CompleteAsync`.

**Rationale:** we want operators to be able to declare the target shape now (config, ops
runbooks, secret provisioning) and get a clear error the first time a real call is made, rather
than a startup crash before any diagnostic UI is up. Startup still fails hard for **unknown**
providers — this is only about the two known-but-unwired ones.

**Alternative considered:** throw in the ctor. Rejected — makes it impossible to boot the API
with a "prod-shape" config unless every referenced adapter is fully wired. That would force
us to bundle the real adapter in this same change.

### 5. Inner-client cache: build once, at DI registration

**Choice:** `Infrastructure/DependencyInjection.cs`, inside `AddInfrastructure`, binds
`LlmOptions`, builds a `Dictionary<string, ILlmClient>` for every agent that has an override,
builds the `Default` client once, and injects both into `RoutingLlmClient`. The routing client
is registered `Singleton`, matching the existing registration lifetime.

**Rationale:** provider switches are config changes, and config is bound at startup. Per-request
factory calls would waste work and complicate the fixture-based tests.

## Risks / Trade-offs

- **[R1] Casing / naming drift on skill names.** If `IAgentSkill.Name` on any of the four LLM
  agents ever changes, config maps by string will silently fall back to `Default`. → Guard with
  an assertion in `AddInfrastructure` that every key in `Llm.Agents` matches one of the four
  known skill names (case-insensitive); unknown keys fail startup with the offending name. Add a
  test.
- **[R2] Silent legacy-key fold.** Operators who set `Llm.Provider = "anthropic"` in an old
  config may be surprised when a partial `Llm.Default` block wins. → Log a warning at startup
  whenever legacy keys are present; drop them in a follow-up change.
- **[R3] Secret leakage through diagnostics.** Threading `LlmProviderOptions` through the
  factory and stub adapters risks a well-meaning `ToString()` printing the key. → `ApiKey` field
  is `string`, never included in exception messages by construction; add a test that a thrown
  stub-adapter exception's `Message` does not contain the configured key value.
- **[R4] Perceived readiness of stubs.** A green build with `Provider = "anthropic"` may look
  ready when it is not. → Stub `CompleteAsync` message is explicit ("not yet wired"). Log an
  info line at startup listing the resolved provider per agent so ops sees the wiring.
- **[R5] Test project sprawl.** New tests could go in a new Infrastructure test project. →
  Piggy-back on `tests/AiPMOInsight.Api.Tests/` to keep the test-project count stable; the routing
  behaviour is Infrastructure-only but the Api test project already has the necessary DI/config
  fixtures.

## Migration Plan

1. Ship this change with `appsettings.json` still fully on `fake` at the `Default` block (no
   per-agent overrides). Existing behaviour is unchanged.
2. Operators wanting to A/B a real provider add an `Llm.Agents.<SkillName>` block plus the
   matching env var. That is a config-only rollout.
3. Rollback is a config revert — no code roll-back needed.
4. Follow-up change wires the stub adapters to real vendor SDKs (structured output, retries,
   budgets). At that point the fold-in of legacy flat keys is dropped and `LlmOptions.Provider/
   ModelId/ApiKey` top-level fields are removed.

## Open Questions

- Should `PerAnalysisTokenBudget` be per-agent or stay a single global? Leaving it single for
  now (on `Default`); can promote to per-agent when the real adapters enforce it.
- Do we need a "disable this agent" selector (e.g. `Provider = "off"`)? Not for this change —
  add later if a real operator asks.
