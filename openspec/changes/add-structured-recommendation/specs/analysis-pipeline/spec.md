## ADDED Requirements

### Requirement: Narrative recommendation is carried as structured data

The Narrative agent's finding SHALL carry its recommendation as structured detail — `owner`, `deadline`,
`action`, and `rationale` — on the finding's metric detail, in addition to the existing prose summary, so a
consumer can read the fields as data rather than parsing the summary string. The prose summary SHALL be
preserved for back-compat. This SHALL apply to both the template-produced and LLM-produced narrative paths,
and SHALL require no change to the narrative prompt or the LLM output contract.

#### Scenario: The narrative finding exposes the recommendation fields

- **WHEN** the Narrative agent produces a recommendation (via either the template or the LLM path)
- **THEN** the finding carries `owner`, `deadline`, `action`, and `rationale` as structured detail, and
  still carries the human-readable summary

#### Scenario: Structured detail matches the recommendation the summary describes

- **WHEN** the narrative finding is produced
- **THEN** the `owner` / `deadline` / `action` / `rationale` in the structured detail are the same values
  rendered in the summary prose (not a separate or fabricated set)
