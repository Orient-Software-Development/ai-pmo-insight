## ADDED Requirements

### Requirement: Resource agent detects the project-manager role robustly

The Resource agent SHALL determine whether a project has a project manager by matching the assignment's
role against the manager concept in a way that recognises real data values (e.g. "Project Management",
"Project Manager", "PM"), not a brittle substring test that a valid value can fail. It SHALL emit a
"missing project manager" finding only when no assignment on the project fills the PM role.

#### Scenario: A "Project Management" role counts as a project manager

- **WHEN** a project has an assignment whose role is "Project Management"
- **THEN** the Resource agent does NOT emit a "no project manager" finding for that project

#### Scenario: A genuinely PM-less project is flagged

- **WHEN** a project has assignments but none fills a project-manager role
- **THEN** the Resource agent emits a "missing project manager" finding cited to the project's assignments

### Requirement: Resource agent flags cross-project key-person concentration

The Resource agent SHALL compute key-person concentration from the full set of assignments available to it
(all projects), counting the distinct projects each person is allocated to, and SHALL emit a concentration
finding for a person meeting the configured band (per the plan doc: 5+ projects Red, 3–4 Amber, fewer than 3
not flagged). The finding SHALL be attached to the project slice being analysed and cited to that project's
assignment for the person.

#### Scenario: A person on five projects is a Red concentration risk

- **WHEN** a person is allocated across five distinct projects in the portfolio
- **THEN** the Resource agent emits a key-person concentration finding at Red severity for that person, cited to the assignment

#### Scenario: A person on two projects is not flagged

- **WHEN** a person is allocated across only two projects
- **THEN** no key-person concentration finding is emitted for that person

### Requirement: Status agent reflects a milestone's recorded status

The Status agent SHALL take a milestone's recorded status into account: a milestone whose status indicates it
was missed or is at risk SHALL NOT be emitted as a Green informational "due soon" finding. A missed milestone
SHALL carry a Red-level Schedule severity and an at-risk milestone at least Amber, so the health Schedule area
can reflect it and the critical-milestone override can fire.

#### Scenario: A missed milestone is not rendered green

- **WHEN** a milestone's recorded status is "Missed"
- **THEN** the Status agent emits a Schedule finding at Red severity (not a Green "due soon"), cited to the milestone

#### Scenario: A normal upcoming milestone is still informational

- **WHEN** a milestone has no adverse status and its due date falls within the upcoming window
- **THEN** the Status agent emits the existing informational "due soon" finding

### Requirement: Decision agent emits decision-backlog findings

The pipeline SHALL include a deterministic Decision agent that reads parsed decision records for the project
and emits an `Area == Decision` finding for each decision that is overdue (past its needed-by date and not
approved) or due soon (needed-by within the near window). Each finding SHALL be cited to its decision record
and carry a severity reflecting overdue/blocking. No LLM is used.

#### Scenario: An overdue decision produces a cited Decision finding

- **WHEN** a decision's needed-by date has passed and its status is not "Approved"
- **THEN** the Decision agent emits an overdue Decision-area finding cited to that decision record

#### Scenario: An approved decision produces no backlog finding

- **WHEN** a decision is marked "Approved"
- **THEN** no overdue/due-soon finding is emitted for it

### Requirement: Numeric and structured findings carry their value as data

An agent SHALL carry a computed, dashboard-rendered quantity (e.g. the Financial agent's total exposure
amount, the Narrative agent's recommendation owner/deadline/action) in the structured finding metric, not
only inside the summary prose, so consumers read data rather than text.

#### Scenario: Financial exposure amount is on the finding metric

- **WHEN** the Financial agent computes a total exposure amount
- **THEN** the emitted finding carries that amount and its currency as a structured metric, in addition to the summary

#### Scenario: Narrative recommendation is structured

- **WHEN** the Narrative agent produces a recommendation with an owner, deadline, and action
- **THEN** those fields are carried as structured metric/metadata on the finding, not only flattened into the summary string
