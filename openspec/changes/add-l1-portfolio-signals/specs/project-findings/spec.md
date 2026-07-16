## ADDED Requirements

### Requirement: Findings may carry an optional structured metric

The `Finding` aggregate SHALL support an optional structured metric alongside its summary — a numeric value
with a unit (e.g. an amount in a currency, a count, a days value) and/or a small set of named detail fields
(e.g. a recommendation's owner / deadline / action). The metric MUST be optional (nullable) and additive:
existing findings without it remain valid, and the existing invariants (mandatory citation;
`Kind == Analysis` ⇒ `Area` + `Severity`) are unchanged. The metric SHALL persist alongside the finding.

#### Scenario: A finding carries a numeric metric with a unit

- **WHEN** an agent emits a finding for a computed amount (e.g. financial exposure)
- **THEN** the finding carries the numeric value and its unit as structured data, in addition to the human-readable summary

#### Scenario: A finding without a metric remains valid

- **WHEN** an agent emits a finding that has no numeric metric (e.g. a plain risk note)
- **THEN** the finding is created and persisted normally, with the metric absent (null)

#### Scenario: Metric does not weaken existing invariants

- **WHEN** a `Kind == Analysis` finding is created
- **THEN** it still requires an `Area` and `Severity` and a citation, whether or not a metric is present
