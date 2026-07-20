## Context

Analysis is a write-time pipeline; health scoring is a read-side query (`HealthScoringService`); the L1 view
reads `GET /api/portfolio` (`ScorePortfolio`), which fans out over the scorer.

Two facts drive the L1-specific work, both verified in code:

1. **Every agent already holds the whole portfolio.** `ProjectSlice.Data` is the full `CollectedData` — its
   own doc comment says *"Agents filter `Data` to `ProjectKey`"*. So key-person concentration needs no new
   architecture — read the unfiltered `slice.Data.Assignments`.
2. **The two agent bugs are confirmed against the fixtures** — `ResourceSkill`'s `Role.Contains("Manager")`
   fails on `"Project Management"`; `StatusSkill` ignores `MilestoneRecord.Status`, rendering a missed
   milestone as a green "due soon".

This change is scoped to **L1 only**. The shared infrastructure it depends on lives in separate tickets:
**#46** (structured `Finding` metric), **#45 + #47** (`HealthArea.Decision` + Decisions parse/agent/override),
**#48** (structured recommendation). This change consumes them.

## Goals / Non-Goals

**Goals:**

- Remove the two deterministic-agent falsehoods (no-PM, missed-milestone-as-green).
- Add key-person concentration (US-7 concentration half) from the data already in every slice.
- Surface the L1 panels whose data exists — financial exposure (€), decision backlog, key-person, and a
  labelled customer-exposure proxy — via the roll-up and the view, never fabricated.
- TDD throughout; keep the full backend suite green; scoring numbers stay `EXAMPLE` placeholders.

**Non-Goals:**

- The shared infrastructure itself — `Finding` metric (#46), Decisions enum/parse/agent/override (#45/#47),
  structured recommendation (#48). This change references them as prerequisites.
- L2 / L3 view work; `MilestoneRecord.baseline_date` / `is_critical`; the US-7 `× absence` combine; Scope
  area/agent; run-over-run "this-period progress"; the true commercial-risk signal.

## Decisions

**1. Split by prerequisite, so the independent parts land first.** The two bug fixes (no-PM, missed-milestone),
key-person concentration, and the customer-exposure proxy have **no** shared dependency — they ship
immediately. The financial-exposure roll-up needs #46 (the metric carries the amount); the decision-backlog
roll-up needs #47 (the Decision findings exist). `tasks.md` sequences the independent slices first and gates
the roll-up slice on the shared tickets.

**2. Key-person: concentration-only now, `× absence` deferred.** The plan doc's *scoring* rule (line 148) is
pure concentration (5+/3–4/<3). The fixture has **no parsed absence signal** (`resources.csv` has no
`OnLeave`; `time-used.csv` is not parsed), so shipping concentration-only matches the actual scoring rule and
avoids fabricating absence. `× absence` (US-7's combine) is a flagged follow-on.

**3. Concentration attaches per project, not portfolio-once.** A person on 5 projects is a risk **on each**
of those projects, so the finding is emitted per project slice (cited to that project's assignment). The L1
roll-up then reports the distinct people over threshold. This keeps the L2 project view able to show
"depends on an over-committed person" with a citation.

**4. Missed-milestone severity mapping.** In `StatusSkill`, a milestone whose `Status` indicates
missed/at-risk takes a non-Green severity (Missed → Red, At Risk → Amber) regardless of the date-window
branch, so the health Schedule area can reach Red and the existing `critical-milestone-missed` override
fires. Date-based bands still apply to milestones without an adverse status. Using `Status` (present on the
record) rather than `is_critical` (not on the record) is what makes this shippable now.

**5. Customer-exposure proxy is labelled, not fabricated.** Group scored Red/Amber projects by
`ProjectRecord.Customer`; report per-customer at-risk counts as **relationship exposure**. The response field
and the view label both say relationship exposure, explicitly **not** contract/margin/SLA commercial risk.

## Risks / Trade-offs

- **The roll-up slice is gated on two not-yet-landed tickets.** If #46/#47 slip, the exposure and
  decision-backlog panels can't be completed — but the bug fixes, key-person, and customer-exposure still
  ship. Mitigation: sequence the independent slices first; the roll-up slice is last and clearly gated.
- **Scoring behaviour shifts once the missed-milestone fix lands.** ORB-1002's Schedule area can now reach
  Red, so its bucket / the `critical-milestone-missed` override behaviour changes (intended — it was wrong
  before). Every changed `HealthScoringService` / `ScorePortfolio` assertion must be re-derived deliberately,
  not force-passed. Placeholder numbers are preserved.
- **Key-person per-project emission can double-count in a naive roll-up.** The roll-up must count *distinct
  people* over threshold, not sum per-project findings. Covered by a `ScorePortfolioTests` case.
