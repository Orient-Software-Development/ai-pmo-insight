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

> **Dashboards (Phase 5, Level 2 — `add-project-status-dashboard`):** the **individual-project status
> rich view** (React, route `/projects`, `ProjectFindings.jsx`). **Presentation-only — no backend/API
> change:** the view reads the two existing surfaces for a project key **concurrently** (`Promise.allSettled`)
> — the findings surface (`GET /api/projects/{key}`) and the health surface (`GET /api/projects/{key}/health`)
> — and renders `HealthBanner` above the four cited sections (narrative/findings/challenge/review). The
> banner shows the RAG colour (`FinalBucket`) + `RawScore`, the per-area breakdown, aggregate `Confidence`,
> the applied-override audit trail (rule/floor/reason + cited finding locator), and the **"Needs PM Review"**
> flag (orthogonal to colour). The health response maps to one of four render states via the pure helper
> `healthState` (`ClientApp/src/health.js`): **SCORED** (200 + score), **SCORING_PENDING** (200 + null
> score — findings exist, nothing scoreable yet), **NOT_SCORED** (404 — no findings on record), **ERROR**
> (network/5xx/401, surfaced via the page error line, never a banner); the two surfaces are independent so
> one failing never blanks the other. RAG colours are theme-aware CSS custom properties in `styles.scss`
> and always paired with a text label + score (colour-blind safe). Status never conveyed by colour alone.
> Where the PRD's L2 wishlist exceeds the finding shape (dated milestones, per-decision owner/deadline,
> explicit AI recommendation) the view renders what exists and flags the gap as a follow-on. **L1
> (Executive Portfolio) and L3 (Data Quality) remain unbuilt** — they need a portfolio-enumeration query
> (`DistinctProjectKeys` + a `ScorePortfolio` fan-out over the pure `HealthScoringService`), deferred to
> later Phase 5 changes. The repo has **no JS test harness**; the L2 data path is locked by the backend
> integration test `ProjectStatusDashboardDataTests` and the render logic verified via the running app.

> **Dashboards (Phase 5, Level 1 — `add-executive-portfolio-dashboard`):** the **executive portfolio
> roll-up** (React, route `/portfolio`, `ExecutivePortfolio.jsx`). Unlike L2 (presentation-only), this
> adds a **real backend slice**: portfolio-wide **discovery** via `IFindingRepository.DistinctProjectKeysAsync`
> (`SELECT DISTINCT project_key` — no first-class `Project` entity; opaque-key model preserved; no schema
> change/migration) and a `ScorePortfolio` slice (`Application/Features/ExecutivePortfolio`) that fans out
> over the **existing pure `HealthScoringService`** (latest run per project — no re-analysis, no LLM cost)
> and rolls up. Exposed at `GET /api/portfolio` (`ExecutivePortfolioEndpoints`; authorized, shared-workspace,
> view-only): **G/A/R counts** (count of each `FinalBucket`), **aggregate (mean) confidence** + count of
> projects flagged `NeedsPmReview`, and a worst-first **intervention list** (Red-before-Amber, then
> `RawScore` ascending) — each entry carries the project key, status, confidence, and a **cited reason**
> (the worst-floor applied override if any, else the worst-severity area cited to its worst finding).
> Unscoreable projects (null `Score`) are excluded from counts; empty store → **zeroed 200, never 404**.
> The L1 view is built to the v2 wireframe (`docs/designs/phase5-wireframe-v2.html`) with a **shared SCSS
> design system** (summary strip, RAG bar, `records` table, `sev` chips) reusing L2's `--rag-*` properties,
> ready for the L2 retrofit. **Presentation-only boundary holds:** panels the roll-up can't back (€
> financial exposure, per-decision detail, key-person risk, owned/dated recommendations) render a dashed
> "not yet captured — follow-on" placeholder, never fabricated data. Backend is TDD-covered
> (`FindingRepositoryDistinctKeysTests`, `ScorePortfolioTests`, `ExecutivePortfolioEndpointsTests`); **L3
> Data Quality will reuse `DistinctProjectKeysAsync`** for enumeration.

> **Analyze flow UI + L2 retrofit (Phase 5, `add-analyze-flow-and-l2-retrofit`, #38):** two UI-only
> wireframe pages, **presentation-only — no backend/API/finding-shape change.** (1) A new **`/upload`
> cold-start page** (`Upload.jsx`, `RequireAuth`) extracts the upload → analyze flow out of
> `ProjectFindings.jsx` into its own surface — drop zone (accepts `.xlsx .xlsm .xml .docx`; CSV rejected
> up front, unchanged), a "this upload" panel, and a **coarse request-lifecycle pipeline stepper**
> (uploading → analyzing → done/failed over `POST /api/ingest/upload` then `POST /api/analyze/{id}`). It is
> the **post-login landing route** (`Login.jsx` → `/upload`; coordinates with the auth-UI change #33). On
> success it links to `/projects?key=<analyzed key>`. (2) The **L2 view** (`/projects`,
> `ProjectFindings.jsx`) is **retrofitted onto the shared Phase 5 design system** L1 established (`--rag-*`,
> `records`, `sev`, `eyebrow`, `block`, `flagged-*`): a project header (key + name, RAG chip from
> `FinalBucket`, confidence, a **score-overridden** indicator when `FinalBucket≠RawBucket`, a project
> switcher) above the `HealthBanner` (score audit) and the four cited sections. The **data path is
> unchanged** — the `Promise.allSettled` findings+health read and the `healthState` mapping are preserved,
> and `?key=` auto-loads on mount; the change is styling/layout only, so **`ProjectStatusDashboardDataTests`
> stays green** (the repo has no JS harness — the render is `/verify`-checked in the running app).
> **Presentation-only boundary holds** (dashed placeholders, never fabricated): per-file parse status,
> **duplicate-identity merge (US-2)**, **live per-agent progress (US-9)** on `/upload`; **dated milestones**
> and **per-decision owner/deadline** on L2 (the Narrative stays the closest recommendation surface).

> **Client framework:** template param `--client-framework` (`-cf`) = `react` (default) or
> `none` (API only). Driven by `ClientFramework` symbol → computed `UseReact` / `UseApiOnly`,
