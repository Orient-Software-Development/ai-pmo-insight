## ADDED Requirements

### Requirement: Decisions is a scored health area

The scoring configuration SHALL include a `Decision` health area with a configured weight (a placeholder
`EXAMPLE` value; `IsPlaceholder` stays true). Adding the Decision weight SHALL keep the configured weights
summing to `WeightTotal` (the startup validation enforces this), so the other weights are rebalanced rather
than exceeded. Decision-area findings SHALL contribute to the weighted score exactly like the other areas —
reduced to their worst severity, weighted, and weight-normalised over the areas present.

#### Scenario: Decision findings contribute to the weighted score

- **WHEN** a project's latest run has an `Area == Decision` finding and the config includes a Decision weight
- **THEN** the Decision area participates in the weighted, weight-normalised score alongside the other areas

#### Scenario: Adding the Decision weight keeps the configuration valid

- **WHEN** the Decision weight is added to the scoring configuration
- **THEN** the configured weights still sum to `WeightTotal` and startup validation passes

### Requirement: Key-decision-overdue override applies a worst-case floor

The scoring configuration SHALL support an override that raises a project's health to at least a configured
floor (per the plan doc: minimum Amber) when a Decision-area finding at or above the configured severity is
present ("key decision overdue"). The override SHALL use the existing generic
`{ Area, WhenSeverityAtLeast, Floor }` model, behave as a worst-case floor (never lowering the bucket), and
be auditable like the other overrides — recording the tripping finding.

#### Scenario: An overdue key decision floors the bucket to Amber

- **WHEN** a project's raw bucket is Green but it has a Decision-area finding at or above the override's severity
- **THEN** the final bucket is floored to Amber and the applied override is recorded in the audit trail, cited to the tripping finding

#### Scenario: No qualifying decision leaves the score unchanged

- **WHEN** a project has no Decision-area finding meeting the override severity
- **THEN** the key-decision override does not fire and the score is unchanged
