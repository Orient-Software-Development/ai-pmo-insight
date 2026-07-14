# upload-history Specification

## Purpose

Expose a read-only surface over the uploads and analysis runs already persisted by the ingest and
`analysis-pipeline` slices: list past uploads and read an upload's latest analysis. Visibility is
shared-workspace — any authenticated caller sees every upload (no per-user scoping). The history is
derived from existing data (uploads plus the `runId`/`createdAt` on findings); it introduces no new
persistence and is strictly view-only (no re-analyze, delete, search, or pagination).

## Requirements

### Requirement: List uploads

The system SHALL expose an authenticated endpoint that returns all uploads, ordered most-recently-
uploaded first. Each entry SHALL include the upload id, the original file name, and the upload
timestamp. Raw file content SHALL NOT be included. Any authenticated caller SHALL see every upload
regardless of who uploaded it (shared-workspace visibility). The endpoint SHALL require
authentication; an unauthenticated caller SHALL receive 401.

#### Scenario: Uploads returned newest first

- **WHEN** an authenticated caller requests the upload list and three uploads exist
- **THEN** the response is 200 with all three entries ordered by upload timestamp descending
- **AND** each entry contains id, file name, and uploaded-at, but not the file content

#### Scenario: Empty list when nothing uploaded

- **WHEN** an authenticated caller requests the upload list and no uploads exist
- **THEN** the response is 200 with an empty list

#### Scenario: Unauthenticated caller is rejected

- **WHEN** an unauthenticated caller requests the upload list
- **THEN** the response is 401

### Requirement: Read an upload's latest analysis

The system SHALL expose an authenticated endpoint that returns the findings produced by an upload's
most recent analysis run, identified by the upload id. When an upload has been analyzed more than
once, the endpoint SHALL return only the findings of the latest run (the run whose findings have the
most recent creation time); earlier runs SHALL remain persisted but SHALL NOT appear in the
response. The findings SHALL be partitioned into the same four sections as the existing project view:
analysis findings, narrative, challenge, and review. Each finding SHALL carry its citation and
provenance (producing agent, run id, prompt version, confidence, created-at).

#### Scenario: Latest run's findings returned in four sections

- **WHEN** an authenticated caller requests findings for an upload that has been analyzed
- **THEN** the response is 200 with the latest run's findings grouped into analysis, narrative,
  challenge, and review sections

#### Scenario: Only the latest run is shown when re-analyzed

- **WHEN** an upload has been analyzed twice, producing two runs
- **THEN** the response contains only the findings whose run is the most recent
- **AND** findings from the earlier run are absent

#### Scenario: Analyzed-but-no-findings and never-analyzed both return empty sections

- **WHEN** an authenticated caller requests findings for a known upload that has no findings
- **THEN** the response is 200 with all four sections empty

#### Scenario: Unknown upload returns 404

- **WHEN** an authenticated caller requests findings for an upload id that does not exist
- **THEN** the response is 404

#### Scenario: Unauthenticated caller is rejected

- **WHEN** an unauthenticated caller requests findings for any upload
- **THEN** the response is 401
