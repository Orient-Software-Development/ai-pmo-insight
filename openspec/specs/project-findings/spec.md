# project-findings Specification

## Purpose

Trigger a stub analysis over a stored upload that emits findings, each carrying a citation to its
source; persist them grouped by an opaque `projectKey`; and expose a Level-2 read endpoint
returning a project's findings. The citation link is real from commit one; the analysis itself is
stubbed in this slice.

## Requirements

### Requirement: Analyze a stored upload into cited findings

The system SHALL expose `POST /api/analyze/{uploadId}` for an authenticated caller that reads the referenced upload and produces one or more findings. In this slice the analysis is a stub — it SHALL emit a hard-coded finding without any LLM or real parsing. The analysis SHALL run synchronously and be a separate step from upload (upload and analyze are distinct endpoints), so the asynchronous seam exists without requiring queue infrastructure.

#### Scenario: Analyze produces a finding

- **WHEN** an authenticated caller posts to `POST /api/analyze/{uploadId}` for a stored upload
- **THEN** the system produces at least one finding grouped under a `projectKey` and persists it

#### Scenario: Analyze an unknown upload

- **WHEN** an authenticated caller posts to `POST /api/analyze/{uploadId}` for an upload that does not exist
- **THEN** the system responds `404 Not Found` and persists no finding

### Requirement: Every finding cites its source

The system SHALL record, for every finding it produces, a citation back to the source it was derived from. The citation MUST include the originating `uploadId` and a locator within that source. A finding MUST NOT be persisted without a citation.

#### Scenario: Finding carries a citation to its upload

- **WHEN** the stub analysis emits a finding from an upload
- **THEN** the persisted finding includes a citation whose `uploadId` matches the analyzed upload and a non-empty locator

### Requirement: Findings are grouped by an opaque project key

The system SHALL group findings by an opaque `projectKey` string and SHALL NOT persist a separate project entity in this slice. The `projectKey` is a label on the finding; when real Orbit data is fed later, that key becomes the Orbit project id without a structural change.

#### Scenario: Findings share a project key

- **WHEN** analysis emits findings for the same source project
- **THEN** the persisted findings carry the same `projectKey` value

### Requirement: Read a project's findings (Level 2)

The system SHALL expose `GET /api/projects/{projectKey}` for an authenticated caller that returns the findings recorded for that project key, each with its citation. This is the Level-2 (individual project status) read path.

#### Scenario: Return findings for a known project key

- **WHEN** an authenticated caller requests `GET /api/projects/{projectKey}` for a key that has findings
- **THEN** the system responds `200 OK` with the list of findings for that key, each including its citation

#### Scenario: Project key with no findings

- **WHEN** an authenticated caller requests `GET /api/projects/{projectKey}` for a key that has no findings
- **THEN** the system responds `200 OK` with an empty list

#### Scenario: Unauthenticated read is rejected

- **WHEN** an unauthenticated caller requests `GET /api/projects/{projectKey}`
- **THEN** the system responds `401 Unauthorized`
