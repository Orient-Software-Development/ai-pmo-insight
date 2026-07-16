## ADDED Requirements

### Requirement: Findings may carry an optional structured metric

The `Finding` aggregate SHALL support an optional structured metric alongside its summary: a typed numeric
value with a unit (`MetricValue` + `MetricUnit`, e.g. an amount in a currency, a count, a days value) and
an optional small `string`→`string` detail map (`MetricDetail`, e.g. a recommendation's owner / deadline /
action). The metric MUST be optional (all parts nullable) and additive — existing findings without it
remain valid, and the existing invariants (mandatory citation; `Kind == Analysis` ⇒ `Area` + `Severity`)
are unchanged whether or not a metric is present. The metric SHALL persist alongside the finding and read
back intact.

#### Scenario: A finding carries a numeric metric with a unit

- **WHEN** an agent emits a finding for a computed amount (e.g. a financial exposure of 80000 in EUR)
- **THEN** the finding carries the numeric value and its unit as typed data, in addition to the summary,
  so a consumer can sum or sort on it without parsing the summary string

#### Scenario: A finding carries a structured detail map

- **WHEN** an agent emits a finding whose structured content is not a single number (e.g. a recommendation
  with an owner, deadline, and action)
- **THEN** the finding carries those as a `string`→`string` detail map, in addition to the summary

#### Scenario: A finding without a metric remains valid

- **WHEN** an agent emits a finding that has no numeric metric and no detail (e.g. a plain risk note)
- **THEN** the finding is created and persisted normally, with the metric value, unit, and detail all
  absent (null)

#### Scenario: The metric persists and reads back intact

- **WHEN** a finding with a metric value, unit, and detail map is saved and then read back
- **THEN** the value, unit, and detail map are returned unchanged; a finding saved without a metric reads
  back with all three null

#### Scenario: Metric does not weaken existing invariants

- **WHEN** a `Kind == Analysis` finding is created with or without a metric
- **THEN** it still requires an `Area`, a `Severity`, and a citation; and a non-analysis finding still
  carries neither `Area` nor `Severity`
