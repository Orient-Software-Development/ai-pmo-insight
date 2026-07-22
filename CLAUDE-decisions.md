# CLAUDE-decisions.md

Load-bearing architectural choices for this repo — recorded once; contradict only with explicit
user consent. Dates are approximate (month or phase) where the exact commit is older than the
current git-log window. Split out from [CLAUDE.md](CLAUDE.md) §7 so the main file stays under
the living-spec length ideal; load this file when a change might contradict a past decision.

---

**[pre-2026-07] JWT in httpOnly cookies, not Authorization header.** XSS-safe; both tokens
`SameSite=Strict` (no separate CSRF token). Refresh token has a **fixed 7-day TTL, not sliding
on rotation** — an inactive session must eventually die.

**[pre-2026-07] Shared workspace, no per-user scoping.** Any authenticated caller sees every
finding / upload / project. Per-user scoping is a new spec, not a cleanup.

**[pre-2026-07] Provider (Anthropic / OpenAI / fake) selectable per-agent via config alone.**
`RoutingLlmClient` + `ILlmClientFactory` — no agent/prompt/orchestrator code change to swap.

**[pre-2026-07] Migrations run in Dev auto, in Prod deliberately.**
`DbInitializer.MigrateAndSeedAsync` is `IsDevelopment()`-guarded; Prod applies as a deploy step.
Never change without a rollback plan.

**[pre-2026-07] Integration tests use EF in-memory, not mocked repositories.**
`TestWebAppFactory` swaps in the in-memory provider; endpoint tests exercise real repository
code paths. Mocks belong in handler unit tests, not endpoint tests.

**[pre-2026-07] TDD for OpenSpec-tracked changes.** Red-green-refactor; suite green before
checking off a task.

**[2026-07 Phase 4] Scoring is a re-runnable query, not a pipeline step.**
`HealthScoringService` is pure and runs on demand — no persisted score column. Config changes
take effect without re-running paid LLM analysis.

**[2026-07 Phase 4] All health-scoring / DQ numbers ship as EXAMPLE placeholders.** Startup
warning is the guardrail. No score / threshold / override is client-agreed until PMO kickoff.

**[2026-07 Phase 5] No first-class `Project` entity.** Keys are opaque strings from
`IFindingRepository.DistinctProjectKeysAsync`. Projects have no lifecycle of their own.

**[2026-07-21] Response types declared on every endpoint via `.Produces<T>()`.** Otherwise
minimal-API handlers return opaque `IResult`, the OpenAPI doc omits response schemas, and the
Layer-1 sensor misses field-level drift. Non-negotiable.

**[2026-07-21] Layer-2 runtime contract test is GET-only.** POST bodies would duplicate the
hand-written `AuthEndpointsTests` / `*EndpointsTests`. Revisit only if a POST-side runtime bug
slips through.

**[2026-07-21] Layer-3 (LLM semantic drift) — advisory only, one CI-automated doc.**
Non-deterministic, ~$0.10-0.20 per invocation, no established best practice — reasons to
keep it out of blocking gates, not reasons to skip it entirely. Two forms exist:
(a) `/check-doc-drift` slash command for any prose doc, run on demand; (b) automated CI job
`doc-drift-authentication` in `.github/workflows/ci.yml`, gated on PRs that actually touch
auth code or the doc itself. Only `docs/authentication.md` is on CI — highest cost/benefit
(security-critical, code changes independently of the doc, reviewers skim it). Other docs
stay on-demand until the pattern proves value for them. Requires the `ANTHROPIC_API_KEY`
repo secret; the CI job skips cleanly if the secret is unset.

**[2026-07-21] Drift classification (breaking vs additive) via `oasdiff` is advisory, not
blocking.** Layer 1 remains the strict "baseline must be current" merge gate — any change to
the shape requires a same-PR baseline update. The `openapi-classify` CI job runs on PRs only,
posts to the job summary, and never fails the build. Its purpose is to tell the reviewer
**what kind** of change landed (would a consumer need to update?), not to decide for them
whether it's OK. Local equivalent command is in [CLAUDE.md](CLAUDE.md) §2.
