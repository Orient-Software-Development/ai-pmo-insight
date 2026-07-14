## MODIFIED Requirements

### Requirement: Orchestrated 9-agent pipeline with a defined data flow

The system SHALL analyze **one or more stored uploads** through a single orchestrator that invokes nine agents as skills (`IAgentSkill<TInput, TOutput>`) in the Application layer — not as independent services. When multiple uploads are supplied, they SHALL be treated as a single analysis run producing one `RunId`. The uploads are expected to describe the same project, but this change does NOT enforce a same-project precondition at the endpoint boundary; the orchestrator's existing per-`projectKey` loop groups records regardless of how many projects the merged `CollectedData` spans (a same-project precondition is a possible follow-up refinement — see design Open Questions). The orchestrator SHALL follow the data flow: `#1 Data Collector → #2 Data Quality → parallel(#3 Status, #4 Risk & Issue, #5 Financial, #6 Resource) → merged findings → #7 Narrative → #8 Challenge → #9 Review`. It SHALL run agents sequentially where a dependency exists (#7→#8→#9) and in parallel where independent (#3–#6). All agent outputs SHALL be persisted.

#### Scenario: Orchestrator runs the agents in dependency order

- **WHEN** the orchestrator analyzes one or more uploads
- **THEN** Data Collector runs before Data Quality, the four analysis agents (#3–#6) run after Data Quality, and Narrative → Challenge → Review run after the findings are merged

#### Scenario: Independent analysis agents fan out

- **WHEN** the four analysis agents (#3 Status, #4 Risk & Issue, #5 Financial, #6 Resource) run
- **THEN** they execute independently over the shared record set without depending on each other's output

#### Scenario: Multi-upload run produces one RunId

- **WHEN** the orchestrator runs over three uploads in a single call
- **THEN** every finding produced by the run carries the same `RunId`, and the persisted analysis run records all three participating `uploadId`s

### Requirement: Data Collector parses uploads into typed records with source locators

The Data Collector (agent #1) SHALL be pure deterministic code (no LLM) that parses **one or more uploads** into typed records — `Project`, `Milestone`, `BudgetLine`, `Assignment`, `MinuteEntry`, `RaidItem` — using ClosedXML (Excel), `System.Xml` (Orbit XML), and OpenXml (`.docx` minutes). When multiple uploads are supplied, the Data Collector SHALL parse each independently and MERGE the resulting record sets into a single `CollectedData` before returning. Each typed record SHALL carry a source locator (e.g. sheet/row, XML path, or minutes date) **and the `uploadId` of the file it was parsed from**, so findings derived from it can cite their exact origin (file + locator). The merge SHALL NOT collapse records across files nor rewrite their originating `uploadId`. Typed records SHALL be transient analysis-run models and SHALL NOT be persisted. In this slice the parser targets the dummy Orbit-shaped fixtures; hardened real-Orbit parsing is out of scope.

#### Scenario: Single upload parsed into typed records

- **WHEN** the Data Collector parses a single dummy Orbit-shaped fixture
- **THEN** it produces typed records for the categories present in the fixture, each carrying a source locator and the fixture's `uploadId`

#### Scenario: Multiple uploads parsed and merged

- **WHEN** the Data Collector parses two uploads (e.g. an assignments file and an absenteeism file) in one run
- **THEN** the returned `CollectedData` contains the union of records from both files, and each record carries the `uploadId` of the file it was parsed from

#### Scenario: Records are not persisted

- **WHEN** an analysis run completes
- **THEN** only findings (and narrative/challenge/review outputs) are persisted; the intermediate typed records are not

## ADDED Requirements

### Requirement: Cross-file signals fire only when required categories are all present

Deterministic agents (#2 Data Quality, #3 Status, #5 Financial, #6 Resource) MAY produce findings that require records parsed from **more than one input file** in the same run — e.g. an assignments × absenteeism cross-check by employee, or a budget × timeline cross-check. Such a cross-file finding SHALL only be emitted when every required category is represented by at least one record in the merged `CollectedData`. When any required category is absent, the agent SHALL skip that specific rule silently (no synthetic warning) but MAY still emit its single-file findings from whatever records are present. The finding, when emitted, SHALL still cite exactly one source record via its `Citation`; supplementary evidence from the other file(s) MAY be included in the finding's `StructuredExcerpt` or `TextSnippet`.

#### Scenario: Assignments × absenteeism finding fires when both files are present

- **WHEN** a run's merged `CollectedData` contains both assignment records and absenteeism records for the same employee on the same project
- **THEN** the Resource agent produces a "key-person / overlap risk" finding for the affected employee, citing one of the source records

#### Scenario: Cross-file rule silent when one category is missing

- **WHEN** a run's merged `CollectedData` contains assignment records but no absenteeism records
- **THEN** the Resource agent produces its single-file findings (over-allocation, missing PM, capacity pressure) but does NOT emit the assignments × absenteeism cross-file finding, and produces no synthetic warning about the absent category

#### Scenario: Cross-file rule reproducible

- **WHEN** the same multi-file `CollectedData` is fed to the deterministic agent twice
- **THEN** the same cross-file findings are produced both times (no LLM, no randomness)
