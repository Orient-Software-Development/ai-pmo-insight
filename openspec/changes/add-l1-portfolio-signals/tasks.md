> **TDD** — failing test first (red), minimal code (green), refactor; keep the full backend suite green
> before checking off a task. Scoring numbers stay `EXAMPLE` placeholders. This change is **L1-only**; the
> shared infrastructure it consumes lives in tickets **#46** (Finding metric), **#45/#47** (Decisions),
> **#48** (recommendation). Slices 2–4 are independent (no shared dep) and land first; slice 5 (roll-up) is
> **gated** on #46 (exposure) and #47 (decision-backlog). Source: `docs/l1-executive-portfolio-followups.md`.

## 1. Baseline (no code)

- [ ] 1.1 Run the full backend suite; record the green baseline (Application + Api counts).
- [ ] 1.2 Note the tests likely to shift: `HealthScoringServiceTests`, `ScorePortfolioTests`,
      `ExecutivePortfolioEndpointsTests`, Status/Resource agent tests — re-derive shifted assertions
      deliberately (not force-passed) when the fixes land.

## 2. Slice A — Resource "no PM" false-finding fix (TDD, independent)

- [ ] 2.1 (red) Resource-agent test: a project whose only PM assignment has role "Project Management" MUST
      NOT produce a "no project manager" finding. Watch it fail against `Role.Contains("Manager")`.
- [ ] 2.2 (green) Fix the role match in `ResourceSkill` (recognise "Project Management" / "Project Manager"
      / "PM"); a genuinely PM-less project still flags. Refactor into a small helper.
- [ ] 2.3 Re-run the suite; green.

## 3. Slice B — Status "missed milestone → green" fix (TDD, independent)

- [ ] 3.1 (red) Status-agent test: a milestone with `Status == "Missed"` MUST emit a Red Schedule finding,
      not a Green "due soon", even when its due date is within the upcoming window.
- [ ] 3.2 (green) In `StatusSkill`, read `MilestoneRecord.Status`; Missed → Red, At Risk → Amber (overriding
      the date-window Green); milestones with no adverse status keep the existing bands.
- [ ] 3.3 `HealthScoringService` test: the existing `critical-milestone-missed` override now fires when the
      Schedule finding is Red (it couldn't for the missed-critical case before). Re-derive shifted buckets.
- [ ] 3.4 Re-run the suite; green.

## 4. Slice C — Key-person concentration (Resource agent, TDD, independent)

- [ ] 4.1 (red) Resource-agent test: a person on 5 distinct projects → a Red key-person concentration finding
      (cited); a person on 2 projects → none. Bands 5+ Red / 3–4 Amber / <3 none.
- [ ] 4.2 (green) In `ResourceSkill`, compute concentration from the **unfiltered** `slice.Data.Assignments`
      (distinct project keys per person), emit the banded finding attached to the current project's
      assignment for that person. (× absence out of scope — do not fabricate.)
- [ ] 4.3 Re-run the suite; green.

## 5. Slice D — Customer-exposure proxy in the roll-up (TDD, independent)

- [ ] 5.1 (red) `ScorePortfolioTests`: two Red/Amber projects sharing a customer produce a customer-exposure
      entry with an at-risk count; the field is labelled relationship exposure; empty store → empty.
- [ ] 5.2 (green) Extend `ScorePortfolio` to group scored Red/Amber projects by `ProjectRecord.Customer`.
- [ ] 5.3 (red→green) `ExecutivePortfolioEndpointsTests`: `GET /api/portfolio` returns the customer-exposure
      field (additive; auth + empty-store unchanged).
- [ ] 5.4 Re-run the suite; green.

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
