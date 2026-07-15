## ADDED Requirements

### Requirement: Analysis findings carry a structured health area and severity

An `Analysis`-kind finding SHALL carry, in addition to its existing fields, a structured health `Area`
(e.g. Schedule, Budget, Risk, Resource, DataQuality) and a `Severity` on a Red/Amber/Green scale. These
are typed fields on the finding record, distinct from the free-text `Summary` (which is retained for human
readers) and from `Confidence` (which expresses trust, not severity). Findings whose `Kind` is Narrative,
Challenge, or Review SHALL NOT carry an area or severity (the fields are null for them). The `Area` and
`Severity` on an `Analysis` finding SHALL be persisted and readable back with the finding.

#### Scenario: Analysis finding exposes area and severity

- **WHEN** a deterministic analysis agent emits an `Analysis` finding about a budget overrun
- **THEN** the persisted finding carries `Area = Budget` and a `Severity` value, alongside its `Summary`
  and `Citation`

#### Scenario: Non-analysis findings have no area or severity

- **WHEN** a Narrative, Challenge, or Review finding is persisted
- **THEN** its `Area` and `Severity` are null

#### Scenario: Area and severity round-trip through persistence

- **WHEN** a project's findings are read back from the store
- **THEN** each `Analysis` finding returns the same `Area` and `Severity` it was created with
