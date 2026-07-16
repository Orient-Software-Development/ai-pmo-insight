> **TDD** — write the failing test first (red), minimal code to pass (green), refactor; keep the full
> backend suite green before checking off a task. Scoring numbers stay `EXAMPLE` placeholders
> (`IsPlaceholder = true`). Slices are ordered so each can land independently and the suite stays green
> between them. Source of scope: `docs/l1-executive-portfolio-followups.md`.

## 1. Baseline (no code)

- [ ] 1.1 Run the full backend suite and record the green baseline (Application + Api counts).
- [ ] 1.2 Note the existing tests most likely to shift: `HealthScoringServiceTests`, `ScorePortfolioTests`,
      `ExecutivePortfolioEndpointsTests`, and any Status/Resource agent tests — expected assertions will be
      re-derived deliberately (not force-passed) when the fixes land.

## 2. Slice A — Resource "no PM" false-finding fix (TDD)

- [ ] 2.1 (red) Add a Resource-agent test: a project whose only PM assignment has role "Project Management"
      MUST NOT produce a "no project manager" finding. Watch it fail against the current
      `Role.Contains("Manager")`.
- [ ] 2.2 (green) Fix the role match in `ResourceSkill` (recognise "Project Management" / "Project Manager"
      / "PM"); a genuinely PM-less project still flags. Refactor the check into a small helper.
- [ ] 2.3 Re-run the suite; green.

## 3. Slice B — Status "missed milestone → green" fix (TDD)

- [ ] 3.1 (red) Add a Status-agent test: a milestone with `Status == "Missed"` MUST emit a Red Schedule
      finding, not a Green "due soon", even when its due date is within the upcoming window.
- [ ] 3.2 (green) In `StatusSkill`, read `MilestoneRecord.Status`; map Missed → Red, At Risk → Amber
      (overriding the date-window Green); milestones with no adverse status keep the existing bands.
- [ ] 3.3 Add/confirm a `HealthScoringService` test that the `critical-milestone-missed` override now fires
      when a Schedule finding is Red (it previously could not for the missed-critical case). Re-derive any
      shifted bucket expectations deliberately.
- [ ] 3.4 Re-run the suite; green.

## 4. Slice C — `Finding` structured metric (domain + migration, TDD)

- [ ] 4.1 (red) Add a domain test: `Finding.Create` accepts an optional metric (value + unit, and/or a
      small detail map) and rejects nothing it accepted before; the `Kind == Analysis ⇒ Area + Severity`
      and mandatory-citation invariants still hold with and without a metric.
- [ ] 4.2 (green) Add the nullable metric fields to the `Finding` aggregate + `Finding.Create`; keep them
      optional and additive.
- [ ] 4.3 Map the new fields in EF (`WidgetConfiguration`-style, snake_case) and add a migration
      (`dotnet ef migrations add AddFindingMetric` — Infrastructure project, Api startup). Commit the
      generated files. Confirm Development auto-migrate still boots.
- [ ] 4.4 (red→green) Persistence round-trip test: a finding with a metric saves and reads back with the
      metric intact; a finding without one round-trips with null.
- [ ] 4.5 Re-run the suite; green.

## 5. Slice D — Financial exposure amount + Narrative structured recommendation (TDD)

- [ ] 5.1 (red) Financial-agent test: the total-exposure finding carries the exposure **amount + currency**
      on the metric (not only in the summary text).
- [ ] 5.2 (green) Stamp the exposure amount + currency onto the finding metric in `FinancialSkill`; keep the
      summary text.
- [ ] 5.3 (red) Narrative-agent test: the recommendation's owner / deadline / action are carried as
      structured detail on the finding metric, in addition to the existing summary.
- [ ] 5.4 (green) Stamp the `Recommendation` fields onto the metric in `NarrativeSkill` (both template and
      LLM paths); keep the summary for back-compat.
- [ ] 5.5 Re-run the suite; green.

## 6. Slice E — Key-person concentration (Resource agent, TDD)

- [ ] 6.1 (red) Resource-agent test: given assignments where a person is on 5 distinct projects, a Red
      key-person concentration finding is emitted (cited); a person on 2 projects is not flagged. Bands
      5+ Red / 3–4 Amber / <3 none.
- [ ] 6.2 (green) In `ResourceSkill`, compute concentration from the **unfiltered** `slice.Data.Assignments`
      (count distinct project keys per person), emit the banded finding attached to the current project's
      assignment for that person. (× absence is out of scope — do not fabricate.)
- [ ] 6.3 Re-run the suite; green.

## 7. Slice F — Decisions area + parsing + agent + scoring (TDD)

- [ ] 7.1 (green, enum) Add `HealthArea.Decision` (persisted as string; no migration needed for the enum).
- [ ] 7.2 (red) Parser test: a `Decisions` sheet parses into `DecisionRecord`s (status / needed-by / owner /
      consequence), each with a `sheet!row` source. Confirm the fixture (`orbit-sample.xlsx`) carries a
      `Decisions` tab with matching headers; regenerate the tab if the headers don't match the reader.
- [ ] 7.3 (green) Add `DecisionRecord` to the typed model + a `Decisions` reader in `ExcelProjectParser`;
      wire it into `CollectedData`.
- [ ] 7.4 (red) `DecisionSkill` test: an overdue decision (past needed-by, not "Approved") emits a cited
      `Area == Decision` finding at the overdue severity; an "Approved" decision emits nothing; a due-soon
      decision emits a due-soon finding.
- [ ] 7.5 (green) Implement the deterministic `DecisionSkill`; register it in the orchestrator's parallel
      analysis stage.
- [ ] 7.6 (green, config) Add a `Decision` weight to the placeholder `Weights` and a
      `key-decision-overdue` override (`{ Area: Decision, WhenSeverityAtLeast, Floor: Amber }`) to
      `appsettings.json` `HealthScoring`; keep `IsPlaceholder: true`. Add a scoring test that the override
      floors to Amber and is audited.
- [ ] 7.7 Re-run the suite; green (re-derive any shifted portfolio/scoring expectations deliberately).

## 8. Slice G — L1 roll-up + view (TDD)

- [ ] 8.1 (red) `ScorePortfolioTests`: the result carries total financial exposure (amount + currency),
      a decision-backlog count, a key-person concentration list, and a customer-exposure grouping; an empty
      store yields zeroed/empty values (not 404).
- [ ] 8.2 (green) Extend the `ScorePortfolio` result + handler to compute those from the scored projects'
      findings (exposure from the Financial metric; decisions from Decision findings; concentration from
      Resource findings; customer-exposure by grouping Red/Amber projects on `ProjectRecord.Customer`).
- [ ] 8.3 (red→green) `ExecutivePortfolioEndpointsTests`: `GET /api/portfolio` returns the new fields;
      shape is additive; auth + empty-store behaviour unchanged.
- [ ] 8.4 Update `ExecutivePortfolio.jsx` to render the exposure / decision-backlog / key-person /
      customer-exposure panels from the response, replacing those dashed placeholders; keep the true
      commercial-risk panel flagged (labelled, not fabricated).
- [ ] 8.5 Re-run the suite; green.

## 9. Verify + document

- [ ] 9.1 Full backend suite green; `openspec validate add-l1-portfolio-signals --strict` passes;
      `dotnet build` clean; client `vite build` clean.
- [ ] 9.2 `/verify` against the running stack (Docker + API + Vite): upload `orbit-sample.xlsx`, analyze,
      confirm ORB-1002 no longer shows "no PM" and the missed dress-rehearsal milestone is no longer green;
      `GET /api/portfolio` returns exposure € / decision backlog / key-person / customer-exposure; the L1
      view renders them.
- [ ] 9.3 Update `docs/l1-executive-portfolio-followups.md` (items #3/#4/#5/#6-proxy/#7 → done, with the
      × absence and true-commercial-risk follow-ons retained) and `docs/dashboard-output-formats.md`
      (flip the relevant states, note the Decision area now scored).
- [ ] 9.4 Confirm the presentation-only boundary held where claimed: no fabricated commercial-risk figure;
      × absence not invented; L2/L3 view work not started (only the shared agents changed).
