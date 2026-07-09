# orbit-ingest Specification

## Purpose

Accept an uploaded Orbit-shaped fixture file, store its raw bytes, and return an upload reference
that later analysis can resolve. The parser is stubbed in this slice — uploaded content is treated
as opaque bytes.

## Requirements

### Requirement: Upload an Orbit-shaped fixture file

The system SHALL accept an uploaded file via `POST /api/ingest/upload` from an authenticated caller, store its raw bytes, and return an upload reference that later analysis can resolve. The uploaded content SHALL be treated as opaque bytes in this slice — the system SHALL NOT parse or interpret it.

#### Scenario: Successful upload returns a reference

- **WHEN** an authenticated caller uploads a fixture file to `POST /api/ingest/upload`
- **THEN** the system stores the raw bytes and responds `201 Created` with an `uploadId` and the original file name

#### Scenario: Empty upload is rejected

- **WHEN** an authenticated caller posts a request with no file content
- **THEN** the system responds `400 Bad Request` and stores nothing

#### Scenario: Unauthenticated upload is rejected

- **WHEN** an unauthenticated caller posts to `POST /api/ingest/upload`
- **THEN** the system responds `401 Unauthorized` and stores nothing

### Requirement: A stored upload can be retrieved by reference

The system SHALL make a stored upload's raw bytes retrievable by its `uploadId` so the analysis step can read the source it will cite.

#### Scenario: Resolve an existing upload

- **WHEN** analysis requests the upload for a known `uploadId`
- **THEN** the system returns the stored raw bytes and file name for that upload

#### Scenario: Unknown upload reference

- **WHEN** analysis requests the upload for an `uploadId` that does not exist
- **THEN** the system reports the upload as not found
