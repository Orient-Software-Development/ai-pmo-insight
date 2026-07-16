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

- [ ] 5.1 (after #46) Carry the project's customer to read time via the #46 metric/metadata field.
- [ ] 5.2 (red) `ScorePortfolioTests`: two Red/Amber projects sharing a customer produce a customer-exposure
      entry with an at-risk count; labelled relationship exposure; empty store → empty.
- [ ] 5.3 (green) Group scored Red/Amber projects by customer in `ScorePortfolio`.
- [ ] 5.4 (red→green) `ExecutivePortfolioEndpointsTests`: the endpoint returns the customer-exposure field.

## 6. Slice E — Financial-exposure + decision-backlog + key-person roll-up (TDD, **gated on #46 / #47**)

> Prerequisite: #46 (Finding metric) for exposure; #47 (Decisions agent) for the decision-backlog count.
> Do this slice only after those land; until then, leave the exposure/decision panels as placeholders.

- [ ] 6.1 (red) `ScorePortfolioTests`: the result carries total financial exposure (amount + currency, from
      the #46 metric on Financial findings), a decision-backlog count (from #47 Decision findings), and a
      key-person concentration list (distinct people over threshold — not a sum of per-project findings);
      empty store → zeroed/empty.
- [ ] 6.2 (green) Extend the `ScorePortfolio` result + handler to compute those from the scored projects'
      findings.
- [ ] 6.3 (red→green) `ExecutivePortfolioEndpointsTests`: the endpoint returns the new fields (additive).
- [ ] 6.4 Re-run the suite; green (re-derive shifted expectations deliberately).

## 7. Slice F — L1 view (presentation)

- [ ] 7.1 Update `ExecutivePortfolio.jsx` to render the customer-exposure and key-person panels from the
      response (available after slices D + C).
- [ ] 7.2 Render the financial-exposure and decision-backlog panels once slice E lands, replacing those
      dashed placeholders. Keep the true commercial-risk panel flagged (labelled, not fabricated).
- [ ] 7.3 Client `vite build` clean.

## 8. Verify + document

- [ ] 8.1 Full backend suite green; `openspec validate add-l1-portfolio-signals --strict` passes;
      `dotnet build` clean.
- [ ] 8.2 `/verify` against the running stack: upload `orbit-sample.xlsx`, analyze; confirm ORB-1002 no
      longer shows "no PM" and the missed dress-rehearsal milestone is no longer green; `GET /api/portfolio`
      returns the customer-exposure + key-person (and, once #46/#47 land, exposure € + decision backlog); the
      L1 view renders them.
- [ ] 8.3 Update `docs/l1-executive-portfolio-followups.md` (#4 key-person + no-PM done; #6 proxy done; #3/#5
      done once slice E lands) and `docs/dashboard-output-formats.md` (flip the relevant states).
- [ ] 8.4 Confirm the boundary held: no fabricated commercial-risk figure; `× absence` not invented; no L2/L3
      view work; the shared pieces came from #45/#46/#47/#48, not re-implemented here.
