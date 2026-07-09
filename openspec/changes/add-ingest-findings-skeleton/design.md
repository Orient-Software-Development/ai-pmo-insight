## Context

This is the walking skeleton for the AI PMO Insight POC (`docs/prds/poc-ai-pmo-insight.md`). It sits on the existing Clean-Architecture .NET + React template, whose only reference vertical slice today is `Widgets` (`Domain → Application slice → Infrastructure repo → Api endpoint`, snake_case EF configs, cookie-JWT auth). New slices mirror that pattern exactly.

The POC's load-bearing bet is that the durable domain is **findings + citations**, not client project data. The client's real data lives in **Orbit** (GraphQL API + XSD/XML import + CSV/Excel/XML export), but this slice is built on **dummy fixtures shaped like an Orbit export** so it never waits on client data. It attacks the *architecture* risk (does upload → analyze → cited finding → read hang together?), not the *ingest* risk (can we parse Orbit's real files?) — the parser is deliberately stubbed.

## Goals / Non-Goals

**Goals:**
- Prove the end-to-end wiring through every layer: React → API → Application → Domain → EF/Postgres → back.
- Establish the **finding → citation** link as real from commit one (source `uploadId` + locator).
- Establish the **upload ≠ analyze** seam (two endpoints), run synchronously — the async shape without queue infrastructure.
- Ship a dummy Orbit-shaped fixture (structured rows + one meeting-minutes blob) that doubles as a golden-file test input.

**Non-Goals:**
- Real parsing of Orbit exports (stubbed).
- Any LLM / agent pipeline, health scoring, Level-1/Level-3 dashboards.
- A `Project` entity (findings group by opaque `projectKey` string).
- Separating raw uploads from findings into distinct stores, TTL, retention (deferred — they may share a store).
- New PMO roles (`pmoAdmin`, `executive`, …) — reuse the template's authenticated-caller auth.
- The Orbit GraphQL pull (plan item #13).

## Decisions

**1. Two vertical slices / capabilities: `orbit-ingest` and `project-findings`.**
Ingest (store the upload) is a distinct concern from findings (analyze → persist → read). Mirrors the modular boundaries the PRD draws. Alternative — one combined slice — was rejected: it would couple the upload store to the findings store, which is exactly the separation we want to keep *possible* later.

**2. Domain: `Finding` aggregate + `Citation` value object; no `Project` entity.**
`Finding { Id, ProjectKey (string), Summary, Citation, CreatedAt }`; `Citation { UploadId, Locator }`. Grouping by an opaque `projectKey` string is the thinnest thing that makes `GET /api/projects/{projectKey}` work. When real Orbit data arrives, `projectKey` = Orbit project id — a value change, not a schema change. Alternative — a real `Project` aggregate — deferred until tenanted metadata (name, type) or duplicate-merge (user story #2) is actually needed; Orbit's single-source project identity makes that need unlikely for the POC.

**3. Citation is mandatory and enforced in the domain.**
`Finding.Create(...)` requires a non-null `Citation` with a non-empty `UploadId` and `Locator`. A finding cannot exist without provenance. This is the trust story (PRD success criterion #5) and the hardest thing to retrofit, so it is the one invariant the skeleton refuses to stub.

**4. Storage: raw uploads and findings may share `AppDbContext`; no landing-zone split yet.**
Two EF entities (`Upload`, `Finding`) with snake_case configs and one migration; auto-migrated in Development via `DbInitializer`. Raw bytes stored in a column for the POC (fixtures are tiny). The finding→citation link means separating stores later is a data move, not a rewrite. Alternative — blob storage / separate landing DB — deferred (PRD open question).

**5. Analyze is a synchronous, separately-triggered step.**
`POST /api/ingest/upload` returns fast with an `uploadId`; `POST /api/analyze/{uploadId}` reads it and emits the finding. Run synchronously (no queue). This preserves the seam that real (slow, LLM-backed) analysis will need, at near-zero cost. Alternative — analyze inline in upload — rejected: hides a seam we will pay to retrofit.

**6. Minimal read-only React view.**
One page calling `GET /api/projects/{projectKey}`, listing findings with their citation. Keeps the skeleton genuinely end-to-end (UI included) without building real dashboards.

## Risks / Trade-offs

- **Storing raw bytes in Postgres is not the target architecture** → Acceptable: fixtures are tiny and the split is an explicit PRD open question; the citation link keeps the migration path cheap.
- **`projectKey` string could feel too loose if a `Project` entity is later needed** → Mitigation: the key is isolated behind the finding aggregate and the read query; promoting to an entity is an additive migration, not a rewrite.
- **Synchronous analyze hides eventual latency/failure modes** → Mitigation: the *seam* (separate endpoint) is what matters now; making it async later changes wiring behind the endpoint, not the contract.
- **A wrongly-shaped dummy fixture gives false confidence** → Mitigation: shape fixtures against Orbit's export columns/XSD; treat the fixture as the spec for the later real parser.

## Open Questions

- Exact shape of the Orbit-export fixture columns — best confirmed against one real (anonymized) Orbit export, but not blocking (we model a plausible shape now).
- Whether the minimal React view is worth wiring in this change or deferred to a follow-up — included by default to keep the skeleton end-to-end.
