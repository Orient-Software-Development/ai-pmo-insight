## Why

The Level-1 Executive Portfolio dashboard renders only 2 of the plan doc's 7 panels from live data
(health counts + intervention list); the other five are dashed placeholders. An explore-mode walkthrough
(`docs/l1-executive-portfolio-followups.md`) verified that most of those placeholders are **not**
client-blocked — the data and formulas already exist, and two are outright **defects in deterministic
agents that emit falsehoods a stakeholder would act on**:

- The Resource agent detects the PM role with `Role.Contains("Manager")`, but the fixture role is
  `"Project Management"` — which does not contain the substring `"Manager"` — so it emits a **false
  "No Project Manager is assigned"** on projects that clearly have one.
- The Status agent reads only a milestone's adjusted `DueDate` and ignores `MilestoneRecord.Status`, so a
  **missed** critical milestone renders as a benign green *"due soon"*. This also means the wired
  `critical-milestone-missed` health override never fires for the case it is named for.

Beyond the bugs, the raw material for financial exposure (€), the decision backlog, and key-person
concentration is already computed or already in memory — it is either trapped in a finding's summary
string or thrown away by per-project filtering. This change turns those into structured, rolled-up L1
signals, and adds the one genuinely missing area (Decisions). It is grounded in the plan doc's KPIs and
the shipped fixtures; every scoring number stays an `EXAMPLE` placeholder.

## What Changes

- **Fix the Resource "no PM" false finding** — match the project-manager role robustly (e.g. against
  `"Project Management"` / `"Manager"` / a role list) instead of a brittle substring.
- **Fix the Status "missed milestone → green" bug** — read `MilestoneRecord.Status`; a `Missed` / `At Risk`
  milestone must carry a non-Green severity so it is visible and so the `critical-milestone-missed` override
  can fire. (`baseline_date` / `is_critical` are not on `MilestoneRecord` — re-adding them is a flagged
  follow-on, out of scope here.)
- **Key-person concentration (US-7, plan-doc line 148)** — count distinct projects per person from the
  **unfiltered** `slice.Data.Assignments` (already the full portfolio in every slice) and band it
  **5+ Red / 3–4 Amber / <3 Green**. The `× absence` half of US-7 is **deferred** — the fixture carries no
  absence signal in the parsed data (`resources` has no `OnLeave`, `time-used` is unparsed); shipping
  concentration-only matches the plan doc's actual scoring rule (see design decision).
- **Decisions area + agent** — add `HealthArea.Decision`; parse a `Decisions` sheet into a new
  `DecisionRecord`; a deterministic `DecisionSkill` emits **overdue** (past `needed_by`, not `Approved`) and
  **due-soon** findings, each cited; wire the plan-doc override *"key decision overdue → minimum Amber"* and
  a `Decisions` scoring weight (placeholder). This lights up **both** the L1 decision-backlog panel and the
  L2 decisions-needed panel.
- **Structured metric on `Finding`** — add optional typed metric/metadata to the `Finding` aggregate
  (persisted; EF migration) so computed numbers stop being trapped in the `Summary` string. Use it for
  (a) the **financial exposure € amount + currency** from the Financial agent, and (b) the **Narrative
  recommendation** owner/deadline/action as structured fields rather than flattened prose. The same field
  later unlocks the L3 Age column (out of scope now).
- **Customer-exposure proxy** for the L1 "client / commercial risk" panel — group at-risk (Red/Amber)
  projects by `ProjectRecord.Customer`, clearly labelled **relationship exposure**. The **true** commercial
  signal (contract value / margin / SLA-penalty) stays a kick-off question — not fabricated.
- **L1 roll-up + view** — extend the `ScorePortfolio` result (`GET /api/portfolio`) with financial exposure
  €, decision-backlog count, key-person concentration, and the customer-exposure proxy; the L1 React view
  renders these from live data, replacing the corresponding dashed placeholders.

Not in scope: the L2/L3 view work (this change only touches the shared agents + the L1 surface); `baseline`/
`is_critical` milestone fields; the US-7 `× absence` combine; Scope area/agent; run-over-run "this-period
progress"; the true commercial-risk signal.

## Capabilities

### New Capabilities

- `l1-portfolio-signals`: The Level-1 executive roll-up's additional backed panels — financial exposure (€),
  decision backlog, key-person concentration, and the customer-exposure proxy — exposed via `GET /api/portfolio`
  and rendered by the L1 view. Owns the requirement that these panels are produced from live findings (or a
  clearly-labelled proxy), never fabricated, and that the true commercial-risk signal remains out of scope.

### Modified Capabilities

- `analysis-pipeline`: the Resource agent's key-role detection is corrected and gains a **cross-project
  key-person concentration** finding; the Status agent must reflect a milestone's `Status` (a missed
  milestone is not Green); a new **Decision** agent + `DecisionRecord` parsing is added; the Financial agent
  stamps the exposure **amount** and the Narrative agent stamps a **structured recommendation** via the new
  `Finding` metric field.
- `health-scoring`: a new `Decision` health area (weight, placeholder) and a **key-decision-overdue**
  override; behaviour is otherwise unchanged (still config-driven, placeholder numbers).
- `executive-portfolio`: the roll-up gains financial-exposure (€), decision-backlog, key-person, and
  customer-exposure fields, each backed by findings or a labelled proxy.
- `project-findings`: the `Finding` aggregate gains an optional structured **metric** (value/unit/metadata)
  alongside the existing summary, so numeric findings carry the number, not just prose.

## Impact

- **Domain:** `Finding` gains optional metric fields (+ `Finding.Create` validation); `HealthArea` gains
  `Decision`. **EF migration** for the new `Finding` column(s).
- **Application:** `ResourceSkill`, `StatusSkill`, `FinancialSkill`, `NarrativeSkill` updated; new
  `DecisionSkill` + `DecisionRecord` + orchestrator wiring; `ScorePortfolio` result extended;
  `HealthScoringOptions` gains a Decision weight + override (placeholder).
- **Infrastructure:** `ExcelProjectParser` reads a `Decisions` sheet (fixture: confirm/add the tab).
- **API:** `GET /api/portfolio` response shape grows (additive fields); no breaking change.
- **Client:** `ExecutivePortfolio.jsx` renders the new fields, replacing dashed placeholders for exposure /
  decisions / key-person / customer-exposure.
- **Tests (TDD):** unit tests for each agent change + `DecisionSkill`; `HealthScoringService` override/area
  tests; `ScorePortfolioTests` extended; `ExecutivePortfolioEndpointsTests` extended; full backend suite
  stays green.
- **Docs:** update `docs/l1-executive-portfolio-followups.md` (items → done) and
  `docs/dashboard-output-formats.md` (states + the corrected scoring facts).
