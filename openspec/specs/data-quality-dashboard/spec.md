# data-quality-dashboard Specification

## Purpose
TBD - created by archiving change add-data-quality-dashboard. Update Purpose after archive.
## Requirements
### Requirement: Portfolio-wide data-quality roll-up

The system SHALL compute a portfolio-wide data-quality summary by enumerating every project on record
and collecting the `DataQuality`-area findings from each project's latest analysis run. Enumeration SHALL
reuse the existing distinct-project-key discovery; per-project findings SHALL be resolved to the latest
run (newest run) exactly as the health scorer does. The roll-up SHALL NOT re-run analysis or invoke the
LLM. Only findings whose health area is `DataQuality` SHALL be included; findings of other areas or of
non-analysis kinds SHALL be excluded.

#### Scenario: Latest-run data-quality findings are collected across projects

- **WHEN** three projects each have a latest run carrying `DataQuality` findings
- **THEN** the roll-up includes exactly those projects' latest-run `DataQuality` findings and no findings
  from earlier runs

#### Scenario: Non-data-quality findings are excluded

- **WHEN** a project's latest run has both `DataQuality` findings and findings in other areas (e.g. Budget)
- **THEN** only the `DataQuality` findings appear in the roll-up

### Requirement: Confidence block with configured publish threshold

The roll-up SHALL report a portfolio confidence block containing the mean of each scored project's
aggregate confidence, the configured publish threshold, and a below-target flag that is set when the mean
is strictly below the threshold. The mean SHALL be computed via the existing per-project health-scoring
confidence (so it never disagrees with the scores shown elsewhere), averaged across scored projects. The
threshold SHALL be read from the existing health-scoring configuration (`ConfidenceFloor`) and SHALL NOT
be hard-coded in the data-quality slice. Projects that produce no score SHALL NOT contribute to the mean.

#### Scenario: Below-target flag reflects the threshold

- **WHEN** the portfolio mean confidence is below the configured publish threshold
- **THEN** the confidence block reports that mean, the threshold, and a below-target flag set to true

#### Scenario: At-or-above target is not flagged

- **WHEN** the portfolio mean confidence is at or above the configured publish threshold
- **THEN** the below-target flag is false

#### Scenario: Unscoreable project does not distort the mean

- **WHEN** a project enumerates but produces no score
- **THEN** it does not contribute to the mean confidence

### Requirement: Missing/inconsistent items are listed worst-first with a cited source

The roll-up SHALL produce a list of missing/inconsistent items, one entry per collected `DataQuality`
finding. Each entry SHALL carry the project key, the issue text (the finding summary), the finding's RAG
severity, and the citation locator of the source that evidences it. The list SHALL be ordered worst-first
by severity (Red before Amber before Green). The roll-up SHALL also report the total item count and a
per-project item count. No entry SHALL be present without a citation locator.

#### Scenario: Items are ordered by severity

- **WHEN** the portfolio has both Red (inconsistent/orphan) and Amber (missing field) data-quality items
- **THEN** the items list places all Red entries before Amber entries

#### Scenario: Each item cites its source

- **WHEN** an item is produced for a `DataQuality` finding
- **THEN** the entry carries the project key, the issue text, the severity, and the finding's citation
  locator

#### Scenario: Counts reflect the items

- **WHEN** the portfolio has five `DataQuality` items spread across two projects
- **THEN** the roll-up reports a total item count of 5 and a per-project count for each of the two projects

### Requirement: Data-quality read endpoint

The system SHALL expose an authenticated, view-only endpoint `GET /api/data-quality/summary` that returns
the data-quality roll-up (the confidence block, the worst-first cited items list, and the counts). Any
authenticated caller SHALL see the whole portfolio (shared-workspace visibility, consistent with the other
read surfaces). An unauthenticated caller SHALL receive 401. When the findings store is empty the endpoint
SHALL return 200 with a zeroed confidence block and an empty items list — never 404.

#### Scenario: Populated portfolio returns the roll-up

- **WHEN** an authenticated caller requests the data-quality summary and data-quality findings exist
- **THEN** the response is 200 with the confidence block, the worst-first cited items list, and the counts

#### Scenario: Empty store returns a zeroed 200

- **WHEN** an authenticated caller requests the data-quality summary and the findings store is empty
- **THEN** the response is 200 with an empty items list, a zero item count, and no error

#### Scenario: Unauthenticated caller is rejected

- **WHEN** an unauthenticated caller requests the data-quality summary
- **THEN** the response is 401

### Requirement: L3 data-quality view renders backed panels and flags unbacked ones

The Level-3 data-quality view (React SPA, protected route `/data-quality`) SHALL consume
`GET /api/data-quality/summary` and render the v2 wireframe layout on the shared Phase 5 design system.
Panels the roll-up can populate — the confidence hero (mean confidence, publish threshold, below-target
banner) and the missing/inconsistent items table (project, issue, severity, citation, worst-first) — SHALL
be rendered from live data. Panels the current `DataQuality` finding shape cannot back — a per-item age
column, a suggested-remediation column, ordering by a quantified confidence *lift*, an eight-category
areas-completeness grid, and the duplicate-identity candidates table with merge/keep-separate actions —
SHALL render a clear "not yet captured" follow-on state and SHALL NOT display fabricated values. The view
SHALL NOT ship a merge or keep-separate action while no duplicate-identity signal exists (US-2's
never-silently-merge rule). This view SHALL require no change to agents, prompts, finding shape, or
existing API contracts.

#### Scenario: Backed panels show live data-quality data

- **WHEN** the L3 view loads a portfolio with `DataQuality` findings
- **THEN** the confidence hero and the missing/inconsistent items table render from the
  `GET /api/data-quality/summary` response

#### Scenario: Unbacked panel is flagged, not fabricated

- **WHEN** the v2 layout includes the duplicate-identity candidates table or the areas-completeness grid,
  which the roll-up cannot populate
- **THEN** those panels render a "not yet captured — follow-on" state rather than invented values
- **AND** no merge/keep-separate action is presented, and no agent, prompt, finding-shape, or API contract
  change is introduced by this view

