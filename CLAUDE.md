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
> Where the PRD's L2 wishlist exceeded the finding shape at the time this change shipped (dated
> milestones, per-decision owner/deadline, explicit AI recommendation), the view rendered what existed
> and flagged the gap as a follow-on — since closed (see the L2 register-closure entry below). **L1
> (Executive Portfolio) and L3 (Data Quality)**, unbuilt when this change shipped, followed in later
> Phase 5 changes (below) using the same shape: a portfolio-enumeration query (`DistinctProjectKeys` +
> a `ScorePortfolio`/`SummarizeDataQuality` fan-out over the pure `HealthScoringService`). The repo has
> **no JS test harness**; the L2 data path is locked by the backend integration test
> `ProjectStatusDashboardDataTests` and the render logic verified via the running app.

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
> **Presentation-only boundary holds** (dashed placeholders, never fabricated): per-file parse status and
> **live per-agent progress (US-9)** on `/upload` remain follow-ons. **Duplicate-identity merge (US-2)**
> and **dated milestones / per-decision owner-deadline** on L2, flagged here at the time, have since
> landed — see the L3 and L2 register-closure entries below.

> **Auth UI rebuild + token-base retrofit (Phase 5, `add-phase5-auth-ui`, #33):** the three auth surfaces
> rebuilt to the wireframe, **presentation-only — no `/api/auth/*` / cookie / JWT / Identity change.**
> `Login.jsx` is a centered `.auth-card` with a `Log in` / `Create account` tab toggle (submit label
> follows mode), a Register-mode-only ASP.NET Identity rules hint, and a red-stripe `.auth-error` panel;
> post-login `navigate('/upload')` preserved. `ChangePassword.jsx` is a `.settings-card` (three fields +
> rules hint under "new") with a green `.success-panel` restating the fresh-session behaviour; cancel is
> `navigate(-1)` with `/` fallback (no more hard-coded `/projects`); success panel resets on unmount.
> `NavMenu.jsx` replaces the flat right-hand link list with an **avatar-chip + email + chevron** trigger
> opening a **disclosure** popover (deliberately *not* an ARIA menu — no `role="menu"`, no roving
> tabindex, no arrow-key nav; Tab is sufficient for two items and screen readers get an honest
> contract) with a header (email + role chip from `user.roles.join(' · ')`) and two items: `<Link>`
> Change password + danger-styled `<button>` Log out. Panel closes on outside `mousedown` (not `click`,
> to avoid a re-render race), Escape (returns focus to the trigger), and route change. Nav-tabs +
> user-menu are hidden on `/login` via `useLocation().pathname === '/login'` (not a body class). The
> wireframe token base — `--paper` / `--ink*` / `--panel*` / `--rule*` / `--accent*` / `--sev-*` / `--font-display`
> (Georgia) / `--font-ui` (system) / `--font-mono` (ui-monospace) — lands in `styles.scss` on `:root` with
> a `@mixin wireframe-dark` mirror under `data-theme='dark'` + `prefers-color-scheme: dark`. Strategy is
> **hybrid**: Pico stays underneath for form/reset/button primitives; new or retrofitted selectors
> reference **only wireframe tokens** (a design-system comment at the top of `styles.scss` records the
> rule). L1 (`ExecutivePortfolio.jsx`) and L2 (`ProjectFindings.jsx`) retrofitted in the same change to
> consume the new tokens — CSS-only, no JSX or data-path change; `AuthEndpointsTests` (14),
> `ProjectStatusDashboardDataTests` + `ExecutivePortfolioEndpointsTests` (5), and the full backend suite
> (227) all stay green. Avatar initials derived from `user.userName` (split local on `.`/`-`, first
> letter of first two parts; fallback to `??`).
> **History rich detail (Phase 5, `add-history-rich-detail`, #36):** the `/history` page (`History.jsx`)
> rebuilt into a **master-detail audit surface** (US-9/US-10), **presentation-only — no backend/API/
> finding-shape change.** A sticky master list of uploads (newest-first, from `GET /api/uploads`) + a detail
> panel for the selected upload's latest run: a **run-provenance header** (run id, run date, distinct prompt
> hash(es) — all derived from the findings response's `RunId`/`CreatedAt`/`PromptVersion`), the **four cited
> sections** (Analysis/Narrative #7/Challenge #8/Review #9) restyled on the shared design system, and a
> **Score-audit section (US-10)** that **reuses `GET /api/projects/{key}/health`** per distinct project key
> in the run (`Promise.allSettled`, independent) — rendered by reusing the L2 `HealthBanner` (bucket +
> cited applied-override trail). Because that read is per-project-**latest**-run, the section is labelled the
> project's **current** health with an explicit caveat that a strict **per-run historical** audit is a
> follow-on (chosen over adding a backend read, to keep the change presentation-only). Strictly **read-only**
> (no re-analyze/delete/edit/search/pagination). Render mapping in a pure `history.js` helper
> (`uploadStatus`/`runProvenance`/`projectKeys`; mirrors `health.js`/`dataQuality.js`), reusing
> `bucketColour`+`healthState`. **Presentation-only boundary holds** (flagged, never fabricated): **uploader**
> (`Upload` has no `UserId`), **LLM model** (not on `FindingView`), **project count**/**multi-file summary**
> (one file per upload; batch grouping = deferred `add-multi-file-analyze`), and **live Running/Failed
> status** (analysis is synchronous — only coarse Analyzed/Not-analyzed is derivable). No JS harness — the
> data path is locked by the existing `UploadHistoryEndpointsTests` + health-endpoint tests; render
> `/verify`-checked in the running app.

> **Dashboards (Phase 5, Level 3 — `add-data-quality-dashboard`):** the **Data Quality read surface**
> (React, route `/data-quality`, `DataQuality.jsx`) — the last of the three dashboard levels. Like L1 it
> is a **read over the findings store, not new analysis** (no LLM): a `SummarizeDataQuality` slice
> (`Application/Features/DataQuality`) enumerates projects via L1's `IFindingRepository.DistinctProjectKeysAsync`,
> and per project collects its **latest run's** `Area==DataQuality` findings (same latest-run resolution the
> scorer uses — **no new repository method, no schema change/migration**). It rolls them up into: a
> **confidence block** — the mean of each **scored** project's aggregate `HealthScore.Confidence` (reusing
> the pure `HealthScoringService`, so L3 never disagrees with L1/L2), the configured **publish threshold**
> (`HealthScoringOptions.ConfidenceFloor`, injected — not forked), and a **below-target** flag; a
> **worst-first cited items list** (one entry per DataQuality finding: project · issue=`Summary` ·
> `Severity` · citation locator; ordered Red→Amber→Green, key+locator tiebreak); and **counts** (total +
> per-project). Exposed at `GET /api/data-quality/summary` (`DataQualityEndpoints`; authorized,
> shared-workspace, view-only); empty store → **zeroed 200, never 404**; unauthenticated → 401. Enums
> surface as strings. The L3 view is built to the v2 wireframe (`data-page="l3"`) on the shared Phase 5
> design system (`--rag-*`, `records`, `sev`, `eyebrow`, `flagged-panel`, plus a small `.conf-hero` block);
> render mapping in a pure `dataQuality.js` helper (mirrors L2's `health.js`). At the time this change
> shipped, the DataQuality finding carried only `Summary`+`Severity`+`Citation`+`Confidence`, so per-item
> **age**, **suggested remediation**, confidence-**lift** ordering, the **areas-completeness grid**, and
> **duplicate-identity candidates** were flagged follow-ons — all since closed (see the L3
> register-closure entry below). Backend is TDD-covered (`SummarizeDataQualityTests`,
> `DataQualityEndpointsTests`; the shared `Workbook` fixture yields no DQ findings, so a dedicated
> `OrbitFixtureBuilder.WorkbookWithDataQualityGap` seeds a deterministic milestone-no-due-date item). This
> **completes the three-level Phase 5 dashboard set** (L1 + L2 + L3).

> **L2 follow-on register closed (Phase 5, register #68, PRs #72 + #73):** the buildable L2 panels
> from the plan doc's 8-panel spec. `FindingView` (`GetProjectFindings`) now exposes each finding's
> `Area`/`Severity`/`MetricValue`/`MetricUnit`/`MetricDetail` — the shared read-API enabler every panel
> below consumes. **Decisions needed** (panel 6): `DecisionSkill` stamps structured
> `title`/`owner`/`deadline`/`consequence` on `MetricDetail` (emission unchanged — overdue=Red,
> due-soon=Amber, urgent-only scope); a dedicated worst-first table replaces the placeholder. **Key
> deviations** (panel 3): the flat findings table is grouped by area — Budget / Time / **Scope** /
> Resources — with **Risks & issues** (panel 4) and **Data quality** kept as their own sections (a
> product decision: risks stay separate from key deviations). **Scope** is `HealthArea.Scope` +
> `ScopeSkill` (a POC "unapproved-creep" rule: unapproved increase=Red, approved/open=Amber) —
> **display-only**, excluded from the health score/confidence (`HealthScoringService` filters it out;
> proven by a dedicated test) until the PMO agrees a real rule + weight. **Upcoming milestones** (panel
> 5): the Status agent's due-soon window widened 14→28 days (the doc's "next 2–4 weeks"); milestones
> carry `BaselineDate`/`IsCritical` — a **critical** milestone in trouble (overdue/missed/at-risk)
> escalates to Red regardless of its day-band, and **slip** (adjusted due − baseline) is surfaced as info
> only (does not itself raise severity in this version). **This-period progress** (panel 2): a new
> `SummarizeProgress` slice (`Application/Features/Progress`) + `GET /api/projects/{key}/progress`
> compares a project's two most recent runs — the health-score delta, a qualitative pace label (POC
> placeholder thresholds), and worst-first moved-forward/moved-backward lists (findings that
> cleared/improved vs. are new/worsened, matched by agent + citation locator). All of it TDD-covered;
> everything invented (Scope's rule, the pace thresholds, the milestone-critical escalation) is flagged
> `IsPlaceholder`/"POC" in code and the UI, per `docs/poc-data-rules-v0.md`.

> **L3 follow-on register closed (Phase 5, register #69):** the remaining L3 Data Quality items.
> `DataQualityOptions` (config-bound, `Validate()`d at startup — the same pattern as
> `HealthScoringOptions`) externalises the new POC thresholds. **Age** + **suggested remediation**: the
> DQ agent stamps the staleness age as a real `MetricValue`/`MetricUnit` (not only in the summary text)
> and attaches a deterministic remediation string per check type (a static rule-map, no LLM) via
> `MetricDetail`. **Duplicate-identity candidates (US-2)**: a POC heuristic (name-token Jaccard ×50% +
> same-customer ×30% + shared-resource ×20%, threshold ≥60) flags project pairs once per pair; the L3
> view's Merge/Keep-separate control only **records** the choice (client-side, this POC) and **never
> auto-merges**. **Per-risk staleness**: a RAID item not updated within 21 days is an Amber DQ finding.
> **Budget-actuals-missing**: `BudgetLineRecord.Actual` is now nullable; a missing actual is a DQ gap
> (`FinancialSkill` guards the null). **Confidence-lift ordering**: `SummarizeDataQuality` reconstructs
> each project's `DataQualitySignal` from a `signalKind` tag on every finding and re-runs
> `ConfidencePolicy` counterfactually, ranking items by **global** lift across the whole portfolio (a
> lift of 2 on one project outranks a lift of 1 on another — a design decision, not a client input).
> **Areas-completeness grid**: an 8-input-category (Schedule/Budget/Scope/Resources/Risks/Decisions/
> Minutes/Time — **not** the 5 `HealthArea` buckets) present/expected metric against a POC
> mandatory-field set, emitted as one informational Green finding per project, excluded from scoring.
> **Resource-plan vs. time-entries**: a new POC `TimeEntryRecord` + parser "Time" sheet backs a
> cross-source check (allocated-no-time / time-logged-unplanned). This closes all 8 items of the L3
> register; TDD-covered throughout. The one-time **`DEMO-TREND`** seed used earlier to demo the progress
> panel without two uploads has been removed from `DbInitializer` — a fresh database now starts at zero
> projects until something is actually uploaded and analyzed.

> **Spec-drift sensor (Layer 1, `feat/spec-drift`):** the API's live OpenAPI document
> (`/openapi/v1.json`, generated by `Microsoft.AspNetCore.OpenApi`'s `AddOpenApi()`/`MapOpenApi()`) is
> pinned by the committed baseline at repo-root `openapi.json`. The xUnit test `OpenApiDriftTest`
> (`tests/AiPMOInsight.Api.Tests/OpenApiDriftTest.cs`) boots the API via `TestWebAppFactory`, GETs the
> live doc, and **structurally** compares it to the baseline (parsed with `JsonDocument`; objects
> compared with keys sorted, arrays positionally — so key-order non-determinism doesn't flap, but
> real drift fails). Any added / removed / renamed endpoint, parameter, or DTO field fails the
> build so the baseline update is reviewed in the same PR as the code. To accept a deliberate API
> change, regenerate with `UPDATE_OPENAPI_BASELINE=1 dotnet test --filter FullyQualifiedName~OpenApiDriftTest`
> — the baseline is rewritten with **sorted keys + indented formatting** (stable across regenerations,
> regardless of the generator's internal key order) — and commit the diff. Baseline lookup walks up
> from `AppContext.BaseDirectory` to the first `.git` folder, so the READ and the UPDATE hit the same
> source file (no copy-to-output indirection). CI wiring: `.github/workflows/ci.yml` (`dotnet restore` →
> `build -c Release` → `test -c Release`) runs the full suite on every PR + push-to-main; the drift
> test lives inside `AiPMOInsight.Api.Tests` so it runs as part of that same job — no separate step.
> **Scope is Layer 1 only** (static shape drift): Layer 2
> (contract testing) is deliberately deferred — the API has one consumer (the in-repo React client)
> and the existing endpoint integration tests (`AuthEndpointsTests`, `ProjectStatusDashboardDataTests`,
> `ExecutivePortfolioEndpointsTests`, `SummarizeDataQualityTests`, …) already exercise responses
> end-to-end. Layer 3 (LLM semantic drift over `CLAUDE.md` invariants) is not built until Layer 1
> proves insufficient in practice. The nswag TypeScript client generator (`ClientApp/nswag.json`,
> reads from `https://localhost:7443/openapi/v1.json` → `src/web-api-client.ts`) consumes the same
> live document — so a client regeneration after an approved baseline update is the follow-on step.

> **Client framework:** template param `--client-framework` (`-cf`) = `react` (default) or
> `none` (API only). Driven by `ClientFramework` symbol → computed `UseReact` / `UseApiOnly`,
