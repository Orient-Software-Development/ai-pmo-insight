## Why

The AI PMO Insight POC (see `docs/prds/poc-ai-pmo-insight.md`) rests on one load-bearing architectural bet: the durable domain is **findings + citations**, produced from ingested project data, read back per project. Before adding any intelligence (LLM, the 9-agent pipeline, scoring), we need a walking skeleton that proves this wiring end-to-end. The client's data lives in **Orbit**, but the POC is deliberately built on **dummy fixtures shaped like an Orbit export** so the build never waits on client data. This slice attacks the *architecture* risk, not the *ingest* risk — the parser is stubbed on purpose.

## What Changes

- Add an **upload endpoint** that accepts a dummy, Orbit-shaped fixture file, stores it, and returns an upload reference. The file content is treated as opaque bytes — **no real parsing** in this slice.
- Add a **stub analysis step** (a separate seam from upload: `POST /analyze/{uploadId}`, run synchronously) that reads a stored upload and emits **one hard-coded finding** — no LLM, no logic.
- **Every finding carries a citation** back to its source (upload reference + a locator). This is the one part that is real from commit one; it is the trust story and the hardest thing to retrofit.
- Persist findings, and add a **Level-2 read endpoint** (`GET /api/projects/{projectKey}`) returning the findings for a given project key.
- Findings are grouped by an opaque **`projectKey` string** (no `Project` entity yet); when real Orbit data is fed later, that key becomes the Orbit project id — a value change, not a structural one.
- **Storage split deferred**: raw uploads and findings may share a store for the POC. The finding→citation link is preserved regardless, so separating the stores later is a data move, not a rewrite.
- Add a **minimal read-only React view** that calls the Level-2 endpoint, so the skeleton walks through every layer (UI → API → domain → DB).
- Ship a **dummy fixture** in the repo (Orbit-shaped structured rows + one unstructured meeting-minutes blob) that doubles as a golden-file test input.

Out of scope for this change: real parsers, LLM / agent pipeline, health scoring, Level-1 and Level-3 dashboards, new PMO roles, raw/findings store separation, and the Orbit GraphQL pull (plan item #13). New slices mirror the existing Widgets vertical-slice pattern; auth reuses the template's existing `.RequireAuthorization()` (authenticated caller only).

## Capabilities

### New Capabilities
- `orbit-ingest`: Accept an uploaded Orbit-shaped fixture file, store its raw bytes, and return an upload reference. Parser is stubbed — content is opaque.
- `project-findings`: Trigger a stub analysis over a stored upload that emits findings, each carrying a citation to its source; persist them grouped by `projectKey`; and expose a Level-2 read endpoint returning a project's findings.

### Modified Capabilities
<!-- None — openspec/specs/ is empty; this is the first capability set. -->

## Impact

- **New vertical slices** (mirroring `Widgets`): `Domain` finding aggregate + citation value object; `Application` slices for upload, analyze, and read (command/query + handler); `Infrastructure` repositories (ports in Application) + EF configuration; `Api` endpoints.
- **Persistence**: new EF entities + snake_case configuration + a new migration; auto-migrated in Development via `DbInitializer`.
- **API surface**: `POST /api/ingest/upload`, `POST /api/analyze/{uploadId}`, `GET /api/projects/{projectKey}`, all requiring an authenticated caller.
- **Client**: one minimal read-only React view for the Level-2 endpoint.
- **Dependencies**: none new expected beyond what the template already provides (EF Core, ASP.NET Core, React/Vite).
- **Tests**: golden-file / integration tests through `TestWebAppFactory` asserting a fixture upload flows to cited findings on the read endpoint.
