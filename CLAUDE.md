# CLAUDE.md

Guidance for Claude Code working in this repo. Reorganised into 6 sections following
"living-spec" practice, with the shipped-features log moved out to [SHIPPED.md](SHIPPED.md)
to keep this file lean. Read a specific SHIPPED.md block when the task touches that surface
(auth, health scoring, dashboards, etc.); otherwise it is not needed every session.

Anti-patterns explicitly avoided here (see [SHIPPED.md](SHIPPED.md) `spec-drift-sensor` blocks
for context): (a) no `file:line` refs — symbols only, so refactors don't rot the doc;
(b) no "Implementation Status" section — git branches, PRs and the issue tracker are the
authoritative state; (c) automation rules (auto-accept, deny) live in `.claude/settings.json`,
not in prose here.

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

# Local Postgres for dev / running the API outside tests.
docker-compose up -d postgres
dotnet run --project source/AiPMOInsight.Api

# React dev server (proxies to the API).
cd source/AiPMOInsight.Api/ClientApp && npm install && npm run dev
```

**Config the agent must NOT commit values for** (env / user-secrets only): `Jwt__SigningKey`,
`Llm__Default__ApiKey`, `Llm__Agents__<SkillName>__ApiKey`, `AppDb__ConnectionString`.

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
`IUploadRepository`, `IFindingRepository`, `ILlmClient`. Implementations are in
`AiPMOInsight.Infrastructure` and `AiPMOInsight.Api/Security`.

**Endpoints** are Minimal-API extension methods in `AiPMOInsight.Api/Endpoints/*.cs`
(`AuthEndpoints`, `ProfileEndpoints`, `FindingsEndpoints`, `IngestEndpoints`,
`UploadHistoryEndpoints`, `HealthScoringEndpoints`, `ExecutivePortfolioEndpoints`,
`DataQualityEndpoints`, `ProgressEndpoints`, `ProjectKeysEndpoints`). Every endpoint must
declare `.Produces<T>(200)` (+ `.Produces(404)` on the nullable → 404 patterns) so the OpenAPI
generator emits a response schema — the Layer-1 drift sensor depends on this. See
`OpenApiDriftTest` and `OpenApiRuntimeContractTest` (both in `AiPMOInsight.Api.Tests`).

**Security.** `AuthCookies` centralises cookie flags (httpOnly, SameSite=Strict, per-path scoping);
`TokenService` signs HS256 JWTs; `RefreshTokenService` issues/rotates/revokes with a 7-day fixed
TTL (see decision log). `Program.cs` `JwtBearerEvents.OnMessageReceived` pulls the access token
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

Automation rules (auto-accept commands, deny lists) live in `.claude/settings.json`. This section
is what the agent must NOT do based on domain knowledge that isn't obvious from the code.

**Never:**
- Commit `Jwt__SigningKey`, `Llm__*__ApiKey`, or `AppDb__ConnectionString` values (env / user-
  secrets only). The dev value in `appsettings.json` is intentional and non-production.
- Introduce per-user data scoping without an explicit spec change — the model is
  **shared workspace, any authenticated caller sees everything**. Adding a `Where(x => x.UserId
  == currentUser)` is a design-changing decision, not a cleanup.
- Treat `HealthScoring`, `DataQuality`, or "POC" thresholds as client-agreed — they are PRD
  placeholders. Preserve the `IsPlaceholder` / startup-warning wiring.
- Fabricate UI data for signals the backend doesn't yet emit. Render a dashed "not yet captured
  — follow-on" placeholder instead.
- Run migrations on startup in Production (`DbInitializer.MigrateAndSeedAsync` is guarded
  `IsDevelopment()`; production applies migrations as a deliberate deploy step).
- Return auth tokens in a response body — access + refresh both travel only as `httpOnly`
  cookies.
- Bypass the drift sensor by editing the test or the baseline manually — regenerate with
  `UPDATE_OPENAPI_BASELINE=1 dotnet test --filter "…OpenApiDriftTest"` and review the diff.

**Ask first:**
- Repo-wide refactors touching more than one slice (mediator shape, DI wiring, endpoint
  conventions).
- Schema migrations (destructive columns, rename cascades, index changes on hot tables).
- Anything that would require regenerating the drift baseline as a side effect of an unrelated
  change.
- Adding a new LLM provider adapter or changing provider-selection semantics.
- Changing OpenSpec proposals under `openspec/` — those are review-gated design docs.

**Safe / auto:**
- Adding a new slice + endpoint + integration test following the pattern in section 4.
- Adding a new `HealthArea` finding kind that stays out of the score (mirror the `Scope`
  precedent).
- Reformatting, dead-code removal, adding tests around existing behavior.

---

## 6. Decision log

Load-bearing choices future agents keep re-litigating. Recorded once; contradict only with
explicit user consent.

**JWT in httpOnly cookies, not Authorization header.** Security: XSS-safe. Both tokens
`SameSite=Strict` (no separate CSRF token needed). Refresh token has a **fixed 7-day TTL, not
sliding on rotation** — an inactive session must eventually die even if used moments before
expiry.

**No per-user scoping on findings / uploads / projects.** Shared workspace by product decision;
any authenticated caller sees everything. If a per-user story appears, it's a new spec, not a
security fix.

**No first-class `Project` entity.** Project keys are opaque strings discovered from
`IFindingRepository.DistinctProjectKeysAsync` (`SELECT DISTINCT project_key`). Preserved because
projects have no lifecycle of their own — they exist only as far as findings reference them.

**Scoring is a re-runnable query, not a pipeline step.** `HealthScoringService` is pure and runs
on demand — no persisted score column, no re-analysis needed to see the current bucket. Enables
config changes to take effect without re-running paid LLM analysis.

**All health-scoring / DQ numbers ship as EXAMPLE placeholders.** The startup warning is the
guardrail. No score, threshold, or override in production config is client-agreed until PMO
kickoff replaces the block.

**Provider (Anthropic / OpenAI / fake) selectable per-agent via config alone.** Adapter code and
per-agent registration go through `RoutingLlmClient` + `ILlmClientFactory`. No agent, prompt, or
orchestrator code changes to swap providers.

**Response types declared on every endpoint via `.Produces<T>()`.** Otherwise minimal-API
handlers return opaque `IResult`, the OpenAPI doc omits response schemas, and the Layer-1 drift
sensor can't detect field-level drift. Non-negotiable — an endpoint without `.Produces<T>()` is
an incomplete endpoint.

**Layer-2 runtime contract test is GET-only.** POST endpoints require valid request bodies per
schema, which would duplicate the hand-written `AuthEndpointsTests` / `*EndpointsTests`. Revisit
only if a POST-side runtime bug slips through.

**Layer-3 (LLM semantic drift over CLAUDE.md invariants) is deliberately unbuilt.** Non-
deterministic, high cost per PR, no established best practice. Layers 1+2 must prove insufficient
in practice first.

**Migrations run in Dev auto, in Prod deliberately.** `DbInitializer.MigrateAndSeedAsync` is
`IsDevelopment()`-guarded. Production runs `dotnet ef database update` as a deploy step. Never
change this without an accompanying rollback plan.

**TDD for OpenSpec-tracked changes.** OpenSpec proposals under `openspec/` are implemented
red-green-refactor; the suite must be green before checking off a task.

**Integration tests use EF in-memory, not mocked repositories.** `TestWebAppFactory` swaps the
Npgsql `AppDbContext` for the EF in-memory provider; all finding/upload/health endpoint tests
exercise real repository code paths against it. If you find yourself reaching for a mocked
`IFindingRepository` in an integration test, use `TestWebAppFactory` instead — mocks belong in
handler unit tests, not endpoint tests.
