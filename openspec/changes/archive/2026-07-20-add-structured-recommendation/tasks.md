> **TDD** — failing test first (red), minimal code (green), refactor; keep the full backend suite green.
> Baseline: 139 Application + 126 Api = 265. Ticket #48; deps #46 (metric field) + #47 (Decisions) both
> landed. Small change — stamp the existing `Recommendation` onto `MetricDetail`; keep the summary.

## 1. Slice A — Structured recommendation on the narrative finding (TDD)

- [x] 1.1 (red) Added 2 `NarrativeAgentTests` — LLM path carries owner/deadline/action/rationale in
      `MetricDetail` + summary; template path also carries the detail. Failed (detail null).
- [x] 1.2 (green) `NarrativeSkill.ToFinding` populates `MetricDetail` with the `Recommendation` fields
      (both paths, via the shared method); summary kept; `MetricValue`/`MetricUnit` null.
- [x] 1.3 Full backend suite green: 141 Application + 126 Api = 267.

## 2. Verify + document

- [x] 2.1 Full suite green (141 Application + 126 Api = 267); `openspec validate --strict` passes; build
      clean (suite builds).
- [x] 2.2 Ticked #48; updated `docs/l1-`/`l2-` registers (#7 — recommendation structured; view rendering
      still a presentation follow-on) and the recommendation rows + detail keys in
      `docs/dashboard-output-formats.md`.
- [x] 2.3 Boundary held: only `NarrativeSkill.ToFinding` changed. No prompt, LLM-contract, template
      classifier, schema, or view change.
