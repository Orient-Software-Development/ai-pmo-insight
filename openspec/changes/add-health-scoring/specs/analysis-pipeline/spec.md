## ADDED Requirements

### Requirement: Deterministic agents emit structured area and severity

The deterministic agents (#2 Data Quality, #3 Status, #5 Financial, #6 Resource) SHALL stamp a structured
health `Area` and `Severity` (Red/Amber/Green) onto every `Analysis` finding they produce, surfacing the
severity signal they already compute internally (e.g. budget overrun band, days-late band, allocation
concentration) rather than encoding it only in the free-text summary. An agent MAY produce findings in more
than one area; `Area` SHALL be set per finding, not per agent. Emitting the structured fields SHALL NOT
change which findings an agent produces nor their summaries — it is additive provenance.

#### Scenario: Financial agent stamps Budget area and a severity

- **WHEN** the Financial agent (#5) emits a forecast-overrun finding
- **THEN** the finding carries `Area = Budget` and a `Severity` reflecting the overrun band, in addition to
  its existing summary and citation

#### Scenario: One agent can emit multiple areas

- **WHEN** an agent produces findings that concern different health areas in the same run
- **THEN** each finding carries its own `Area`, independent of the producing agent's identity

#### Scenario: Enrichment is additive

- **WHEN** the agents run over a fixture that previously produced N analysis findings
- **THEN** the same N findings are produced with the same summaries, each now additionally carrying an
  `Area` and `Severity`
