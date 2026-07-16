## ADDED Requirements

### Requirement: Decision agent emits decision-backlog findings

The pipeline SHALL include a deterministic Decision agent (no LLM) that reads the parsed decision records
for the project and emits an `Area == Decision` finding for each decision that is **overdue** (its
needed-by date has passed and its status is not "Approved") or **due soon** (needed-by within the near
window and not approved). Each finding SHALL be cited to its decision record and carry a severity by band —
overdue at Red, due-soon at Amber. An approved decision, or one with no needed-by date, SHALL produce no
finding. The agent SHALL run in the pipeline's parallel analysis stage alongside the other deterministic
agents.

#### Scenario: An overdue, unapproved decision is a cited Red finding

- **WHEN** a decision's needed-by date has passed and its status is not "Approved"
- **THEN** the Decision agent emits an `Area == Decision` finding at Red severity, cited to that decision record

#### Scenario: A due-soon decision is an Amber finding

- **WHEN** a decision's needed-by date is within the upcoming window and its status is not "Approved"
- **THEN** the Decision agent emits an `Area == Decision` finding at Amber severity, cited to that decision record

#### Scenario: An approved decision produces no finding

- **WHEN** a decision's status is "Approved"
- **THEN** the Decision agent emits no overdue/due-soon finding for it

### Requirement: Decisions are parsed into typed records

The Data Collector SHALL parse a `Decisions` sheet into typed decision records (project key, title, status,
owner, needed-by date, consequence), each with a source locator, and expose them on the collected data so
the Decision agent can read them. A workbook without a Decisions sheet SHALL yield no decision records
(not an error), consistent with the other optional sheets.

#### Scenario: A Decisions sheet parses into records

- **WHEN** an uploaded workbook contains a Decisions sheet with the expected columns
- **THEN** each row becomes a typed decision record with its status, needed-by date, owner, and a
  `Decisions!row` source locator

#### Scenario: A workbook with no Decisions sheet yields no decision records

- **WHEN** an uploaded workbook has no Decisions sheet
- **THEN** the collected data carries an empty set of decision records and analysis proceeds normally
