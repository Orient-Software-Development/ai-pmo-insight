## ADDED Requirements

### Requirement: Master-detail history layout

The `/history` view SHALL present a master-detail layout: a master list of every upload (newest first) and
a detail panel for the currently selected upload. The master list SHALL be populated from
`GET /api/uploads` and SHALL show, per row, the data that read returns — the upload date and file name.
Selecting a row SHALL load that upload's latest analysis run via `GET /api/uploads/{id}/findings` into the
detail panel. Before any selection the detail panel SHALL prompt the user to select an upload. The view
SHALL require no change to any endpoint, agent, prompt, or finding shape.

#### Scenario: Selecting an upload loads its detail

- **WHEN** the user selects an upload row in the master list
- **THEN** the detail panel loads that upload's latest run from `GET /api/uploads/{id}/findings`

#### Scenario: Empty store shows an empty master list

- **WHEN** no uploads exist
- **THEN** the master list renders an empty state and the detail panel prompts for a selection, with no error

### Requirement: Run-provenance detail header

The detail panel SHALL render a run-provenance header built only from data the findings response carries:
the analysis run id, the run date, and the prompt hash(es) (the `PromptVersion` content hash present on
LLM-produced findings). When multiple LLM findings in the run carry different prompt hashes, the header
SHALL surface the distinct set. Fields the findings response does not carry — the uploader and the LLM
model id — SHALL NOT be shown as if known; they SHALL render a "not captured" follow-on state.

#### Scenario: Header shows run id, date, and prompt hash

- **WHEN** a selected run contains LLM-produced findings carrying a `PromptVersion`
- **THEN** the header shows the run id, the run date, and the distinct prompt hash(es)

#### Scenario: Unbacked header fields are flagged, not fabricated

- **WHEN** the wireframe header lists an uploader and an LLM model that the findings response does not carry
- **THEN** those fields render a "not captured — follow-on" state rather than invented values

### Requirement: Four cited analysis sections

The detail panel SHALL render the four sections from the findings response — Analysis findings, Narrative,
Challenge, and Review — each finding carrying its confidence and its citation locator. The sections SHALL
reflect the latest run only (as the endpoint already returns). A known upload with no findings SHALL render
an explicit "not analyzed" state rather than an error.

#### Scenario: Sections render with citations

- **WHEN** a selected run has analysis, narrative, challenge, and review findings
- **THEN** all four sections render, each finding showing its confidence and cited source locator

#### Scenario: Known upload with no findings

- **WHEN** a selected upload exists but has no findings
- **THEN** the detail panel shows a "not analyzed yet" state, not an error

### Requirement: Score-audit section reuses the per-project health read

The detail panel SHALL render a score-audit section (US-10) by calling `GET /api/projects/{key}/health`
for each distinct project key present in the selected run, rendering that project's RAG bucket and its
applied overrides (rule, floor, reason, and the cited finding locator). This SHALL reuse the existing
health endpoint with no backend change. Because that endpoint always scores the project's latest run, the
section SHALL be labelled as the project's current health and SHALL carry a caveat that, for an upload
whose run is no longer the project's latest, a strict per-run historical audit is a follow-on. When a
project has no applied overrides, the section SHALL say the bucket was set by the raw score (no override
fired), not omit the project silently.

#### Scenario: Applied overrides render with their citations

- **WHEN** a project in the selected run has an applied override on its current health score
- **THEN** the score-audit section shows that project's bucket and the override's rule, floor, reason, and
  cited finding locator

#### Scenario: Current-audit caveat is shown

- **WHEN** the score-audit section renders from the per-project health read
- **THEN** it is labelled as the project's current health, with a caveat that a per-run historical audit is
  a follow-on

### Requirement: History detail is strictly read-only

The `/history` view SHALL remain strictly read-only: it SHALL NOT offer re-analyze, delete, edit, search,
or pagination controls. Any signal the current reads do not carry — uploader, project count, a multi-file
files summary, and a live Running/Failed status — SHALL render a flagged follow-on state and SHALL NOT be
fabricated; only a coarse Analyzed (findings present) vs Not-analyzed (empty) status is derivable.

#### Scenario: No mutation controls are present

- **WHEN** the history view renders
- **THEN** it presents no re-analyze, delete, edit, search, or pagination control

#### Scenario: Unbacked list/status fields are flagged

- **WHEN** the wireframe lists an uploader, a project count, a multi-file summary, or a Running/Failed status
  pill that the current reads do not carry
- **THEN** those render a flagged follow-on state, and status shows only the coarse Analyzed / Not-analyzed
  distinction that findings presence supports
