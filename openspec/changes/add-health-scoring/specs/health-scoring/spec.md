## ADDED Requirements

### Requirement: Per-project weighted health score

The system SHALL compute a health score for a project by reading that project's `Analysis`-kind findings,
grouping them by health `Area`, reducing each area to a numeric area score (derived from the area's worst
`Severity` via a configured Severity→number mapping), and combining the area scores into a single 0–100
score using configured per-area weights. Scoring SHALL be pure and deterministic (no LLM, no randomness):
the same findings and configuration SHALL always yield the same score. Scoring SHALL run as an on-demand
query, independent of the analysis pipeline, so that re-running it after a configuration change re-scores
without triggering a new analysis run.

#### Scenario: Weighted score combines area severities

- **WHEN** a project's latest-run findings map to Schedule (Amber) and Budget (Red), with configured
  weights and a Severity→number mapping
- **THEN** the service returns a single 0–100 `rawScore` equal to the weighted sum of the per-area numbers

#### Scenario: Scoring is a query, re-runnable without re-analysis

- **WHEN** the configuration weights change and the score is requested again for the same project
- **THEN** a new score is returned reflecting the new weights, and no new analysis run is created

#### Scenario: Deterministic

- **WHEN** the same project findings and configuration are scored twice
- **THEN** both calls return an identical `rawScore`, `rawBucket`, and `finalBucket`

### Requirement: RAG bucketing from the weighted score

The system SHALL map the numeric score to a Red/Amber/Green (RAG) bucket using configured thresholds
(e.g. Green ≥ high threshold, Amber ≥ low threshold, else Red). Threshold values SHALL come from
configuration, not be hardcoded. "RAG" denotes the Red/Amber/Green health colour.

#### Scenario: Score buckets by configured thresholds

- **WHEN** a `rawScore` of 82 is bucketed with thresholds Green≥80 / Amber≥60
- **THEN** the `rawBucket` is Green

#### Scenario: Boundary lands in the configured band

- **WHEN** a `rawScore` equal to the Amber lower threshold is bucketed
- **THEN** it falls in Amber, not Red (thresholds are inclusive lower bounds as configured)

### Requirement: Override rules apply a worst-case floor

The system SHALL apply configured override rules that set a *floor* on the bucket (e.g. critical milestone
missed → minimum Amber; critical unmitigated risk → minimum Red). When multiple overrides fire, the most
severe floor SHALL win (minimum Red beats minimum Amber beats the raw bucket). An override SHALL only fire
when the finding signal it depends on is present in the scored set; when the signal is absent the override
SHALL NOT fire and SHALL NOT produce a synthetic warning.

#### Scenario: Override raises severity above the raw bucket

- **WHEN** the `rawBucket` is Green but a finding trips the "critical milestone missed → minimum Amber"
  override
- **THEN** the `finalBucket` is Amber

#### Scenario: Worst-case floor wins when overrides collide

- **WHEN** both a "minimum Amber" and a "minimum Red" override fire on the same project
- **THEN** the `finalBucket` is Red

#### Scenario: Override cannot lower severity

- **WHEN** the `rawBucket` is Red and a "minimum Amber" override fires
- **THEN** the `finalBucket` stays Red (a floor never improves the rating)

#### Scenario: Absent signal does not fire the override

- **WHEN** no finding represents a missed critical milestone
- **THEN** the "critical milestone missed" override does not fire and no synthetic warning is emitted

### Requirement: Score is scoped to the latest run per project

The system SHALL score only the findings from the **latest analysis run** for the project. When a project
has been analyzed multiple times (findings appended under successive `RunId`s), older runs' findings SHALL
be excluded. When a run spans multiple projects, the latest run SHALL be resolved per project key, not
globally.

#### Scenario: Only the newest run contributes

- **WHEN** a project has findings from two runs and the newer run downgrades a milestone
- **THEN** the score reflects the newer run's findings only; the older run's findings are ignored

#### Scenario: Latest run resolved per project

- **WHEN** one multi-file run produced findings for projects A and B, and A was later re-analyzed alone
- **THEN** project A scores from its newer run and project B scores from the shared run

### Requirement: Auditable score result

The score result SHALL expose enough to explain itself: `rawScore`, `rawBucket`, an ordered list of
`appliedOverrides` (each naming the rule and the finding/citation that tripped it), the `finalBucket`, an
aggregate `confidence`, and a per-area breakdown (`area`, `severity`, `weight`, `contribution`). When an
override changed the bucket, both the pre-override and post-override buckets SHALL be visible.

#### Scenario: Override change is visible in the result

- **WHEN** the raw bucket was Green and an override forced Amber
- **THEN** the result shows `rawBucket = Green`, `finalBucket = Amber`, and an `appliedOverrides` entry
  naming the rule and the source finding

#### Scenario: Area breakdown is returned

- **WHEN** a project is scored
- **THEN** the result lists each contributing area with its severity, weight, and contribution to the score

### Requirement: Very-low data confidence yields "Needs PM Review"

When the aggregate confidence of a project's scored findings is below a configured floor, the system SHALL
mark the project's status as "Needs PM Review" — a state distinct from the RAG colour, computed from the
already-persisted `Confidence` on findings with no additional data.

#### Scenario: Low confidence supersedes the colour

- **WHEN** a project's aggregate finding confidence is below the configured floor
- **THEN** the result carries a "Needs PM Review" status alongside (and taking precedence over) the RAG
  colour

### Requirement: External, validated, swappable scoring configuration

Weights, Severity→number mappings, RAG thresholds, and override rules SHALL be supplied by external
configuration, not hardcoded. The configuration SHALL be validated at startup — weights SHALL sum to the
configured total (e.g. 100) and thresholds SHALL be ordered — failing fast with a message naming the
offending key. The shipped default configuration SHALL carry the PRD's EXAMPLE values as an explicit
placeholder and SHALL be replaceable without code changes.

#### Scenario: Invalid configuration fails fast

- **WHEN** the configured area weights do not sum to the configured total
- **THEN** startup fails with an error naming the weights configuration, and no scoring is served

#### Scenario: Default config is a labelled placeholder

- **WHEN** the service runs with the shipped default configuration
- **THEN** it scores using the EXAMPLE values and a startup log line states the configuration is the
  placeholder set until overridden
