> Prerequisites (shared tickets): the financial-exposure roll-up consumes the structured `Finding` metric
> (#46); the decision-backlog roll-up consumes the Decision agent's findings (#45 + #47). Those tickets
> define the metric and the Decision agent; this capability only rolls them up and renders them.

## ADDED Requirements

### Requirement: L1 financial exposure is rolled up from findings

The executive portfolio roll-up SHALL report a portfolio financial-exposure figure derived from the
Financial agent's findings (the sum of per-project forecast-over-budget amounts), rendered as a currency
value on the L1 view. The amount SHALL come from a structured finding metric, not by parsing a summary
string. When no project has a forecast overrun the exposure SHALL be zero, not absent.

#### Scenario: Exposure sums forecast overruns across the portfolio

- **WHEN** two projects each carry a Financial finding whose forecast exceeds budget
- **THEN** the roll-up reports a total financial exposure equal to the sum of those overrun amounts, with its currency

#### Scenario: No overruns yields zero exposure

- **WHEN** no project has a forecast-over-budget finding
- **THEN** the roll-up reports zero financial exposure (not a missing/placeholder value)

### Requirement: L1 decision backlog is rolled up from Decision findings

The roll-up SHALL report a decision-backlog count — the number of decisions that are overdue or due soon —
derived from the Decision agent's findings across the portfolio, rendered on the L1 view. Each contributing
decision SHALL remain cited to its source.

#### Scenario: Overdue and due-soon decisions are counted

- **WHEN** the portfolio has decisions past their needed-by date (not approved) and decisions due within the near window
- **THEN** the roll-up reports a decision-backlog count covering both, each traceable to its source finding

### Requirement: L1 key-person concentration is rolled up

The roll-up SHALL report key-person concentration — people allocated across a number of projects that meets
the configured band (per the plan doc: 5+ Red, 3–4 Amber) — derived from the Resource agent's concentration
findings, rendered on the L1 view. The absence dimension of key-person risk is out of scope and SHALL NOT be
fabricated.

#### Scenario: A person over the concentration threshold is surfaced

- **WHEN** a person is allocated across five distinct projects
- **THEN** the roll-up surfaces that person as a key-person concentration risk at the Red band

### Requirement: L1 customer-exposure proxy, labelled and not fabricated

The roll-up SHALL report a customer-exposure view — at-risk (Red/Amber) projects grouped by customer —
explicitly labelled as relationship exposure, NOT as true commercial/contract risk. The true commercial-risk
signal (contract value, margin, SLA penalties) SHALL NOT be produced from data that does not contain it, and
SHALL remain an out-of-scope kick-off question.

#### Scenario: At-risk projects are grouped by customer

- **WHEN** two Red/Amber projects share a customer
- **THEN** the roll-up reports that customer with its at-risk project count, labelled as relationship exposure

#### Scenario: True commercial risk is not fabricated

- **WHEN** the input data carries no contract value, margin, or SLA signal
- **THEN** no commercial-risk figure is invented; the panel presents only the labelled customer-exposure proxy
