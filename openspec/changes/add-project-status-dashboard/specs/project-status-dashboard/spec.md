## ADDED Requirements

### Requirement: Level-2 project status view consumes findings and health together

The system SHALL provide a Level-2 individual-project status view (React SPA, route `/projects`) that,
for a given project key, reads **both** the findings surface (`GET /api/projects/{projectKey}`) and the
health-score surface (`GET /api/projects/{projectKey}/health`) and renders them as a single project
status page. The view SHALL require authentication (it is a protected route; an unauthenticated visitor
is redirected to sign-in). The four existing cited sections — narrative, analytic findings, challenge,
and review — SHALL continue to render, each item citing its source, positioned below the health banner.

#### Scenario: Both surfaces are read for one project key

- **WHEN** an authenticated user opens the project status view for a project key that has been analyzed
- **THEN** the view issues a request to both the findings endpoint and the health endpoint for that key
- **AND** renders the health status banner above the narrative / findings / challenge / review sections

#### Scenario: Existing cited sections are preserved

- **WHEN** the project status view renders a project that has narrative, findings, challenge, and review
- **THEN** all four sections render as before, each item showing its citation (source locator / upload)

### Requirement: RAG status banner

When the health surface returns a score, the view SHALL display a status banner presenting the health
colour (`FinalBucket` — Red, Amber, or Green) and the raw numeric score (`RawScore`). The banner's colour
treatment SHALL reflect the `FinalBucket` value and SHALL be theme-aware (legible in light and dark
themes).

#### Scenario: Scored project shows its colour and score

- **WHEN** the health endpoint returns a score with `FinalBucket` = Amber and `RawScore` = 72
- **THEN** the banner presents an Amber status with the score 72 visible

#### Scenario: Overridden bucket shows the final colour

- **WHEN** the health endpoint returns `RawBucket` = Green but `FinalBucket` = Amber (an override fired)
- **THEN** the banner presents the Amber (final) colour, not the raw Green

### Requirement: Score audit surfacing

When a score is present, the view SHALL surface the auditable detail behind it: the per-area breakdown
(each area's health area name, its severity, its weight, and its weighted contribution), the aggregate
confidence, and the ordered list of applied overrides. Each applied override SHALL be shown with its rule
id, the floor it imposed, its human-readable reason, and the citation locator of the finding that tripped
it — so a user can see why the raw score was floored (PRD user story #10).

#### Scenario: Per-area breakdown is shown

- **WHEN** the score contains area contributions for Schedule, Budget, and Risk
- **THEN** the view lists each area with its severity, weight, and contribution

#### Scenario: Applied overrides show their reason and citation

- **WHEN** the score contains one applied override (e.g. "critical unmitigated risk → minimum Red")
- **THEN** the view shows that override's rule, imposed floor, reason, and the cited finding locator

#### Scenario: No overrides fired

- **WHEN** the score contains an empty applied-overrides list
- **THEN** the view shows no override entries (and `FinalBucket` equals `RawBucket`)

### Requirement: "Needs PM Review" flag is presented distinctly

When the score's `NeedsPmReview` flag is set, the view SHALL present it as a signal distinct from the RAG
colour, conveying that aggregate confidence is very low and the status needs a PM's judgement. The flag
SHALL be able to appear alongside any RAG colour (it is orthogonal to Red/Amber/Green).

#### Scenario: Needs-PM-Review shown on a Green project

- **WHEN** the score has `FinalBucket` = Green and `NeedsPmReview` = true
- **THEN** the view shows the Green banner AND a distinct "Needs PM Review" indicator

#### Scenario: Flag absent when confidence is adequate

- **WHEN** the score has `NeedsPmReview` = false
- **THEN** the view shows no "Needs PM Review" indicator

### Requirement: Defined rendering for every health-endpoint response

The view SHALL define a distinct rendering for each response the health endpoint can return for a project
key: a scored result, a findings-exist-but-nothing-scoreable result, and a not-found result. The view
SHALL NOT treat a null score or a 404 as an error state.

#### Scenario: Scored result

- **WHEN** the health endpoint returns 200 with a non-null score
- **THEN** the view renders the full banner, per-area breakdown, confidence, overrides, and PM-review flag

#### Scenario: Scoring pending

- **WHEN** the health endpoint returns 200 with a null score (the project has findings but none are
  scoreable yet)
- **THEN** the view renders a "scoring pending" state (findings exist but nothing scoreable yet) instead
  of a banner, and still renders the four finding sections

#### Scenario: Unknown project

- **WHEN** the health endpoint returns 404 (no findings on record for the key)
- **THEN** the view renders a "no such project / no findings on record" state rather than a banner or an
  error, consistent with the empty findings result for the same key

### Requirement: Presentation-only boundary

This view SHALL render only what the existing findings and health surfaces already carry. Where the PRD's
Level-2 wishlist calls for data the current finding shape does not carry — dated upcoming milestones
(next 2–4 weeks), per-decision owner/deadline/consequence, or an explicit AI recommendation — the view
SHALL render what is available and MAY note the gap, and SHALL NOT require any change to agents, prompts,
finding shape, or API contracts.

#### Scenario: Unmet PRD field is not fabricated

- **WHEN** the PRD asks for a dated upcoming-milestones list but findings carry only a Schedule severity
- **THEN** the view renders the available Schedule information and does not invent milestone dates
- **AND** no agent, prompt, finding-shape, or API contract change is introduced by this view
