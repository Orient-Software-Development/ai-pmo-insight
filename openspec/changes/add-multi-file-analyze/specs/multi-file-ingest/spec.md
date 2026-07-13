## ADDED Requirements

### Requirement: Batch upload of multiple files in one request

The system SHALL expose a batch ingest endpoint that accepts multiple files in one HTTP request and returns one `uploadId` per stored file. Each file SHALL be stored under the same rules as the existing single-file endpoint (opaque bytes, filename, timestamp) and become an independently retrievable `Upload`. Batch upload SHALL be atomic **per file**, not across files — a corrupt or empty file in the batch SHALL fail only that file with a per-file error and MUST NOT prevent the other files from being stored. The existing single-file endpoint (`POST /api/ingest/upload`) SHALL remain available and unchanged.

#### Scenario: Multiple files stored in one call

- **WHEN** an authenticated caller posts six files (Budget, Hours, Assignments, Absenteeism, RAID, Minutes) to the batch endpoint
- **THEN** the response contains six entries, each with a distinct `uploadId` and the original `fileName`, and every file is retrievable by its `uploadId`

#### Scenario: One bad file does not fail the whole batch

- **WHEN** a batch upload contains five valid files and one empty file
- **THEN** the response reports five successful stores and one per-file error, and the five valid uploads are persisted

#### Scenario: Single-file endpoint keeps working

- **WHEN** a caller posts to the existing `POST /api/ingest/upload` with one file
- **THEN** the response shape and semantics are unchanged from before this change

### Requirement: Analyze a set of uploads as one run

The system SHALL expose a multi-file analyze endpoint that accepts a set of `uploadId`s and drives the 9-agent pipeline over them **as a single analysis run** with one `RunId`. The endpoint SHALL fail with 400 when the set is empty or contains fewer than one recognisable `uploadId`, and with 404 when any referenced `uploadId` cannot be resolved (all-or-nothing at the endpoint boundary — a partial run is not permitted). Findings produced by the run SHALL each cite the specific `uploadId` the source record was parsed from, not the run as a whole. The existing single-uploadId endpoint (`POST /api/analyze/{uploadId}`) SHALL remain available and unchanged.

#### Scenario: Set of uploads produces one analysis run

- **WHEN** an authenticated caller posts `{"uploadIds": ["a", "b", "c"]}` to the multi-file analyze endpoint
- **THEN** the response carries a single `RunId` and a list of findings whose citations reference `a`, `b`, or `c` as appropriate for each finding's source record

#### Scenario: Unknown uploadId in the set is a 404

- **WHEN** the request references any `uploadId` that does not resolve to a stored upload
- **THEN** the endpoint responds `404 Not Found` and no findings are persisted for the run

#### Scenario: Empty set is a 400

- **WHEN** the request body contains an empty `uploadIds` array
- **THEN** the endpoint responds `400 Bad Request` and no run is created

#### Scenario: Single-uploadId endpoint keeps working

- **WHEN** a caller posts to the existing `POST /api/analyze/{uploadId}` for a stored upload
- **THEN** the response shape and semantics are unchanged from before this change (one upload analysed, findings cite that upload)

### Requirement: Per-file provenance preserved through the merge

The system SHALL preserve, for every parsed record inside a multi-file run, the `uploadId` of the file it came from, so that each finding derived from that record cites the correct originating file. The merge into a single `CollectedData` SHALL NOT collapse records across files nor rewrite their originating `uploadId` to some run-level or "primary" upload. When two records with structurally identical content originate from different files, both SHALL survive the merge; the deterministic agents decide downstream whether to treat them as duplicates.

#### Scenario: Finding cites the file it came from

- **WHEN** a run analyses two files, and a Status finding is derived from a milestone record parsed out of the timeline file
- **THEN** the finding's `Citation.UploadId` equals the timeline file's `uploadId`, not the other file's

#### Scenario: Records from different files coexist post-merge

- **WHEN** an assignment record for the same employee exists in both the assignments file and (implausibly) the absenteeism file
- **THEN** both records are present in the merged `CollectedData` (deduplication, if any, is an agent-level decision, not a merge-level one)
