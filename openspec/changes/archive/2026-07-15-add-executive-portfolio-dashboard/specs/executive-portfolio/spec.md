## ADDED Requirements

### Requirement: Portfolio-wide project discovery

The system SHALL enumerate the distinct project keys present in the findings store without requiring a
caller to already know any key. Enumeration SHALL be derived from persisted findings (the distinct set of
`project_key` values); it SHALL NOT depend on a first-class project entity. A project key SHALL appear in
the enumeration when at least one finding cites it, and SHALL appear at most once.

#### Scenario: Distinct keys are returned

- **WHEN** findings exist for three distinct project keys (one key having several findings)
- **THEN** the enumeration returns exactly those three keys, each once

#### Scenario: Empty store yields no keys

- **WHEN** the findings store is empty
- **THEN** the enumeration returns an empty set (not an error)

### Requirement: Portfolio roll-up over per-project health scores

The system SHALL compute a portfolio roll-up by scoring each enumerated project via the existing
per-project health-scoring service (its latest run) and aggregating the results. Scoring SHALL reuse the
existing service — the roll-up SHALL NOT re-run analysis or invoke the LLM. The roll-up SHALL include: the
count of projects in each RAG bucket (`Green` / `Amber` / `Red`), the count of projects flagged "Needs PM
Review", and an aggregate confidence across scored projects. Projects that enumerate but produce no score
SHALL be excluded from the RAG counts (they are not scoreable), and their exclusion SHALL NOT be counted as
any colour.

#### Scenario: RAG counts reflect each project's final bucket

- **WHEN** the portfolio has two Red, one Amber, and three Green scored projects
- **THEN** the roll-up reports red=2, amber=1, green=3

#### Scenario: Needs-PM-Review is counted separately from colour

- **WHEN** two scored projects have `NeedsPmReview` set (on any colour)
- **THEN** the roll-up reports a needs-PM-review count of 2, independent of the RAG counts

#### Scenario: Unscoreable project does not distort counts

- **WHEN** a project enumerates but has no scoreable findings (its score is null)
- **THEN** it is absent from the RAG counts and contributes to no bucket

### Requirement: Projects needing intervention are ranked with a cited reason

The system SHALL produce an ordered list of the projects needing intervention — the projects whose final
bucket is `Red` or `Amber` — ordered worst-first (Red before Amber). Each entry SHALL carry the project
key, the final RAG bucket (its status), the project's aggregate confidence, a worst-area reason describing
why it needs attention, and the citation locator of the finding (or applied-override finding) that drove
that reason. Green projects SHALL NOT appear in the intervention list.

#### Scenario: Red projects rank above Amber

- **WHEN** the portfolio has Red and Amber projects needing intervention
- **THEN** the intervention list places all Red entries before Amber entries

#### Scenario: Each entry cites its driving finding

- **WHEN** an intervention entry is produced for a Red project whose bucket was set by an applied override
- **THEN** the entry's reason names the driving area/override and includes the citation locator of the
  finding that tripped it

#### Scenario: Green projects are excluded

- **WHEN** a project's final bucket is Green
- **THEN** it does not appear in the intervention list

### Requirement: Portfolio read endpoint

The system SHALL expose an authenticated, view-only endpoint `GET /api/portfolio` that returns the
portfolio roll-up (RAG counts, needs-PM-review count, aggregate confidence, and the ordered intervention
list). Any authenticated caller SHALL see the whole portfolio (shared-workspace visibility, consistent with
the other read surfaces). An unauthenticated caller SHALL receive 401. When the findings store is empty the
endpoint SHALL return 200 with zeroed counts and an empty intervention list — never 404.

#### Scenario: Populated portfolio returns the roll-up

- **WHEN** an authenticated caller requests the portfolio and scored projects exist
- **THEN** the response is 200 with the RAG counts, needs-PM-review count, aggregate confidence, and the
  worst-first intervention list

#### Scenario: Empty store returns a zeroed 200

- **WHEN** an authenticated caller requests the portfolio and the findings store is empty
- **THEN** the response is 200 with red=0, amber=0, green=0, an empty intervention list, and no error

#### Scenario: Unauthenticated caller is rejected

- **WHEN** an unauthenticated caller requests the portfolio
- **THEN** the response is 401

### Requirement: L1 executive view renders backed panels and flags unbacked ones

The Level-1 executive view (React SPA, protected route) SHALL consume `GET /api/portfolio` and render the
v2 wireframe layout. Panels the roll-up can populate — portfolio health (G/A/R counts), aggregate
confidence with the "Needs PM Review" count, and the projects-needing-intervention list (with status,
confidence, reason, and citation) — SHALL be rendered from live data. Panels the current finding shape
cannot back — financial exposure in currency amounts, per-decision days-open/owner detail, per-person
key-person risk (allocation × absence), and owned/dated recommended actions — SHALL render a clear "not yet
captured" follow-on state and SHALL NOT display fabricated values. This view SHALL require no change to
agents, prompts, finding shape, or existing API contracts.

#### Scenario: Backed panels show live portfolio data

- **WHEN** the L1 view loads a portfolio with scored projects
- **THEN** the health counts, confidence, needs-PM-review count, and intervention list render from the
  `GET /api/portfolio` response

#### Scenario: Unbacked panel is flagged, not fabricated

- **WHEN** the v2 layout includes a financial-exposure (€) or owned-recommendations panel that the roll-up
  cannot populate
- **THEN** that panel renders a "not yet captured — follow-on" state rather than invented figures
- **AND** no agent, prompt, finding-shape, or API contract change is introduced by this view
