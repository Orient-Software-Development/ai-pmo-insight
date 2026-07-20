## ADDED Requirements

### Requirement: Roll-up carries the additional backed L1 signals

The portfolio roll-up (`GET /api/portfolio`) SHALL, in addition to the existing RAG counts / confidence /
intervention list, carry: a total financial-exposure amount with currency, a decision-backlog count, a
key-person concentration list, and a customer-exposure grouping. Each SHALL be derived from findings (or, for
customer exposure, from the scored projects' customer + health) — never fabricated. Fields SHALL be additive
to the existing response (no breaking change); an empty store SHALL yield zeroed/empty values, never 404.

#### Scenario: Populated portfolio returns the new fields

- **WHEN** an authenticated caller requests the portfolio and scored projects with exposure, decisions, and concentration exist
- **THEN** the response includes financial exposure (amount + currency), a decision-backlog count, a key-person concentration list, and a customer-exposure grouping — alongside the existing counts and intervention list

#### Scenario: Empty store returns zeroed new fields

- **WHEN** an authenticated caller requests the portfolio and the findings store is empty
- **THEN** the new fields are zero/empty (exposure 0, decision-backlog 0, empty concentration and customer-exposure), with no error

### Requirement: L1 view renders the newly-backed panels from live data

The Level-1 executive view SHALL render the financial-exposure, decision-backlog, key-person, and
customer-exposure panels from the roll-up response, replacing the previous "not yet captured" placeholders
for those panels. The customer-exposure panel SHALL be labelled as relationship exposure, and any panel the
roll-up still cannot back (true commercial risk) SHALL remain a clearly-flagged placeholder rather than
showing fabricated values.

#### Scenario: Backed panels show live values

- **WHEN** the L1 view loads a portfolio whose roll-up carries exposure, decisions, and concentration
- **THEN** those panels render the live values instead of dashed placeholders

#### Scenario: Still-unbacked panel stays flagged

- **WHEN** the view reaches the true commercial-risk panel, which the data cannot back
- **THEN** it renders a labelled "not yet captured" state, never a fabricated figure
