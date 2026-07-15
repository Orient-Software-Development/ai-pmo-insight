## ADDED Requirements

### Requirement: Dedicated upload / analyze cold-start page

The system SHALL provide a dedicated ingest page (React SPA, route `/upload`) that hosts the upload →
analyze flow, extracted out of the Level-2 project view. The page SHALL require authentication (protected
route; an unauthenticated visitor is redirected to sign-in) and SHALL be the destination a user lands on
after login. From this page a user SHALL be able to select a project export, upload it
(`POST /api/ingest/upload`), and run analysis on the resulting upload (`POST /api/analyze/{uploadId}`)
without first visiting the project view.

#### Scenario: Cold-start upload and analyze from the dedicated page

- **WHEN** an authenticated user on `/upload` selects an accepted file and runs the flow
- **THEN** the page uploads the file and runs analysis against the returned upload id
- **AND** on success offers a way to view the analyzed project's results (it does not require the user to
  re-enter the flow from the project view)

#### Scenario: Post-login lands on the upload page

- **WHEN** a user completes sign-in
- **THEN** the application navigates to `/upload`

#### Scenario: Upload page requires authentication

- **WHEN** an unauthenticated visitor navigates to `/upload`
- **THEN** they are redirected to the sign-in page rather than shown the upload form

### Requirement: Accepted formats and CSV rejection preserved

The upload page SHALL accept only `.xlsx`, `.xlsm`, `.xml`, and `.docx` files and SHALL reject a `.csv`
selection up front with a clear message, matching the existing ingest behaviour — a CSV parses to nothing,
so it is refused before upload rather than yielding a silent empty result.

#### Scenario: CSV is rejected before upload

- **WHEN** the user selects a `.csv` file on the upload page
- **THEN** the page shows an "unsupported file type — CSV is not supported" message and does not upload it
- **AND** the user can re-pick an accepted file without reloading

#### Scenario: Accepted formats are advertised

- **WHEN** the upload page renders its file control
- **THEN** it communicates the accepted formats (`.xlsx`, `.xlsm`, `.xml`, `.docx`)

### Requirement: Parse results and analysis-pipeline presentation reflect the API

When a file has been uploaded and analysis run, the page SHALL present the outcome the existing endpoints
return — an upload result and the analysis result — including a coarse pipeline status reflecting the
request lifecycle (uploading, analyzing, done, failed). The page SHALL NOT present per-file parse status,
duplicate-identity detection, or live per-agent progress as though the API returned them.

#### Scenario: Pipeline status reflects the request lifecycle

- **WHEN** the user runs upload → analyze
- **THEN** the page shows progress moving through uploading → analyzing → done
- **AND** on a failed request shows a failed state with the error, not a fabricated success

### Requirement: Presentation-only boundary on the upload page

The upload page SHALL render only what the existing ingest and analyze surfaces already return. Where the
wireframe's `upload` page shows data the current API does not carry — per-file parse status / notes,
duplicate-identity merge confirmation (US-2), or a live nine-agent progress stepper (US-9) — the page SHALL
render an explicit "not yet captured — follow-on" placeholder and SHALL NOT require any change to ingest,
analysis, agents, prompts, finding shape, or API contracts.

#### Scenario: Unbacked panel is a labelled placeholder, not fabricated

- **WHEN** the wireframe calls for a per-file parse-results table or a live agent stepper but the API
  returns only an upload id and a completed analysis result
- **THEN** the page renders the available result and shows the richer panels as a labelled follow-on
  placeholder
- **AND** no ingest, analysis, agent, prompt, finding-shape, or API contract change is introduced
