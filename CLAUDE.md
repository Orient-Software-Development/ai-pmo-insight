# CLAUDE.md

Guidance for Claude Code working in this repo. Keep current as the project grows.

## What this is

A **Clean-Architecture .NET service** template, packaged as a `dotnet new` template
(`.template.config/template.json`, `sourceName: AiPMOInsight`). Vertical-slice CQRS over a
**lightweight in-process mediator** (`AiPMOInsight.Application/Messaging`, no MediatR), an optional
**React + Vite SPA** (`source/AiPMOInsight.Api/ClientApp`), a Widgets aipmoinsight, and `/health`. Ships
with GitHub Actions CI and Claude skills.

> **Persistence:** EF Core + **PostgreSQL** (`Npgsql`) for runtime queries only. `AppDbContext`
> lives in `AiPMOInsight.Infrastructure/Persistence`; the Widgets slice persists through it via
> `EfWidgetRepository` (port = `IWidgetRepository` in Application). Connection string key
> `ConnectionStrings:AppDb` (appsettings + env). Local Postgres via `docker-compose.yml`.
>
> **Schema migrations:** owned by **EF Core migrations** (`dotnet-ef` in the tool manifest).
> Migration code lives in `source/AiPMOInsight.Infrastructure/Migrations/`; the design-time context is
> built by `AppDbContextFactory` (override the connection with the `AppDb__ConnectionString` env
> var). Covers the Widgets slice **and ASP.NET Core Identity** tables (auth added Identity, which
> is EF-migration-native). Add a migration with `dotnet ef migrations add <Name> --project
> source/AiPMOInsight.Infrastructure --startup-project source/AiPMOInsight.Api`; commit the generated files.
> Apply with `dotnet ef database update` (same project flags). **In Development the API
> auto-migrates and seeds** via `DbInitializer.MigrateAndSeedAsync` (`Program.cs`, guarded to
> `IsDevelopment()`); in production migrations run as a deliberate deploy step (see
> `deploy.yaml`), never on startup. EF column mappings (`WidgetConfiguration`, snake_case) define
> the schema — there is no separate hand-written SQL to keep in sync.

> **Auth:** **JWT, cookie-transported**, **role-based** (RBAC). Design spec: `docs/authentication.md`.
> Both tokens travel **only as `httpOnly` cookies** — never in the response body or readable by JS
> (XSS-safe), with `Secure` (tracks request scheme), `SameSite=Strict` (CSRF-safe, no separate
> token). The **access token** (HS256 JWT, 15 min) is scoped `Path=/`; the **refresh token**
> (opaque, SHA-256-hashed in `refresh_tokens`, **fixed 7-day TTL — not reset on rotation**) is
> scoped `Path=/api/auth/refresh`. Cookie read/write/clear is centralized in `AuthCookies`
> (`AiPMOInsight.Api/Security`); `AuthCookies` flags + `JwtBearerEvents.OnMessageReceived` (in `Program.cs`,
> pulls the access token from the cookie) are the only cookie-specific wiring. ASP.NET Core Identity
> is the **user store** (`AddIdentityCore<AppUser>` — `UserManager`/`RoleManager`/PBKDF2 hashing),
> `AppUser : IdentityUser` + `AppDbContext : IdentityDbContext<AppUser>`. `AuthEndpoints` exposes
> `POST /api/auth/register`, `POST /api/auth/login` (sets both cookies), `POST /api/auth/refresh`
> (rotates the refresh cookie; reuse → revoke whole chain → 401), and `POST /api/auth/logout`
> (authorized; revokes all the caller's refresh tokens + clears cookies). Identity is separate:
> `GET /api/profile/me` (`ProfileEndpoints`). `TokenService` signs HS256 tokens; `RefreshTokenService`
> (`AiPMOInsight.Infrastructure/Security`) issues/rotates/revokes. Config = `Jwt` section (`SigningKey`
> dev value in `appsettings.json`; override via `Jwt__SigningKey` secret in prod). `DbInitializer`
> seeds roles (`admin`, `user`) + optional dev admin (`Seed:Admin:*`). Endpoints opt in with
> `.RequireAuthorization()` / `.RequireAuthorization(p => p.RequireRole("admin"))`. Handlers read
> the caller via the `ICurrentUser` port (Application), implemented by `CurrentUser` (Api) over
> `HttpContext.User`. Integration tests bypass auth with a header-driven `TestAuthHandler`
> (`TestWebAppFactory.UseTestAuthentication`); `AuthEndpointsTests` exercises the real cookie/JWT
> pipeline end-to-end.

> **History (read surface):** two authenticated read-only endpoints under `/api/uploads` browse past
> uploads and their analysis — `GET /api/uploads` lists uploads newest-first (id/fileName/uploadedAt,
> never content) and `GET /api/uploads/{id}/findings` returns that upload's **latest** run in the same
> four sections as the project view (analysis/narrative/challenge/review); unknown id → 404, known
> upload with no findings → 200 empty. Slices `GetUploads` / `GetUploadFindings`
> (`Application/Features/History`) over `IUploadRepository.ListAsync` +
> `IFindingRepository.GetByUploadIdAsync` (findings link to an upload via the owned
> `citation_upload_id` column, indexed by the `AddCitationUploadIdIndex` migration). Endpoints in
> `UploadHistoryEndpoints`; React `History` page at `/history`. **Shared-workspace visibility** — any
> authenticated caller sees all uploads (no per-user scoping; `Upload` has no `UserId`). View-only:
> no re-analyze, delete, search, or pagination. Multi-file batch grouping deferred to
> `add-multi-file-analyze`.

> **LLM routing:** the four LLM-backed agents (`RiskAndIssue` #4, `Narrative` #7, `Challenge` #8,
> `Review` #9) reach the model through the single `ILlmClient` port, but **provider selection is
> per-agent via config alone** — no agent, prompt, or orchestrator code changes to swap providers.
> Config is an `Llm.Default` block plus optional `Llm.Agents.<SkillName>` overrides (keyed by the
> agent's `SkillName`, case-insensitive; any field present on an agent block overrides that field
> of `Default`, else inherits it — `LlmOptions.ResolvedFor`). A missing agent block falls back to
> `Default`. `AddInfrastructure` binds the options, folds the legacy flat `Llm.Provider`/`ModelId`/
> `ApiKey` shape into `Default` (a one-release back-compat; explicit `Default` wins), asserts every
> `Llm.Agents` key is one of the four known skills, then builds one inner `ILlmClient` **per agent
> eagerly** via `ILlmClientFactory` and registers a `RoutingLlmClient` (`Infrastructure/Analysis/
> Llm`) as the sole `ILlmClient` — it dispatches each `CompleteAsync` by `LlmRequest.SkillName`
> (constant-time dictionary lookup, no per-request factory calls). The factory recognises `fake`
> (fully working, the no-API-key demo/test path), `anthropic`, and `openai`. **`anthropic` is a
> working adapter** (`AnthropicLlmClient`, issue #27): it calls the Anthropic Messages API via the
> official `Anthropic` NuGet SDK, requesting **structured JSON output** constrained to a schema
> derived from the `TOutput` contract (`JsonSchemaGenerator`, with a fixed-audience-key override for
> `ReviewResult`'s dynamic dictionary) and deserialising the returned text block into `TOutput` —
> never free-text parsing. It honours the resolved `ModelId` (default `claude-opus-4-8`), `ApiKey`,
> and `PerAnalysisTokenBudget` (→ `MaxTokens`), maps typed `Anthropic.Exceptions.*` to an
> `LlmProviderException` (naming provider + skill, never the key), and respects the `CancellationToken`.
> **`openai` is also a working adapter** (`OpenAiLlmClient`): it calls the OpenAI Chat Completions
> API via the official `OpenAI` NuGet SDK, requesting the **same structured JSON output** (the
> `JsonSchemaGenerator` schema is fed to `ChatResponseFormat.CreateJsonSchemaFormat(strict)`, whose
> subset matches the generator's) and deserialising the returned text into `TOutput`. It honours the
> resolved `ModelId` (default `gpt-4o-mini`), `ApiKey`, and `PerAnalysisTokenBudget`
> (→ `MaxOutputTokenCount`), maps `System.ClientModel.ClientResultException` to an
> `LlmProviderException` (naming provider + skill, never the key), and respects the
> `CancellationToken`. A missing key is a request-time provider failure, not a startup one (eager DI
> construction uses a placeholder so a prod-shape config still boots). An **unknown provider fails at startup**, never mid-request; a startup log line lists the
> resolved provider per agent (never the `ApiKey`). `ApiKey` is supplied only via env/secret
> (`Llm__Default__ApiKey`, `Llm__Agents__<SkillName>__ApiKey`), never committed.

> **Health scoring (Phase 4, `add-health-scoring`):** the per-project **Red/Amber/Green (RAG)** health
> score ("RAG" here = the health colour, never retrieval-augmented generation). **Findings are
> self-describing:** an `Analysis` finding now carries a structured `HealthArea`
> (Schedule/Budget/Risk/Resource/DataQuality) and `Severity` (Green/Amber/Red) — the deterministic
> agents (#2 DataQuality, #3 Status, #5 Financial, #6 Resource) plus the RAID/minutes agent (#4 Risk)
> stamp them via `FindingFactory` (surfacing the RAG band they already compute), and `Finding.Create`
> enforces `Kind==Analysis ⇒ Area+Severity non-null` (mirrors the citation invariant); non-analysis
> findings leave both null. Enums persist as strings (`area`/`severity` columns,
> `AddFindingAreaSeverity` migration). **Scoring is a re-runnable query, not a pipeline step**
> (`Application/Features/HealthScoring`): `HealthScoringService` is pure/deterministic — resolve the
> **latest run per project** (newest `CreatedAt`, so a run spanning projects still keys per project),
> group its `Analysis` findings by `Area`, reduce each area to its **worst** severity, map via the
> configured Severity→number table, take a **weight-normalised weighted average** over the areas
> present (absent areas don't dilute), and bucket by inclusive lower-bound thresholds → `rawScore` +
> `rawBucket`. **Overrides set a worst-case floor** (`min Red` beats `min Amber` beats raw; a floor
> never lowers severity; an absent signal never fires) and the result is **auditable** (`rawScore`,
> `rawBucket`, ordered `appliedOverrides` naming the tripping finding + citation, `finalBucket`,
> aggregate `confidence`, per-area breakdown). Very-low aggregate `confidence` → a separate
> **"Needs PM Review"** flag, orthogonal to the colour. All weights/thresholds/mappings/overrides are
> **external, validated config** (`HealthScoringOptions`, `HealthScoring` section in appsettings —
> **JSON, not YAML**; bound + `Validate()`d at startup in `AddInfrastructure`, failing fast and naming
> the offending key). **The shipped numbers are the PRD's EXAMPLE placeholders** (`IsPlaceholder:true`,
> header-commented; a startup **warning** log line says so) — replace with PMO-agreed values before
> go-live. Read surface: `GET /api/projects/{projectKey}/health` (`HealthScoringEndpoints` →
> `ScoreProject` slice; authorized, view-only; unknown project → 404, findings-but-nothing-scoreable →
> 200 with a null `Score`). Dashboard consumption of the score is Phase 5.

> **Client framework:** template param `--client-framework` (`-cf`) = `react` (default) or
> `none` (API only). Driven by `ClientFramework` symbol → computed `UseReact` / `UseApiOnly`,
