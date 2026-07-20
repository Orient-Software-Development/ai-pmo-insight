# AI PMO Insight

A PMO **trust-layer** service that turns fragmented project data (Orbit exports, meeting minutes)
into **cited findings** for portfolio-level status reporting. Built as a proof of concept for
NextWave — the [PRD](docs/prds/poc-ai-pmo-insight.md) is the source of truth; the
[roadmap](docs/roadmap.md) tracks what's shipped vs. planned.

The intelligence runs as a **nine-agent analysis pipeline** driven by one orchestrator: five
deterministic agents (parse, data quality, status, financial, resource) and four LLM-backed
agents (risk & issue extraction from minutes, narrative synthesis, adversarial challenge,
stakeholder review). Every finding carries a citation to its source record — findings without
citations are rejected before persist.

## What ships today

- **Upload → analyze → read flow** — `POST /api/ingest/upload` stores raw bytes, `POST /api/analyze/{uploadId}` runs the pipeline, `GET /api/projects/{projectKey}` returns findings grouped by project.
- **Nine-agent pipeline** — `AnalysisOrchestrator` sequences `#1 Data Collector → #2 Data Quality → parallel(#3 Status, #4 Risk & Issue, #5 Financial, #6 Resource) → merge → #7 Narrative → #8 Challenge → #9 Review → persist`. See [`source/AiPMOInsight.Application/Features/Analysis/`](source/AiPMOInsight.Application/Features/Analysis).
- **Per-agent LLM routing** — the four LLM-backed agents each pick their own provider/model via `Llm.Default` + `Llm.Agents.<SkillName>` config. Selectors: `fake` (fixture responses, no API key — demo/tests), `anthropic` (Messages API), and `openai` (Chat Completions) — both working vendor adapters requesting structured JSON output. Provider swap is a config change; no agent, prompt, or orchestrator code moves.
- **Citations + provenance** — every finding records producing agent, confidence, kind, prompt-content-hash version, and analysis run id. Re-analysis appends under a new run id; prior findings stay.
- **Level-2 read view** — minimal React component (`components/ProjectFindings.jsx`) reads a project's findings, grouped by kind.
- **Dummy Orbit-shaped fixtures** in [`docs/samples/`](docs/samples) — one file per input category (identifier, timeline, budget, resources, risks, issues, decisions, minutes) so the whole flow demos with no live Orbit connection.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/) (pinned by `global.json`, prerelease allowed)
- [Docker](https://www.docker.com/) (for local PostgreSQL via `docker-compose.yml`)
- [Node.js](https://nodejs.org/) (for the React client)

## Getting started

```bash
dotnet restore && dotnet tool restore

docker compose up -d                              # local Postgres (empty; EF creates the schema)
dotnet run --project source/AiPMOInsight.Api      # in Development, auto-migrates + seeds
```

The API serves `/health`, `/api/ingest/upload`, `/api/analyze/{uploadId}`,
`/api/projects/{projectKey}`, `/api/auth/*`, `/api/profile/me`, and `/openapi/v1.json`.

### Build & test

```bash
dotnet build -c Release
dotnet test  -c Release
```

## Run (backend + frontend)

### Dev mode — hot reload, two terminals

Vite serves the SPA and proxies API calls to the backend, so the browser sees a single origin.

```bash
# once: trust the local HTTPS cert
dotnet dev-certs https --trust
```

**Terminal 1 — backend** (https://localhost:7443, http://localhost:5080):
```bash
dotnet run --project source/AiPMOInsight.Api --launch-profile https
```

**Terminal 2 — frontend** (http://localhost:5173):
```bash
cd source/AiPMOInsight.Api/ClientApp
npm install        # first time only
npm run dev
```

Open **http://localhost:5173** → login, upload a fixture from `docs/samples/`, analyze, and
read the findings for the resulting project. Vite proxies `/api`, `/openapi`, `/health` to the
backend (see `vite.config.ts`; override the target with `VITE_API_PROXY`).

### Single process — prod-like (one port, no Vite)

`dotnet publish` builds the React app and the API serves it from `wwwroot`:

```bash
dotnet publish source/AiPMOInsight.Api -c Release -o ./publish   # runs npm build → wwwroot
dotnet ./publish/AiPMOInsight.Api.dll                            # SPA + API on one port
```

## Analysis pipeline

Nine agents, one orchestrator. Deterministic agents are pure C# (no LLM, fully reproducible over
the same inputs). LLM-backed agents reach the model through a single `ILlmClient` port; the
routing adapter dispatches per-agent by `LlmRequest.SkillName`.

| # | Agent | Type | Job |
|---|---|---|---|
| 1 | Data Collector | Deterministic | Parse upload into typed records with source locators. |
| 2 | Data Quality | Deterministic | Missing / stale / inconsistent detection + a confidence signal for downstream. |
| 3 | Status | Deterministic | Milestone math: variance, delay, upcoming, dependency risk. |
| 4 | Risk & Issue | Hybrid | Filter RAID records deterministically; **only when minutes present**, extract additional risks via LLM. |
| 5 | Financial | Deterministic | Budget/forecast variance, burn rate, budget-vs-progress. |
| 6 | Resource | Deterministic | Over-allocation, capacity pressure, missing PM, concentration × absence. |
| 7 | Narrative | Hybrid | Template-first prose status + recommendation; LLM fallback for complex multi-signal cases. |
| 8 | Challenge | LLM hybrid | Adversarial critique of #7 + findings (weak claims, unsupported numbers, missing caveats). |
| 9 | Review | LLM hybrid | Anticipated stakeholder questions grouped by audience. Not a keep/drop gate. |

Per-agent LLM config (env-var overrides use `Llm__Default__ApiKey` /
`Llm__Agents__<SkillName>__ApiKey`; keys are **never** committed):

```json
"Llm": {
  "Default": { "Provider": "fake", "ModelId": "", "PerAnalysisTokenBudget": 100000 },
  "Agents": {
    "Narrative": { "Provider": "anthropic", "ModelId": "claude-sonnet-5" },
    "Challenge": { "Provider": "openai",    "ModelId": "gpt-4o" }
  }
}
```

Missing agent block → falls back to `Default`. Unknown provider → **fails at startup**, never
mid-request.

## Project layout

| Path | Purpose |
|------|---------|
| `source/AiPMOInsight.Domain/` | Domain aggregates (`Finding`, `Citation`, `AnalysisRun`, …) |
| `source/AiPMOInsight.Application/` | CQRS handlers, `AnalysisOrchestrator`, agent skills, prompt registry, ports (`ILlmClient`, `IUploadParser`, …) |
| `source/AiPMOInsight.Infrastructure/` | Adapters (`RoutingLlmClient`, `FakeLlmClient`, `UploadParser`, EF repositories), migrations |
| `source/AiPMOInsight.Api/` | Minimal-API endpoints (`AuthEndpoints`, `IngestEndpoints`, `FindingsEndpoints`, `ProfileEndpoints`), OpenTelemetry, health checks |
| `source/AiPMOInsight.Api/ClientApp/` | React + Vite SPA |
| `tests/**/*.Tests/` | Test projects (xUnit + AwesomeAssertions) |
| `openspec/` | Change proposals + live specs — see [`openspec/specs/`](openspec/specs) for source-of-truth capabilities |
| `docs/` | PRD (`prds/`), roadmap, ADRs, plans, auth + database docs, sample fixtures |
| `docker-compose.yml` | Local dev PostgreSQL |
| `Dockerfile` | Multi-stage container build |
| `.github/workflows/` | CI (build/test/coverage), PR title lint, deploy pipeline |
| `.claude/skills/` | Reusable Claude Code workflows (OpenSpec propose/apply/archive, TDD, code-review, …) |
| `CLAUDE.md` | Detailed architecture notes and conventions for Claude Code |

## Database migrations

Schema is owned by EF Core migrations
(`source/AiPMOInsight.Infrastructure/Migrations`). Widgets, Findings, Uploads, and the ASP.NET
Core Identity tables all live here.

```bash
# add a migration after changing entities/DbContext
dotnet ef migrations add Describe_change \
  --project source/AiPMOInsight.Infrastructure --startup-project source/AiPMOInsight.Api

# apply pending migrations manually (Development does this automatically on startup)
export AppDb__ConnectionString="Host=localhost;Port=5432;Database=aipmoinsight;Username=aipmoinsight;Password=aipmoinsight"
dotnet ef database update \
  --project source/AiPMOInsight.Infrastructure --startup-project source/AiPMOInsight.Api
```

In production, migrations run as a deliberate deploy step (see `.github/workflows/deploy.yaml`),
never on startup.

## Authentication

JWT access (15 min) and refresh (7-day) tokens, both transported as `httpOnly`, `Secure`,
`SameSite=Strict` cookies — never exposed to JavaScript. See
[`docs/authentication.md`](docs/authentication.md) for the full design.

```bash
curl -X POST "http://localhost:5080/api/auth/register" -H "Content-Type: application/json" \
  -d '{"email":"me@x.com","password":"Passw0rd!$"}'

curl -c cookies.txt -X POST "http://localhost:5080/api/auth/login" -H "Content-Type: application/json" \
  -d '{"email":"me@x.com","password":"Passw0rd!$"}'

# Upload a fixture and run the full pipeline over it
curl -b cookies.txt -F "file=@docs/samples/timeline.xlsx" http://localhost:5080/api/ingest/upload
curl -b cookies.txt -X POST http://localhost:5080/api/analyze/<uploadId>
curl -b cookies.txt http://localhost:5080/api/projects/<projectKey>

curl -b cookies.txt http://localhost:5080/api/profile/me       # current user + roles
curl -b cookies.txt -c cookies.txt -X POST "http://localhost:5080/api/auth/refresh"
```

## Observability

OpenTelemetry (traces + metrics) is wired up in `Program.cs` and exported over OTLP — point
`OTEL_EXPORTER_OTLP_*` env vars at a collector per environment. Health checks:

- `/health/live` — liveness, no dependencies
- `/health/ready` — readiness, checks the database
- `/health` — liveness alias

## CI/CD

- **`ci.yaml`** — on push/PR to `main`: GitVersion, restore, build, test + coverage.
- **`pr-lint.yml`** — validates PR titles follow Conventional Commits.
- **`deploy.yaml`** — manual (`workflow_dispatch`) pipeline: build & push a GitVersion-tagged
  image to GHCR, run `dotnet ef database update`, then deploy (placeholder — fill in your target).

## More documentation

- [`docs/prds/poc-ai-pmo-insight.md`](docs/prds/poc-ai-pmo-insight.md) — POC PRD (source of truth)
- [`docs/roadmap.md`](docs/roadmap.md) — phase status (Phase 3 in flight; per-agent LLM routing = 3.9/3.10/3.11)
- [`docs/kickoff-questions.md`](docs/kickoff-questions.md) — POC → production: data/decisions still
  open, deferred to kick-off
- [`docs/authentication.md`](docs/authentication.md) — auth design
- [`docs/auth-gap.md`](docs/auth-gap.md) — auth implementation vs. the design spec
- [`docs/database.md`](docs/database.md) — persistence design
- [`openspec/specs/`](openspec/specs) — live capability specs (analysis pipeline, project findings, orbit ingest, LLM routing when Phase 3.9 archives)
- [`openspec/changes/`](openspec/changes) — in-flight proposals
- [`CLAUDE.md`](CLAUDE.md) — architecture notes and conventions

