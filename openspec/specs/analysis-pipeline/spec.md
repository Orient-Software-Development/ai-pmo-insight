# analysis-pipeline Specification

## Purpose

Analyze a stored upload through a single orchestrator that runs nine agents as skills
(`IAgentSkill<TInput, TOutput>`) in the Application layer: a deterministic data + analysis layer
(#1 Data Collector, #2 Data Quality, #3 Status, #5 Financial, #6 Resource) and a trust layer
(#4 Risk & Issue, #7 Narrative, #8 Challenge, #9 Review) behind a provider-agnostic `ILlmClient`
port. The pipeline produces cited findings plus a narrative, challenge critique, and review, with
provenance threaded from the Data Collector's record locators through every stage. In this slice a
`FakeLlmClient` stands in for the vendor adapter so the pipeline runs end-to-end with no API key.

## Requirements

### Requirement: Orchestrated 9-agent pipeline with a defined data flow

The system SHALL analyze a stored upload through a single orchestrator that invokes nine agents as skills (`IAgentSkill<TInput, TOutput>`) in the Application layer — not as independent services. The orchestrator SHALL follow the data flow: `#1 Data Collector → #2 Data Quality → parallel(#3 Status, #4 Risk & Issue, #5 Financial, #6 Resource) → merged findings → #7 Narrative → #8 Challenge → #9 Review`. It SHALL run agents sequentially where a dependency exists (#7→#8→#9) and in parallel where independent (#3–#6). All agent outputs SHALL be persisted.

#### Scenario: Orchestrator runs the agents in dependency order

- **WHEN** the orchestrator analyzes an upload
- **THEN** Data Collector runs before Data Quality, the four analysis agents (#3–#6) run after Data Quality, and Narrative → Challenge → Review run after the findings are merged

#### Scenario: Independent analysis agents fan out

- **WHEN** the four analysis agents (#3 Status, #4 Risk & Issue, #5 Financial, #6 Resource) run
- **THEN** they execute independently over the shared record set without depending on each other's output

### Requirement: Data Collector parses uploads into typed records with source locators

The Data Collector (agent #1) SHALL be pure deterministic code (no LLM) that parses an upload into typed records — `Project`, `Milestone`, `BudgetLine`, `Assignment`, `MinuteEntry`, `RaidItem` — using OpenXml/EPPlus (Excel), `System.Xml` (Orbit XML), and OpenXml (`.docx` minutes). Each typed record SHALL carry a source locator (e.g. sheet/row, XML path, or minutes date) so findings derived from it can cite their origin. Typed records SHALL be transient analysis-run models and SHALL NOT be persisted. In this slice the parser targets the dummy Orbit-shaped fixtures; hardened real-Orbit parsing is out of scope.

#### Scenario: Upload parsed into typed records

- **WHEN** the Data Collector parses a dummy Orbit-shaped fixture
- **THEN** it produces typed records for the categories present in the fixture, each carrying a source locator

#### Scenario: Records are not persisted

- **WHEN** an analysis run completes
- **THEN** only findings (and narrative/challenge/review outputs) are persisted; the intermediate typed records are not

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

### Requirement: Deterministic data-quality and analysis agents

Agents #2 Data Quality, #3 Status, #5 Financial, and #6 Resource SHALL be pure deterministic code (no LLM). Data Quality SHALL detect missing fields, stale updates, and inconsistent IDs, emit DQ findings, and produce a confidence signal consumed by downstream agents. Status SHALL compute milestone adherence, schedule variance, delay severity, and upcoming/dependency risk. Financial SHALL compute budget/forecast variance, burn rate, budget-vs-progress cross-signal, and financial exposure. Resource SHALL compute allocation variance, capacity pressure, missing roles, and concentration × absence.

#### Scenario: Data Quality emits findings and a confidence signal

- **WHEN** Data Quality runs over the typed records
- **THEN** it emits DQ findings for missing/stale/inconsistent data and yields a confidence signal available to the analysis agents

#### Scenario: Analysis agents emit cited findings deterministically

- **WHEN** the Status, Financial, or Resource agent runs over the same records twice
- **THEN** it produces the same findings, each citing the record(s) it was derived from, without any LLM call

### Requirement: Deterministic agents emit structured area and severity

The deterministic agents (#2 Data Quality, #3 Status, #5 Financial, #6 Resource) SHALL stamp a structured
health `Area` and `Severity` (Red/Amber/Green) onto every `Analysis` finding they produce, surfacing the
severity signal they already compute internally (e.g. budget overrun band, days-late band, allocation
concentration) rather than encoding it only in the free-text summary. An agent MAY produce findings in more
than one area; `Area` SHALL be set per finding, not per agent. Emitting the structured fields SHALL NOT
change which findings an agent produces nor their summaries — it is additive provenance.

#### Scenario: Financial agent stamps Budget area and a severity

- **WHEN** the Financial agent (#5) emits a forecast-overrun finding
- **THEN** the finding carries `Area = Budget` and a `Severity` reflecting the overrun band, in addition to
  its existing summary and citation

#### Scenario: One agent can emit multiple areas

- **WHEN** an agent produces findings that concern different health areas in the same run
- **THEN** each finding carries its own `Area`, independent of the producing agent's identity

#### Scenario: Enrichment is additive

- **WHEN** the agents run over a fixture that previously produced N analysis findings
- **THEN** the same N findings are produced with the same summaries, each now additionally carrying an
  `Area` and `Severity`

### Requirement: Provider-agnostic LLM client port with structured output

The system SHALL define an `ILlmClient` port in Application that abstracts the LLM runtime; the concrete runtime SHALL be selected only at the Infrastructure boundary and SHALL NOT leak into Application or Domain. Every LLM call SHALL request structured JSON output (tool-use / `response_format`) against a declared contract — the system SHALL NOT parse free text. In this slice the only registered implementation SHALL be a `FakeLlmClient` returning fixture responses; no real vendor adapter is included.

#### Scenario: LLM calls use structured output

- **WHEN** an LLM-backed agent invokes the port
- **THEN** it requests output shaped to a declared JSON contract rather than free text

#### Scenario: Fake client drives the pipeline without an API key

- **WHEN** the pipeline runs with `FakeLlmClient` registered
- **THEN** agents #4 (minutes), #7, #8, and #9 produce shaped outputs from fixture responses with no network call or API key

### Requirement: Risk & Issue agent combines deterministic RAID with LLM minutes extraction

The Risk & Issue agent (#4) SHALL be hybrid: it SHALL filter the deterministic RAID records in code, and it SHALL extract risks and issues from unstructured meeting-minute content via the `ILlmClient` (fake in this slice). The LLM path SHALL be invoked **only when the upload contains meeting-minute content** — when no minutes are present, #4 SHALL run purely deterministically and make no LLM call. Findings from either path SHALL carry a citation to their source record or minutes locator.

#### Scenario: Risks extracted from meeting minutes

- **WHEN** an upload contains meeting-minute content
- **THEN** the Risk & Issue agent produces risk findings from that text via the LLM port, each citing the minutes locator

#### Scenario: RAID findings produced without the LLM

- **WHEN** the upload contains structured RAID records
- **THEN** the Risk & Issue agent produces findings from them deterministically, independent of the LLM path

#### Scenario: No minutes means no LLM call

- **WHEN** the upload contains no meeting-minute content
- **THEN** the Risk & Issue agent produces only its deterministic RAID findings and makes no LLM call

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

### Requirement: Narrative synthesis (hybrid: template-first, LLM fallback)

The Narrative agent (#7) SHALL synthesize the merged findings into prose describing overall status and a recommendation that names an owner, a deadline, and a rationale. It SHALL be hybrid: recurring narrative shapes (single-signal RED, two-signal RED with a clear primary/secondary, data-quality-driven "Needs PM Review", routine GREEN) SHALL be rendered deterministically from templates, and only cases that do not fit a template (multi-signal cross-referencing, minute-extracted signals) SHALL fall back to the `ILlmClient`. The narrative SHALL be persisted and returned with the project's findings regardless of which path produced it.

#### Scenario: Template path renders a recurring shape without the LLM

- **WHEN** the merged findings match a recurring narrative shape (e.g. a single dominant RED signal)
- **THEN** the Narrative agent renders the status and recommendation from a template, with no LLM call, still naming an owner, deadline, and rationale

#### Scenario: Complex case falls back to the LLM

- **WHEN** the merged findings do not fit a template (e.g. multiple cross-referencing signals or a minute-extracted signal)
- **THEN** the Narrative agent produces the prose and recommendation via the `ILlmClient`, persisted for the project

### Requirement: Narrative recommendation is carried as structured data

The Narrative agent's finding SHALL carry its recommendation as structured detail — `owner`, `deadline`,
`action`, and `rationale` — on the finding's metric detail, in addition to the existing prose summary, so a
consumer can read the fields as data rather than parsing the summary string. The prose summary SHALL be
preserved for back-compat. This SHALL apply to both the template-produced and LLM-produced narrative paths,
and SHALL require no change to the narrative prompt or the LLM output contract.

#### Scenario: The narrative finding exposes the recommendation fields

- **WHEN** the Narrative agent produces a recommendation (via either the template or the LLM path)
- **THEN** the finding carries `owner`, `deadline`, `action`, and `rationale` as structured detail, and
  still carries the human-readable summary

#### Scenario: Structured detail matches the recommendation the summary describes

- **WHEN** the narrative finding is produced
- **THEN** the `owner` / `deadline` / `action` / `rationale` in the structured detail are the same values
  rendered in the summary prose (not a separate or fabricated set)

### Requirement: Adversarial Challenge critique

The Challenge agent (#8, LLM hybrid) SHALL produce an adversarial critique of the findings and the narrative — weak claims, unsupported numbers, alternative interpretations, and missing caveats — augmented by deterministic checks for broken evidence links and stale data. It SHALL read the narrative (#7) and the findings. Its output SHALL be persisted and retrievable alongside the findings. Challenge SHALL NOT delete findings.

#### Scenario: Challenge critiques the findings and narrative

- **WHEN** the Challenge agent runs after the narrative
- **THEN** it produces a critique referencing specific findings/claims, which is persisted alongside them, and no finding is removed

### Requirement: Review anticipates stakeholder questions by audience

The Review agent (#9, LLM hybrid) SHALL predict the questions stakeholders will ask, grouped by audience (executive, sponsor, data lead, peer PM). It SHALL read the narrative (#7), the Challenge output (#8), and the findings. Its output SHALL be persisted and retrievable alongside the findings. Review SHALL NOT be a keep/drop gate over findings.

#### Scenario: Review produces audience-grouped questions

- **WHEN** the Review agent runs after Challenge
- **THEN** it produces anticipated questions grouped by audience, persisted alongside the findings, without dropping any finding

### Requirement: Citations and provenance propagate through every stage

Every finding the pipeline produces SHALL carry a citation back to the source it was derived from, threaded from the Data Collector's record locators through every downstream agent. A finding reaching persistence without a citation SHALL be rejected (`Finding.Create` enforces this). Each finding SHALL also record its producing agent and prompt version (for LLM agents).

#### Scenario: A math finding cites its parsed record

- **WHEN** the Financial agent emits a variance finding from a `BudgetLine` record
- **THEN** the finding's citation resolves to the upload and the locator of that budget line

#### Scenario: Uncited finding is rejected

- **WHEN** any agent would emit a finding with no citation
- **THEN** the system does not persist that finding

### Requirement: Deterministic behavior under test

The system SHALL allow the full pipeline to run with `FakeLlmClient` so the orchestrator's control flow and citation propagation can be asserted without a live LLM, and the deterministic agents (#1, #2, #3, #5, #6) SHALL be unit-testable directly without any LLM. Tests SHALL NOT assert live LLM output content.

#### Scenario: Pipeline tested end-to-end with the fake client

- **WHEN** the pipeline runs under test with `FakeLlmClient`
- **THEN** tests can assert which agents ran, that findings carry citations and provenance, and that narrative/challenge/review outputs are produced — with no network call
