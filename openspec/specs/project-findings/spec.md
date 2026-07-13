# project-findings Specification

## Purpose

Analyze a stored upload through the 9-agent `analysis-pipeline` into cited findings plus a
narrative, a challenge critique, and a review; persist them grouped by an opaque `projectKey` and
identified by analysis run; and expose a Level-2 read endpoint returning a project's full analysis.
Every finding carries a citation to its source, and re-analysis appends a new run rather than
overwriting prior findings.

## Requirements

### Requirement: Analyze a stored upload into cited findings

The system SHALL expose `POST /api/analyze/{uploadId}` for an authenticated caller that reads the referenced upload and produces cited findings, a narrative, a challenge critique, and a review by driving the `analysis-pipeline` (the orchestrated 9-agent pipeline). The analysis SHALL NOT be a hard-coded stub. It SHALL run synchronously and remain a separate step from upload (distinct endpoints), preserving the asynchronous seam without queue infrastructure. Findings SHALL be grouped under a `projectKey` derived from the parsed source, falling back to a deterministic key when the source carries no identifiable project.

#### Scenario: Analyze produces pipeline outputs

- **WHEN** an authenticated caller posts to `POST /api/analyze/{uploadId}` for a stored upload
- **THEN** the system runs the pipeline and persists findings (each cited), plus the narrative, challenge, and review outputs, grouped under a `projectKey`

#### Scenario: Analyze an unknown upload

- **WHEN** an authenticated caller posts to `POST /api/analyze/{uploadId}` for an upload that does not exist
- **THEN** the system responds `404 Not Found` and persists nothing

#### Scenario: Unauthenticated analyze is rejected

- **WHEN** an unauthenticated caller posts to `POST /api/analyze/{uploadId}`
- **THEN** the system responds `401 Unauthorized` and persists nothing

### Requirement: Every finding cites its source

The system SHALL record, for every finding it produces, a citation back to the source it was derived from. The citation MUST include the originating `uploadId` and a locator within that source. A finding MUST NOT be persisted without a citation.

#### Scenario: Finding carries a citation to its upload

- **WHEN** analysis emits a finding from an upload
- **THEN** the persisted finding includes a citation whose `uploadId` matches the analyzed upload and a non-empty locator

### Requirement: Findings are grouped by an opaque project key

The system SHALL group findings by an opaque `projectKey` string and SHALL NOT persist a separate project entity in this slice. The `projectKey` is a label on the finding; when real Orbit data is fed later, that key becomes the Orbit project id without a structural change.

#### Scenario: Findings share a project key

- **WHEN** analysis emits findings for the same source project
- **THEN** the persisted findings carry the same `projectKey` value

### Requirement: Read a project's findings (Level 2)

The system SHALL expose `GET /api/projects/{projectKey}` for an authenticated caller that returns the analysis outputs recorded for that project key: the findings (each with its citation), the narrative + recommendation, the challenge critique, and the review's anticipated questions. This is the Level-2 (individual project status) read path.

#### Scenario: Return the full analysis for a known project key

- **WHEN** an authenticated caller requests `GET /api/projects/{projectKey}` for a key that has been analyzed
- **THEN** the system responds `200 OK` with the findings (each including its citation), the narrative, the challenge output, and the review output

#### Scenario: Project key with no findings

- **WHEN** an authenticated caller requests `GET /api/projects/{projectKey}` for a key that has not been analyzed
- **THEN** the system responds `200 OK` with empty sections

#### Scenario: Unauthenticated read is rejected

- **WHEN** an unauthenticated caller requests `GET /api/projects/{projectKey}`
- **THEN** the system responds `401 Unauthorized`

### Requirement: Findings record provenance and version by analysis run

Every persisted finding SHALL record the agent that produced it, a confidence value, and — for LLM-produced findings — the prompt version (content hash of the prompt) used. Each analysis run SHALL be identified by a `RunId`; re-analyzing the same upload SHALL append a new run's outputs under a new `RunId` rather than overwriting or silently duplicating prior findings.

#### Scenario: Finding carries producing agent, confidence, and prompt version

- **WHEN** the pipeline persists a finding
- **THEN** the record includes the producing agent, a confidence value, and (for LLM agents) the prompt version that produced it

#### Scenario: Re-analysis appends a new run

- **WHEN** the same upload is analyzed a second time
- **THEN** the new outputs are persisted under a new `RunId` and the prior run's findings are retained

### Requirement: Extended citation shape

A finding's `Citation` SHALL retain the mandatory `UploadId` and `Locator` and MAY additionally carry a structured excerpt (e.g. sheet/row/column or field path) and a text snippet, both nullable, so a reader can see the exact evidence a finding rests on.

#### Scenario: Citation carries a structured excerpt or snippet

- **WHEN** an agent derives a finding from a specific parsed cell or a passage of minutes
- **THEN** the citation may include a structured excerpt and/or a text snippet in addition to the upload id and locator
