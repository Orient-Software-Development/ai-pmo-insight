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

**[2026-07-22] Layer-3 CI automation expanded from one doc to three.** Added
`doc-drift-analysis-pipeline` and `doc-drift-dashboard-output-formats` alongside the
existing `doc-drift-authentication` job, same pattern (path-filtered, advisory, skips
cleanly without the secret). Rationale for these two specifically: `analysis-pipeline.md`
had already drifted twice from real code changes (stale cost/routing config, an
already-shipped feature documented as "not yet implemented") when checked manually via
`/check-doc-drift`; `dashboard-output-formats.md` traces every number a stakeholder sees on
the dashboards, so a silent drift there is a "the tool told the client a wrong number" risk.
`database.md` and `auth-gap.md` stay on-demand only — no evidence yet that they drift often
enough to justify a standing CI cost.

**[2026-07-23] `CLAUDE.md` added as a fourth Layer-3 CI job — stays advisory, not blocking.**
`doc-drift-claude-md` added on user request, same non-blocking pattern as the other three.
Explicitly considered and rejected: making any of the four jobs blocking (would require
parsing a structured verdict instead of free-text and `exit 1` on drift — reintroduces the
exact non-determinism/false-positive risk the 2026-07-21 decision chose advisory to avoid) and
a deterministic "PR touched the code but not the doc" presence gate (no LLM, no flakiness, but
only proves the doc was touched, not that it's correct — considered lower value than the
LLM check's actual content comparison, so not built either). Revisit if advisory-only proves
insufficient in practice. Code sent to the model is a hand-picked ~41-file set matching
CLAUDE.md §3's control points, not the whole `source/` tree — CLAUDE.md's scope is close to
the entire solution, so a naive wildcard would blow up prompt cost for no real gain.

**[2026-07-23] All four Layer-3 CI jobs moved from automatic push-trigger to a PR-comment
chatops trigger (`/check-doc-drift [doc]`).** Moved out of `ci.yml` into its own
`doc-drift-on-demand.yml`, triggered by `issue_comment` instead of `pull_request`. Reason:
the push-triggered version's path filter diffed `origin/<base>...HEAD` (cumulative since
branch point), not the incremental push — so once a PR touched a mapped path, every
subsequent push re-ran the LLM call again with no caching, and a multi-commit PR touching
several docs' code areas could rack up many redundant calls. On-demand ties cost to how many
times a human actually asks. Gated on `author_association` (OWNER/MEMBER/COLLABORATOR) to
stop a random commenter from spending API budget. Result posts as both a job summary and a
direct PR reply comment (more visible than before, since this is now something a human
explicitly requested rather than a silent background check). Comment body is passed through
an env var, never spliced directly into a `run:` script via `${{ }}` — required to avoid a
GitHub Actions script-injection vector on untrusted comment text.

**[2026-07-23] Added `workflow_dispatch` as a second manual trigger alongside the PR comment,
plus `issues: write` permission.** User wanted the workflow selectable from the Actions tab's
"Run workflow" button, not only via PR comment. Added typed inputs (`pr_number`, `doc` as a
fixed dropdown so it can't be invalid) — no extra permission gate needed on this path since
GitHub already requires write access to see/trigger `workflow_dispatch`. While wiring this up,
caught that `pull-requests: write` alone isn't enough for the reaction + reply-comment calls
(both hit the `issues/comments/...` API surface under the hood, since a PR conversation
comment is an issue comment) — added `issues: write` too, otherwise those steps 403 even
though the doc-drift jobs themselves would still run. Also: because neither `issue_comment`
nor `workflow_dispatch` carries a PR ref, GitHub always reads this workflow file from the
**default branch**, never a PR branch — a PR that adds/edits this file must be merged to
`main` before either trigger works, including for PRs opened after the merge.

**[2026-07-21] Drift classification (breaking vs additive) via `oasdiff` is advisory, not
blocking.** Layer 1 remains the strict "baseline must be current" merge gate — any change to
the shape requires a same-PR baseline update. The `openapi-classify` CI job runs on PRs only,
posts to the job summary, and never fails the build. Its purpose is to tell the reviewer
**what kind** of change landed (would a consumer need to update?), not to decide for them
whether it's OK. Local equivalent command is in [CLAUDE.md](CLAUDE.md) §2.
