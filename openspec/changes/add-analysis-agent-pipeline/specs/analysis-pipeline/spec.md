## ADDED Requirements

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

### Requirement: Deterministic data-quality and analysis agents

Agents #2 Data Quality, #3 Status, #5 Financial, and #6 Resource SHALL be pure deterministic code (no LLM). Data Quality SHALL detect missing fields, stale updates, and inconsistent IDs, emit DQ findings, and produce a confidence signal consumed by downstream agents. Status SHALL compute milestone adherence, schedule variance, delay severity, and upcoming/dependency risk. Financial SHALL compute budget/forecast variance, burn rate, budget-vs-progress cross-signal, and financial exposure. Resource SHALL compute allocation variance, capacity pressure, missing roles, and concentration × absence.

#### Scenario: Data Quality emits findings and a confidence signal

- **WHEN** Data Quality runs over the typed records
- **THEN** it emits DQ findings for missing/stale/inconsistent data and yields a confidence signal available to the analysis agents

#### Scenario: Analysis agents emit cited findings deterministically

- **WHEN** the Status, Financial, or Resource agent runs over the same records twice
- **THEN** it produces the same findings, each citing the record(s) it was derived from, without any LLM call

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

### Requirement: Narrative synthesis (hybrid: template-first, LLM fallback)

The Narrative agent (#7) SHALL synthesize the merged findings into prose describing overall status and a recommendation that names an owner, a deadline, and a rationale. It SHALL be hybrid: recurring narrative shapes (single-signal RED, two-signal RED with a clear primary/secondary, data-quality-driven "Needs PM Review", routine GREEN) SHALL be rendered deterministically from templates, and only cases that do not fit a template (multi-signal cross-referencing, minute-extracted signals) SHALL fall back to the `ILlmClient`. The narrative SHALL be persisted and returned with the project's findings regardless of which path produced it.

#### Scenario: Template path renders a recurring shape without the LLM

- **WHEN** the merged findings match a recurring narrative shape (e.g. a single dominant RED signal)
- **THEN** the Narrative agent renders the status and recommendation from a template, with no LLM call, still naming an owner, deadline, and rationale

#### Scenario: Complex case falls back to the LLM

- **WHEN** the merged findings do not fit a template (e.g. multiple cross-referencing signals or a minute-extracted signal)
- **THEN** the Narrative agent produces the prose and recommendation via the `ILlmClient`, persisted for the project

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
