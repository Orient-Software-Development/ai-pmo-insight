Project Plan Description
Scope with the company
Check what they do and use today, and ideally, we have the following in scope:
Data
Project Identifier (ideally project type, if multiple types of projects exists)
Project Scope
Project Timeline
Project Budget
Project Resources (ideally per scope item)
Project Time Used (ideally per resources)
Project Risks
Project Issues
Project Decisions
Minutes from project meetings (unstructured data is expected)
Output
Requirements
Target: Executive, Project Management, Other?
Resource need: NextWave only + client

System Landscape and Data Source OverviewCan we get data examples, e.g., 3 months back?Resource need: NextWave only + client

Proof of Concept Scope ++ (Might be combined with #4)Scope within #1 data sources.Success Criteria (Minimum Viable, Good, Fantastic):“We do not just automate status reporting. We turn fragmented project data into portfolio-level management insight, decision support, and early-warning signals.”
Examples:
Time Saved  Reduce PMO reporting time by 50–90%
Status Quality  Leadership says reports are more useful
Risk Detection  AI finds risks not clearly visible in status reports
Data Quality  AI identifies missing/inconsistent project data
Trust  Project Managers agree with 80%+ of AI-generated conclusions
Actionability  Every red/amber project has a clear recommended actionResource need: NextWave only + client

Kick Off meetingOnsite Kick Off and Workshop to clarify what needs to be clarified.Resource need: NextWave + AI Solution Architect (1 day that is included prep and follow up)

Health status logic definition (rules) (Might be combined with #4)Example: Budget  Green when forecast maximum +5%, Amber >5 <15%, Red >15%Needed for all health parameters – see #1Resource need: NextWave only + client

KPI’s suggestion
Data Quality:
KPI
What it tells you
Data completeness score
Is the project status based on enough data?
Last update age
How many days since project data was updated?
Missing KPI count
Which mandatory fields are missing?
Source consistency score
Do budget, time, resources and project status agree?
Confidence level
How much should we trust the AI-generated status?
Example output:
“Project Alpha is marked Amber, but confidence is Low because budget forecast is missing and the risk log has not been updated for 24 days.”

Project Health:
Schedule / MS Project
KPI
Formula / logic
Milestone adherence
Completed milestones / planned milestones
Schedule variance
Planned date vs current forecast date
Delay severity
Number of delayed critical milestones
Upcoming milestone risk
Milestones due in next 2–4 weeks with risk/blockers
Dependency risk
Open dependencies that may affect timeline
Budget
KPI
Formula / logic
Budget variance
Actual cost vs baseline budget
Forecast variance
Forecast cost vs approved budget
Burn rate
Spend or hours used per week/month
Budget consumption vs progress
% budget used vs % project completed
Financial exposure
Expected overrun amount
This one is very powerful:
Project progress
Budget used
Signal
40% complete
70% budget used
High financial risk
80% complete
60% budget used
Healthy or under-spending
50% complete
50% budget used
On track
Resource
KPI
What it tells you
Resource allocation variance
Planned vs actual allocation
Capacity pressure
People/roles above healthy utilization
Missing critical roles
Roles needed but not assigned
Time burn variance
Planned hours vs actual hours
Role-level bottleneck
Which capability is constraining delivery

Scope
KPI
What it tells you
Scope change count
Number of approved/pending changes
Unapproved scope items
Potential scope creep
Change request value
Commercial impact of scope changes
Scope stability score
Whether scope is controlled or drifting
Open scope decisions
Decisions needed to clarify delivery

Risk and issue
KPI
What it tells you
Open high risks
Current serious risks
Risk trend
Increasing, stable, decreasing
Unmitigated risk count
Risks without mitigation plan
Issue age
How long blockers have been open
Escalation need
Issues requiring management attention

Decision
KPI
What it tells you
Decisions overdue
Management decisions not made on time
Decisions due soon
Decisions needed in next 1–2 weeks
Blocked by decision count
Work blocked by missing decisions
Decision impact
Budget, scope, schedule, client impact

Budget: Green when forecast maximum +5%, Amber >5 <15%, Red >15%
Project Allocation: Red if no allocation is found, Green if found
Project Resources: Red - Resource allocated 5 or more projects, Amber 3-4, Green <3
Project Time usage: Lack of time used per allocation based on allocation of activities
Project Resources: Absence, e.g. holiday or illness – clarify if possible
Activity: No moving forward / very slow, medium, okay?
Risks: Increasing Amber, no risks red, all risks have mitigation plan?
Identified issues: Growing path and mitigation
Project Health Scoring
EXAMPLE!
Area
Weight
Schedule
20%
Budget
20%
Scope
15%
Resources
15%
Risks/issues
15%
Decisions/dependencies
10%
Data quality
5%
Then convert the score to legend:
Score
Status
80–100
Green
60–79
Amber
0–59
Red
With overriding based on rules (still example):
override rule:
Condition
Result
Critical milestone missed
Minimum Amber
Forecast overrun >15%
Minimum Amber or Red
Critical unmitigated risk
Minimum Red
Key decision overdue and blocking work
Minimum Amber
Data confidence very low
Status marked “Needs PM Review”


Setup environment + Identify AI Agents requiredCreate the client environment and plan the AI Agents.Assumption but not decided:
#
Name
Purpose
1
Data Collector Agent
Pulls/imports data from systems and files.
2
Data Quality Agent
Checks missing data, inconsistent project IDs, old updates.
3
Status Analyst Agent
Calculates project health and detects deviations.
4
Risk & Issue Agent
Reviews RAID logs, meeting notes, unresolved blockers.
5
Financial Analyst Agent
Reviews budget, actuals, forecast and burn rate.
6
Resource Agent
Checks staffing, overload, missing capabilities.
7
Narrative Agent
Writes the project and portfolio status.
8
Challenge Agent
Questions weak conclusions and flags unsupported claims.
9
Review Agent
Prepares questions for the “responsible” before publishing.
Resources: AI Solution Architect / Agent Designer

Identify AI Skills
What to do?
Resources: AI Prompt / Skill Designer

Output format / Dashboard Design.Prepare output definitions
Level 1: Executive Portfolio Summary
Overall portfolio health  Number of Green/Amber/Red projects
Projects needing intervention  Top projects with reason
Financial exposure  Budget overruns and forecast risk
Resource bottlenecks  Skills/roles causing delivery pressure
Decision backlog  Decisions blocking progress
Client/commercial risk  Projects at risk of damaging commitments
Recommended actions  What leadership should do next
Level 2: Individual Project Status
Overall status  Green/Amber/Red including explanation
This period progress  What moved forward
Key deviations  Budget, time, scope, risks, resources
Risks and issues  Top items needing attention
Upcoming milestones  Next 2–4 weeks
Decisions needed  Owner, deadline, consequence
AI recommendation  Practical next action
Confidence level  High/medium/low based on data quality
Level 3: Data QualityExamples:
Project A: No risk update in 21 days
Project B: Budget actuals missing
Project C: Milestone dates not updated
Project D: Resource plan does not match time entries
Resources: Dashboard Designer

Build the Prototype
Create data / input templates (per data source)
Create output templates (per level)
Collect Sample Data / Real Project Data
Create data model (ideally only capture findings, not client data)
Create AI Skills + AI Agents
Create Dashboard / Reports
Resources: Next Wave + AI Prompt / Skill / Agent Designer + Dashboard Designer + Data Governance Specialist + Data Model Builder

Run POC with manual data input
Feed data into the POC manually.
Verify data output is correct, and AI agents work as planned.
Verify reports / dashboard.
Verify data security.
Verify that the AI creates a better project status than current human process.
Resources: NextWave / AI Solution Architect for Unit Test. Client: Solution Test.

Automated data loadClarify technology and build data load.Test load + error handling.
Resources: NextWave + Data Engineer / Integration Specialist

Go-liveHand over login to AI environment and/or user id + password to Dashboard that pull data from the AI environment.
Resources: NextWave

Hypercare + Success Criteria Evaluation
Check-in status + Support + Calculation of effectiveness.
Resources: NextWave

Enhancement Options (Out of Scope in POC)
Orchestration Layer
Documentation
Create Library and in-house usage (project type = Advisory, Implementation, AI PMO, AI Friction …)
Lessons Learned forwarded to NextWave
Lessons Learned from other customers to client (from NextWave)
Machine Learning over time
Resources: NextWave + AI Solution Architect for suggestions.

Resources needed and initial assumptions
AI Solution Architect – 3-4 days of work
Agent Designer – 3-5 days of work
AI Prompt / AI Designer 5-7 days of work
Data Model Builder 3-5 days of work
Dashboard Designer 3-5 days of work
Data Governance Specialist 1-2 days of work
Approx 25 days?!?



