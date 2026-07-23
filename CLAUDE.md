# CLAUDE.md

Guidance for Claude Code in this repo. Seven sections adapted from "living-spec" practice.
Two sibling files loaded on demand: [SHIPPED.md](SHIPPED.md) (historical feature log) and
[CLAUDE-decisions.md](CLAUDE-decisions.md) (dated architectural decisions).

**Deliberate omissions** (guideline recommendations we do NOT follow): symbols not `file:line`
(refactor-resistant); automation rules live in `.claude/settings.json`, not prose here.

---

## 1. Agent role & project overview

A **Clean-Architecture .NET 10 service** packaged as a `dotnet new` template
(`.template.config/template.json`, `sourceName: AiPMOInsight`). Vertical-slice CQRS over a
**lightweight in-process mediator** (`AiPMOInsight.Application/Messaging`, no MediatR). Optional
**React + Vite SPA** at `source/AiPMOInsight.Api/ClientApp` (template param `--client-framework
= react | none`).

**Domain:** an AI-assisted PMO insight surface. LLM agents (deterministic + LLM-backed) analyse
uploaded project workbooks/minutes into findings, which back RAG-scored health dashboards
(L1 executive portfolio, L2 per-project, L3 data quality). All numbers are **POC placeholders
until the PMO agrees them** — the running app logs a warning at startup saying so.

**Priorities in tension you'll be asked to make tradeoffs on:**
- Correctness of the analysis over speed of iteration on it.
- Auditability of every scoring decision (cited findings, applied overrides) over compact JSON.
- Preserving the "shared workspace, no per-user scoping" model unless a change explicitly
  proposes otherwise.
- Not fabricating data in the UI — a missing signal renders a dashed "not yet captured"
  placeholder, never a plausible-looking zero.

**Stack:** .NET 10, PostgreSQL via EF Core / Npgsql, ASP.NET Core Identity, Minimal API,
xUnit + AwesomeAssertions, React 19 + Vite 8, OpenTelemetry (OTLP).

---

## 2. Key commands

```bash
# Build + test the whole solution (Release config matches CI).
dotnet build -c Release
dotnet test -c Release

# Single test class or fact.
dotnet test --filter "FullyQualifiedName~HealthScoringEndpointsTests"
dotnet test --filter "FullyQualifiedName~OpenApiDriftTest"

# EF migrations (design-time context = AppDbContextFactory; override with AppDb__ConnectionString).
dotnet ef migrations add <MigrationName> \
    --project source/AiPMOInsight.Infrastructure --startup-project source/AiPMOInsight.Api
dotnet ef database update \
    --project source/AiPMOInsight.Infrastructure --startup-project source/AiPMOInsight.Api

# Accept a deliberate API-shape change: rewrites the committed openapi.json baseline in place.
# Commit the diff in the same PR as the code change.
UPDATE_OPENAPI_BASELINE=1 dotnet test --filter "FullyQualifiedName~OpenApiDriftTest"

# Classify openapi.json drift vs main: Breaking (consumer must update) vs Additive
# (backwards-compatible). Mirrors the CI advisory step. Requires oasdiff installed locally.
git show main:openapi.json > /tmp/base-openapi.json
oasdiff breaking /tmp/base-openapi.json openapi.json     # empty = additive only
oasdiff diff /tmp/base-openapi.json openapi.json --summary

# Local Postgres for dev / running the API outside tests.
docker-compose up -d postgres
dotnet run --project source/AiPMOInsight.Api

# React dev server (proxies to the API).
cd source/AiPMOInsight.Api/ClientApp && npm install && npm run dev

# Format the backend. Use --verify-no-changes as a non-destructive check (CI / pre-commit).
dotnet format --verify-no-changes
dotnet format

# Frontend has no lint script yet (ClientApp/package.json defines only dev/build/preview/
# generate-api). Adding ESLint + a `lint` script is a documented follow-on.
```

**Config the agent must NOT commit values for** (env / user-secrets only): `Jwt__SigningKey`,
`Llm__Default__ApiKey`, `Llm__Agents__<SkillName>__ApiKey`, `AppDb__ConnectionString`. Enforced
as a hard **Never** in §5.

---

## 3. Architecture & critical control points

Referenced by symbol so refactors don't rot the map. Grep for names to open.

**Composition root.** `Program.cs` (in `AiPMOInsight.Api`) wires everything — Identity, JWT, EF,
OpenTelemetry, health checks, OpenAPI schema-name transformer, and the endpoint map. Feature
services register via extension methods (`AddApplication`, `AddInfrastructure`).

**Vertical slices** live under `AiPMOInsight.Application/Features/<Feature>/*.cs`. Each slice is a
single file containing `Query`/`Command` + `Result` records + `internal sealed class Handler`.
The mediator (`AiPMOInsight.Application/Messaging`) dispatches `ISender.Send(new Query(), ct)`
through a `LoggingBehavior` pipeline.

**Domain ports** live in `AiPMOInsight.Application/Abstractions/*.cs`: `ICurrentUser`,
`IUploadRepository`, `IUploadParser`, `IFindingRepository`, `ILlmClient`. Implementations are in
`AiPMOInsight.Infrastructure` and `AiPMOInsight.Api/Security`.

**Endpoints** are Minimal-API extension methods in `AiPMOInsight.Api/Endpoints/*.cs`
(`AuthEndpoints`, `ProfileEndpoints`, `FindingsEndpoints`, `IngestEndpoints`,
`UploadHistoryEndpoints`, `HealthScoringEndpoints`, `ExecutivePortfolioEndpoints`,
`DataQualityEndpoints`, `ProgressEndpoints`, `ProjectKeysEndpoints`). Every endpoint that returns
a typed body must declare `.Produces<T>(200)` (+ `.Produces(404)` on the nullable → 404 patterns)
so the OpenAPI generator emits a response schema — the Layer-1 drift sensor depends on this. See
`OpenApiDriftTest` and `OpenApiRuntimeContractTest` (both in `AiPMOInsight.Api.Tests`). Exempt:
`AuthEndpoints` — every route returns a bare `Results.Ok()` / `Results.Problem(...)` with no typed
response body, so there's no schema for `.Produces<T>` to describe.

**Security.** `AuthCookies` centralises cookie flags (httpOnly, SameSite=Strict, per-path scoping);
`TokenService` signs HS256 JWTs; `RefreshTokenService` issues/rotates/revokes with a configurable,
default-7-day fixed (non-sliding — rotation inherits the original expiry) TTL (see decision log).
`Program.cs` `JwtBearerEvents.OnMessageReceived` pulls the access token
out of the cookie — the Authorization header is never used by the browser client.

**Persistence.** `AppDbContext` (`AiPMOInsight.Infrastructure/Persistence`) is
`IdentityDbContext<AppUser>`. Migrations under `AiPMOInsight.Infrastructure/Migrations/`. EF is the
schema authority — no hand-written SQL to keep in sync.

**Health scoring.** `HealthScoringService` (`AiPMOInsight.Application/Features/HealthScoring`) is
pure/deterministic. All weights/thresholds/overrides come from `HealthScoringOptions`, bound and
`Validate()`d at startup. **Shipped numbers are PRD placeholders** (`IsPlaceholder: true`).

**LLM routing.** `RoutingLlmClient` (`AiPMOInsight.Infrastructure/Analysis/Llm`) is the single
`ILlmClient` — it dispatches per-agent to an inner `ILlmClient` built by `ILlmClientFactory`.
Provider (`fake` | `anthropic` | `openai`) selectable per-agent via `Llm.Agents.<SkillName>`
config; no code change to swap providers.

**Integration test host.** `TestWebAppFactory` (`AiPMOInsight.Api.Tests`) swaps EF for an in-memory
provider and installs a header-driven `TestAuthHandler` so tests need no real login. Use
`factory.CreateClientAs("test-user", "admin", "user")`.

---

## 4. Code style via examples

**Canonical vertical slice + endpoint + integration test.** Match this shape; don't invent new
ones without cause.

```csharp
// -- Application/Features/<Feature>/<Verb><Noun>.cs -------------------------------------------
public static class SummarizeThing
{
    // Nullable Result → endpoint maps null to 404; non-null → 200.
    public sealed record Query(string Key) : IRequest<Result?>;

    public sealed record Result(string Key, IReadOnlyList<ItemView> Items);
    public sealed record ItemView(string Name, string Severity, string CitationLocator);

    internal sealed class Handler(IFindingRepository findings) : IRequestHandler<Query, Result?>
    {
        public async Task<Result?> Handle(Query request, CancellationToken cancellationToken)
        {
            var all = await findings.GetByProjectKeyAsync(request.Key, cancellationToken);
            if (all.Count == 0) return null;                    // → 404 at the endpoint

            var items = all.Select(f => new ItemView(
                f.Summary, f.Severity!.Value.ToString(), f.Citation.Locator)).ToList();
            return new Result(request.Key, items);
        }
    }
}

// -- Api/Endpoints/ThingEndpoints.cs ---------------------------------------------------------
public static class ThingEndpoints
{
    public static IEndpointRouteBuilder MapThingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/things").WithTags("Thing").RequireAuthorization();

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SummarizeThing.Query(key), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("SummarizeThing")
        .Produces<SummarizeThing.Result>(StatusCodes.Status200OK)   // required — Layer-1 sensor
        .Produces(StatusCodes.Status404NotFound);                    // required if handler returns null
        return app;
    }
}

// -- tests/AiPMOInsight.Api.Tests/ThingEndpointsTests.cs -------------------------------------
public class ThingEndpointsTests
{
    [Fact]
    public async Task Unknown_key_returns_404()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("test-user", "admin", "user");

        var response = await client.GetAsync("/api/things/UNKNOWN");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

**Conventions:** slice types are `sealed record`; `Handler` is `internal sealed class`; primary-
constructor DI (`Handler(IRepo repo)`); enums surface at the read boundary as `string` (never
raw ints). Comments explain the **why** — non-obvious constraints, security invariants, POC
placeholder status. Don't restate what the code already says.

---

## 5. Boundaries (semantic rules only)

Automation rules (auto-accept, deny) live in `.claude/settings.json`; this section is what the
agent must not do based on domain knowledge not obvious from the code.

**Always (Auto-accept):**
- New slice + endpoint + integration test following section 4's pattern.
- New `HealthArea` finding kind that stays out of the score (mirror the `Scope` precedent).
- Reformatting, dead-code removal, tests around existing behavior.

**Ask first (Default):**
- Repo-wide refactors touching more than one slice (mediator shape, DI wiring, conventions).
- Schema migrations (destructive columns, renames, indexes on hot tables).
- Any change that would regenerate the drift baseline as a side effect.
- New LLM provider adapter or provider-selection semantic change.
- Changes to OpenSpec proposals under `openspec/` — review-gated design docs.

**Never (Blocked):**
- Commit `Jwt__SigningKey`, `Llm__*__ApiKey`, `AppDb__ConnectionString` values (env / user-
  secrets only; the `appsettings.json` dev value is intentional and non-production).
- Introduce per-user data scoping — model is **shared workspace, all authenticated callers see
  everything**. `Where(x => x.UserId == currentUser)` is a spec change, not a cleanup.
- Treat any `HealthScoring` / `DataQuality` / "POC" threshold as client-agreed — they are PRD
  placeholders; preserve `IsPlaceholder` + startup-warning wiring.
- Fabricate UI data for signals the backend doesn't emit — render a dashed "not yet captured"
  placeholder instead.
- Run migrations on startup in Production (`DbInitializer.MigrateAndSeedAsync` is
  `IsDevelopment()`-guarded; Prod applies as a deploy step).
- Return auth tokens in a response body — both cookies only, `httpOnly` + `SameSite=Strict`.
- Bypass the drift sensor by editing the test or baseline by hand — regenerate via the
  `UPDATE_OPENAPI_BASELINE=1` command in section 2 and review the diff.

---

## 6. Implementation status

Not a working status board — git / PRs / issue tracker are authoritative for day-to-day state.
This section is a one-glance current-phase summary so a cold-start agent knows the shape of the
world. See [SHIPPED.md](SHIPPED.md) for the per-feature log.

- **Phases shipped:** 4 (RAG health scoring, 2026-07-15) → 5 (three-level dashboards L1/L2/L3 +
  auth UI + history rich detail, 2026-07-15 → 2026-07-20; L2 and L3 follow-on registers both
  closed).
- **Most recent branch:** `feat/spec-drift` — spec-drift sensor Layers 1+2 shipped 2026-07-21.
- **No active phase.** Next work is user-driven.
- **Blocked:** none.

---

## 7. Decision log

Twelve dated architectural decisions moved out to [CLAUDE-decisions.md](CLAUDE-decisions.md) —
load on demand when a change might contradict a past decision. Each entry has a date/phase tag
and the rationale.
