## Why

The Level-1 Executive Portfolio dashboard renders only 2 of the plan doc's 7 panels from live data; the
rest are dashed placeholders. An explore-mode walkthrough (`docs/l1-executive-portfolio-followups.md`)
verified that the L1-specific gaps are buildable, and that two are outright **defects in deterministic
agents that emit falsehoods a stakeholder would act on**:

- The Resource agent detects the PM role with `Role.Contains("Manager")`, but the fixture role is
  `"Project Management"` — which does not contain the substring `"Manager"` — so it emits a **false
  "No Project Manager is assigned"** on projects that clearly have one.
- The Status agent reads only a milestone's adjusted `DueDate` and ignores `MilestoneRecord.Status`, so a
  **missed** critical milestone renders as a benign green *"due soon"* — which also means the wired
  `critical-milestone-missed` health override never fires for the case it is named for.

This change covers the **L1-specific** work only. The shared infrastructure it builds on lives in separate
tickets so it is not mislabelled as L1-owned (it serves L2/L3 too):

- **#46** — structured metric field on `Finding` (unlocks the € exposure roll-up here, and L3 Age later).
- **#45 + #47** — `HealthArea.Decision` + the Decisions parse/agent/override (unlocks the decision-backlog
  roll-up here, and the L2 decisions-needed panel).
- **#48** — structured recommendation contract (L1 + L2).

Every scoring number stays an `EXAMPLE` placeholder.

## What Changes

**Independent of the shared tickets (agent fixes + new signal):**

- **Fix the Resource "no PM" false finding** — match the project-manager role robustly (against
  `"Project Management"` / `"Project Manager"` / `"PM"` / a role list) instead of a brittle substring.
- **Fix the Status "missed milestone → green" bug** — read `MilestoneRecord.Status`; a `Missed` / `At Risk`
  milestone must carry a non-Green Schedule severity so it is visible and so the existing
  `critical-milestone-missed` override can fire. (`baseline_date` / `is_critical` are not on
  `MilestoneRecord` — re-adding them is a flagged follow-on, out of scope.)
- **Key-person concentration (US-7, plan-doc line 148)** — count distinct projects per person from the
  **unfiltered** `slice.Data.Assignments` (already the full portfolio in every slice) and band it
  **5+ Red / 3–4 Amber / <3 Green**. The `× absence` half is deferred (no parsed absence signal — design).
- **Customer-exposure proxy** — group at-risk (Red/Amber) projects by `ProjectRecord.Customer`, labelled
  **relationship exposure**. The true commercial signal (contract value / margin / SLA) stays a kick-off
  question — not fabricated.

**Depends on the shared tickets landing first (L1 roll-up + view):**

- **L1 roll-up + view** — extend the `ScorePortfolio` result (`GET /api/portfolio`) with financial exposure
  € (from the #46 metric on Financial findings), a decision-backlog count (from #47 Decision findings),
  key-person concentration, and the customer-exposure proxy; the L1 React view renders these from live data,
  replacing the corresponding dashed placeholders. The exposure and decision-backlog parts are gated on #46
  and #47 respectively.

Not in scope (moved to shared tickets or later): the `Finding` metric field itself (#46), the Decisions
enum/parse/agent/override (#45/#47), the structured recommendation (#48); the L2/L3 views; `baseline`/
`is_critical` milestone fields; the US-7 `× absence` combine; Scope area/agent; run-over-run "this-period
progress"; the true commercial-risk signal.

## Capabilities

### New Capabilities

- `l1-portfolio-signals`: the Level-1 roll-up's additional backed panels — financial exposure (€), decision
  backlog, key-person concentration, and the customer-exposure proxy — exposed via `GET /api/portfolio` and
  rendered by the L1 view. Owns the requirement that these panels are produced from live findings (or a
  clearly-labelled proxy), never fabricated, and that the true commercial-risk signal remains out of scope.

### Modified Capabilities

- `analysis-pipeline`: the Resource agent's key-role detection is corrected and gains a **cross-project
  key-person concentration** finding; the Status agent must reflect a milestone's `Status` (a missed
  milestone is not Green). (The new Decision agent, the `Finding` metric, and the structured recommendation
  are the shared tickets #47/#46/#48, not this change.)
- `executive-portfolio`: the roll-up gains financial-exposure (€), decision-backlog, key-person, and
  customer-exposure fields, each backed by findings or a labelled proxy.

## Impact

- **Application:** `ResourceSkill` (role match + concentration) and `StatusSkill` (missed-milestone
  severity) updated; `ScorePortfolio` result extended with the four roll-up fields.
- **API:** `GET /api/portfolio` response grows (additive fields); no breaking change.
- **Client:** `ExecutivePortfolio.jsx` renders the new fields, replacing dashed placeholders for exposure /
  decisions / key-person / customer-exposure; the true commercial-risk panel stays flagged.
- **Prerequisites:** #46 (Finding metric) for the exposure roll-up; #47 (Decisions) for the decision-backlog
  roll-up. The two bug fixes + key-person + customer-exposure have **no** prerequisite and can land first.
- **Tests (TDD):** Resource + Status agent unit tests; `ScorePortfolioTests` + `ExecutivePortfolioEndpointsTests`
  extended; full backend suite stays green.
- **Docs:** update `docs/l1-executive-portfolio-followups.md` (L1 items → done) and
  `docs/dashboard-output-formats.md` (flip the relevant states).
