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

> The Decision agent, the structured `Finding` metric, and the structured Narrative recommendation moved to
> the shared tickets — #47 (Decisions parse + `DecisionSkill`), #46 (Finding metric field), #48 (structured
> recommendation). This change consumes them; it does not define them.
