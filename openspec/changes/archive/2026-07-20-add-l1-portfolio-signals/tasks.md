> **TDD** — failing test first (red), minimal code (green), refactor; keep the full backend suite green
> before checking off a task. Scoring numbers stay `EXAMPLE` placeholders. This change is **L1-only**; the
> shared infrastructure it consumes lives in tickets **#46** (Finding metric), **#45/#47** (Decisions),
> **#48** (recommendation). Slices 2–4 are independent (no shared dep) and land first; slice 5 (roll-up) is
> **gated** on #46 (exposure) and #47 (decision-backlog). Source: `docs/l1-executive-portfolio-followups.md`.

## 1. Baseline (no code)

- [x] 1.1 Baseline green: **117 Application + 122 Api = 239 passed**, 0 failed.
- [x] 1.2 Watch-list noted: `HealthScoringServiceTests`, `ScorePortfolioTests`,
      `ExecutivePortfolioEndpointsTests`, Status/Resource agent tests — re-derive shifted assertions
      deliberately when the fixes land.

## 2. Slice A — Resource "no PM" false-finding fix (TDD, independent)

- [x] 2.1 (red) Added `A_project_manager_role_is_recognised` theory (Project Management / Project Manager /
      PM). Failed as expected: "Project Management" + "PM" tripped the old `Contains("Manager")` (2 red, 1 pass).
- [x] 2.2 (green) Added `IsProjectManagerRole` helper in `ResourceSkill` (matches Manager / Management / PM);
      a genuinely PM-less project still flags (`Flags_a_missing_key_role` stays green).
- [x] 2.3 Full Application suite green: 120 passed (was 117 + 3 new theory cases).

## 3. Slice B — Status "missed milestone → green" fix (TDD, independent)

- [x] 3.1 (red) Added `A_missed_milestone_is_red_even_when_its_due_date_is_upcoming` +
      `An_at_risk_milestone_is_amber` + a green regression guard. Missed/at-risk failed as expected (2 red).
- [x] 3.2 (green) `StatusSkill` reads `MilestoneRecord.Status` via `AdverseStatusSeverity` (Missed → Red,
      At Risk → Amber) and `Worst(band, statusSeverity)`; a plain upcoming milestone keeps its Green
      "due soon" (regression test passes).
- [x] 3.3 Override path already covered: `HealthScoringOverrideTests` + the fixtures wire
      `critical-milestone-missed` (Schedule ≥Red → Amber). The new Status test produces the Red Schedule
      finding; the scorer's floor is separately tested — the two halves connect, no redundant test needed.
      No existing scoring assertion shifted (suite green).
- [x] 3.4 Full backend suite green: 123 Application + 122 Api = 245.

## 4. Slice C — Key-person concentration (Resource agent, TDD, independent)

- [x] 4.1 (red) Added 4 concentration tests (5→Red, 3→Amber, 2→none, only-on-projects-person-is-on). The
      5-project case failed as expected; the 2-project case correctly stayed clean.
- [x] 4.2 (green) `ResourceSkill` now counts distinct projects per person over the **unfiltered**
      `slice.Data.Assignments` and emits a banded concentration finding (`ConcentrationBand`: 5+ Red / 3–4
      Amber) per project the person is on, cited to that assignment. × absence out of scope, not fabricated.
- [x] 4.3 Full backend suite green: 127 Application + 122 Api = 249. No integration assertion shifted.

## 5. Slice D — Customer-exposure proxy in the roll-up (**gated on #46** — was mis-scoped as independent)

> **Design finding (during apply):** the proxy needs the project's **customer** at roll-up time, but
> `ProjectRecord` has no `Customer` field, the parser doesn't read one, and — the real blocker —
> `ScorePortfolio` runs over **persisted findings** (grouped by `projectKey`), which carry no customer, and
> there is no `Project` entity. A project-level attribute can only reach read time via the **#46 metadata
> field**. So slice D is **gated on #46**, not independent. Deferred to after #46 (with slices E/F).

- [x] 5.1 Customer reaches read time via the **Narrative finding's `MetricDetail["customer"]`** (the one
      finding guaranteed per analyzed project). `ProjectRecord.Customer` + parser (`Customer` column) added;
      `NarrativeSkill` stamps it; fixture Projects sheet carries a `Customer` column.
- [x] 5.2 (red→green) `ScorePortfolioTests.Customer_exposure_groups_at_risk_projects_by_customer` — two Red
      projects sharing a customer → one entry, count 2; empty store → empty.
- [x] 5.3 (green) `ScorePortfolio` groups scored Red/Amber projects by the Narrative-finding customer,
      labelled relationship exposure.
- [x] 5.4 `ExecutivePortfolioEndpointsTests` stays green with the additive field.

## 6. Slice E — Financial-exposure + decision-backlog + key-person roll-up (TDD, **gated on #46 / #47**)

> Prerequisite: #46 (Finding metric) for exposure; #47 (Decisions agent) for the decision-backlog count.
> Do this slice only after those land; until then, leave the exposure/decision panels as placeholders.

- [x] 6.1 (red→green) `ScorePortfolioTests`: 6 new cases — exposure sums the metric (+ currency), exposure
      zero when no amount, decision-backlog counts Decision findings, key-person distinct-by-person, empty
      portfolio empty.
- [x] 6.2 (green) `ScorePortfolio.Result` + handler extended: `FinancialExposure`, `DecisionBacklog`,
      `KeyPersons`, `CustomerExposure`, each from the latest-run findings. Wired the producing agents:
      `ResourceSkill` stamps person + project-count on the concentration finding; `FinancialSkill` already
      stamps exposure (#46); Decision findings from #47.
- [x] 6.3 `ExecutivePortfolioEndpointsTests` green (additive fields; endpoint returns `ScorePortfolio.Result`).
- [x] 6.4 Full backend suite green: 147 Application + 126 Api = 273.

## 7. Slice F — L1 view (presentation)

- [x] 7.1 `ExecutivePortfolio.jsx` renders live **key-person concentration** and **customer-exposure**
      tables (labelled relationship exposure).
- [x] 7.2 Financial-exposure and decision-backlog summary-strip cells now render live values (replaced the
      dashed placeholders); portfolio-level recommendations stay flagged (L1 doesn't roll those up).
- [x] 7.3 Client `vite build` clean.

## 8. Verify + document

- [x] 8.1 Full suite green (147 Application + 126 Api = 273); `openspec validate --strict` passes; build clean.
- [ ] 8.2 **Pending /verify (needs running stack):** upload `orbit-sample.xlsx`, analyze; confirm ORB-1002
      no longer shows "no PM", the missed dress-rehearsal milestone is no longer green, and `GET /api/portfolio`
      returns exposure € / decision backlog / key-person / customer-exposure rendered by the L1 view. This
      also settles the real fixture's `Customer` + `Decisions` columns.
- [x] 8.3 Updated `docs/l1-executive-portfolio-followups.md` (#3/#4/#6) and `docs/dashboard-output-formats.md`
      (L1 rows flipped to backed).
- [x] 8.4 Boundary held: no fabricated commercial-risk figure; `× absence` not invented; no L2/L3 view work;
      the shared pieces came from #45/#46/#47/#48. New this change: the customer channel (Narrative finding)
      + `ProjectRecord.Customer` parsing + the concentration-finding metric stamp.
