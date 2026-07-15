## ADDED Requirements

### Requirement: Wireframe-conformant Level-2 presentation

The Level-2 project status view SHALL present its existing content in the Phase 5 v2 wireframe's `l2`
design system, reusing the shared design tokens the Level-1 view established (palette, type, RAG custom
properties, severity chips, records table, hairline rules). This requirement is presentation-only: the
view's data path — the concurrent read of the findings surface and the health surface, the mapping of the
health response to its render states, and the four cited sections — SHALL be unchanged in behaviour. The
view SHALL render a project header presenting the project key and name, the RAG colour (`FinalBucket`) as a
labelled chip, the aggregate confidence, an indication when the score was overridden (`FinalBucket` differs
from `RawBucket`), the sponsor and PM where those are available, and a project switcher; and SHALL present
the body in the wireframe's sections with each risk/issue row keeping its Finding / Challenge / Review
breakdown and its citation. Status SHALL never be conveyed by colour alone.

#### Scenario: L2 renders in the wireframe design system

- **WHEN** an authenticated user opens the Level-2 view for a scored project
- **THEN** the view presents a project header with the project key/name, a labelled RAG chip, the
  confidence, and (when present) sponsor/PM and a project switcher, styled to the v2 `l2` system
- **AND** the four cited sections still render, each item citing its source

#### Scenario: Data path is unchanged by the retrofit

- **WHEN** the retrofit is applied
- **THEN** the view still reads the findings and health surfaces concurrently and maps the health response
  to the same scored / scoring-pending / not-found / error render states as before
- **AND** the existing backend data-path test for the Level-2 view continues to pass

#### Scenario: Overridden score is indicated in the header

- **WHEN** the health surface returns `RawBucket` = Green but `FinalBucket` = Amber (an override fired)
- **THEN** the header presents the Amber (final) RAG chip and indicates the score was overridden

#### Scenario: Status is never colour-only

- **WHEN** the view presents the RAG colour anywhere in the L2 presentation
- **THEN** it is always paired with a text label and the numeric score (colour-blind safe)
