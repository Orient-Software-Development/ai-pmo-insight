# L1 Executive Portfolio — Follow-on Register

Captures the gap between what the **plan doc** (`inputs/Project Plan with Activity Descriptions.md`)
specifies for **Level 1 — Executive Portfolio Summary** and what the L1 view + `GET /api/portfolio`
roll-up (`ScorePortfolio.cs`) actually back today. Written from an explore-mode walkthrough so a future
OpenSpec change can be scoped without re-deriving the analysis.

> **Legend** — ✅ backed & live · 🟢 buildable now (doc formula + fixture data both exist) · 🟠 buildable
> but needs an internal shape/contract change · 🔵 genuinely needs client input at kick-off.
>
> **Revision (verified against `AnalysisContext.cs` + the agents):** an earlier draft called #4
> key-person "architectural — needs a cross-project pass". That was wrong: `ProjectSlice.Data` is the
> **full `CollectedData`** (its own doc comment: *"Agents filter `Data` to `ProjectKey`"*), so every agent
> already holds the whole portfolio's data and just filters it. #4 is buildable now (🟢). #6 also softened
> — a data-backed "customer exposure" proxy is buildable; only the *true* commercial signal needs the
> client. Same lesson as the L3 pass: deferred design decisions were mislabelled as harder-than-they-are.

## The plan doc's L1 spec (7 panels)

The doc (lines 235–242) lists seven L1 panels. Current status:

| # | Panel (doc) | Status | Where it stands |
|---|-------------|:---:|-----------------|
| 1 | Overall portfolio health (G/A/R count) | ✅ | `ScorePortfolio` counts each `FinalBucket`; L1 renders the bar + legend |
| 2 | Projects needing intervention (top + reason) | ✅ | Worst-first Red/Amber list with a cited reason |
| 3 | Financial exposure (€) | 🟢 | Computed already, trapped in prose — see below |
| 4 | Resource bottlenecks / key-person | 🟢 | **Corrected** — cross-project data already in `slice.Data`; agent just filters it away — see below |
| 5 | Decision backlog | 🟢 | Doc formula + fixture data both exist; pure plumbing — see below |
| 6 | Client / commercial risk | 🟠/🔵 | **Split** — a data-backed customer-exposure proxy is buildable; the true commercial signal needs the client — see below |
| 7 | Recommended actions (owner/deadline/confidence) | 🟠 | Not a formula — a structured-output contract (internal decision) — see below |

> ➕ The current L1 view also shows **Confidence (avg)** + **"Needs PM Review" count** as a headline
> cell. These are backed but **not in the doc's L1 list** — arguably a Data-Lead/L3 concern, not an
> executive headline. Candidate to demote in favour of #3 Financial exposure.

---

## Cross-cutting blocker: `HealthArea` is missing two of the doc's seven scoring areas

`Domain/Findings/HealthArea.cs` defines **5** areas: `Schedule, Budget, Risk, Resource, DataQuality`.
The doc's health-scoring table (lines 156–171) weights **7** areas:

| Doc area | Weight | `HealthArea` bucket? |
|----------|:---:|:---:|
| Schedule | 20% | ✅ Schedule |
| Budget | 20% | ✅ Budget |
| **Scope** | **15%** | ❌ **missing** |
| Resources | 15% | ✅ Resource |
| Risks / issues | 15% | ✅ Risk |
| **Decisions / dependencies** | **10%** | ❌ **missing** |
| Data quality | 5% | ✅ DataQuality |

**25% of the doc's scoring weight has no home in the model.** Any Decision work (#5) is blocked on
adding `HealthArea.Decision` first, or its findings can't be scored. Scope (15%) is likewise absent —
noted here for completeness though it is not one of the four items below.

---

## #5 — Decision backlog ✅ agent LANDED (`add-decisions-agent`, #45+#47) · roll-up = slice E

> **Update:** the Decision agent shipped — `HealthArea.Decision` + weight (10, EXAMPLE), `DecisionRecord`
> parsing, `DecisionSkill` (overdue→Red / due-soon→Amber, cited), and the `key-decision-overdue` override.
> The missed D-1002-1 OVERDUE signal is now produced. The L1 **backlog count** roll-up remains slice E of
> `add-l1-portfolio-signals` (gated). Original rationale below.

**Doc formula** — Decision KPI table (lines 134–144) + override (line 191):
- **Overdue** — decision past `needed_by` and not made (status ≠ Approved).
- **Due soon** — `needed_by` within the next 1–2 weeks.
- **Blocked-by count** — work blocked by a missing decision.
- **Decision impact** — budget / scope / schedule / client impact.
- Override: *"Key decision overdue and blocking work → minimum Amber."*

**Data exists** — `docs/samples/decisions.csv` already carries every field the formula needs:
```
project_id, decision_id, title, status, owner, raised_date, needed_by, consequence_if_delayed
D-1002-1, ..., status=Overdue, needed_by=2026-06-20, owner=Steering Committee,
          consequence="Cutover cannot be scheduled; team idle; client SLA at risk"
```
> ⚠️ **This is the D-1002-1 OVERDUE signal currently missed by the analysis** — the data is present,
> nothing reads it. `decisions.xlsx` is parsed by nothing today (staged; `samples/README.md`), the LLM
> minutes extraction pulls only Risks/Issues (`MinuteRiskExtraction`), and there is no Decision agent.

**Build shape** — mirrors the existing RAID → Risk pattern exactly, no new architecture:
1. **Parse** — add a `DecisionRecord` typed record + `Decisions` sheet handling in `ExcelProjectParser`.
2. **Enum** — add `HealthArea.Decision` (blocker above).
3. **Agent** — a deterministic `DecisionSkill`: overdue / due-soon bands, each finding citing its
   `decisions` row; RAG severity from overdue+blocking.
4. **Scoring** — add the Decisions 10% weight to `HealthScoringOptions` (still an EXAMPLE number).
5. **Roll-up** — `ScorePortfolio` can then surface a portfolio decision-backlog count.

---

## #4 — Resource bottleneck / key-person 🟢 (doc's most-cited rule; buildable now)

**Doc formula** — resource rules (lines 147–150) + PRD US-7:
- **Concentration** — *"Resource allocated 5+ projects = Red, 3–4 = Amber, <3 = Green"* (line 148).
  This is `count(distinct projects per person)` — exact and deterministic.
- **Allocation** — *"Red if no allocation found, Green if found"* (line 147).
- **Absence** — *"holiday or illness — clarify if possible"* (line 150). Not locked in the doc, but the
  fixture already models it.
- **Combine (US-7)** — concentration × absence. **The doc gives no weighting formula** — how to combine
  is a design decision we make (e.g. absent + 5 projects → Red; absent + 3–4 → Amber).

**Data exists**:
- Concentration — `resources.csv`, group by `resource_name` across all rows → **Anna Berg = 5 projects**
  (ORB-1001/1002/1004/1005/1006).
- Absence — `time-used.csv`, `entry_type="Blocked day"`, notes `"Holiday 2026-06-16 to 2026-06-27"`.

**Correction — NOT architectural.** An earlier draft said `ResourceSkill` can *"never"* count a person
across projects because it runs per-project. That is **wrong**: `ProjectSlice.Data` is the **full
`CollectedData`** — its doc comment says *"Agents filter `Data` to `ProjectKey`"*, and both `ResourceSkill`
and `DataQualitySkill` already read `slice.Data.Assignments` / `slice.Data.Projects` **unfiltered** (they
filter inline by key). So the whole portfolio's assignments are already in hand in every slice. Counting
`slice.Data.Assignments.GroupBy(person).Count(distinct ProjectKey)` needs **no new architecture**.

**Build shape** (buildable now):
1. In `ResourceSkill`, compute concentration from the **unfiltered** `slice.Data.Assignments` (not the
   per-project filtered list) → apply the 5+/3–4/<3 band.
2. Read absence from `time-used.csv` (`entry_type=Blocked day`).
3. **Decide** the combine rule (design choice — record it when chosen).
4. **Decide** dedup/attach: the concentration finding is a property of the *person*, but a person shows up
   in several project slices — attach it to each project they're on (correct: it's a risk on each), or emit
   once portfolio-wide via `ScorePortfolio`. A dedup decision, not a blocker.

> 🔴 **Separate one-line bug to fix regardless**: `ResourceSkill.cs:60` checks
> `Role.Contains("Manager")`, but the fixture role is `"Project Management"` — `"Management"` does not
> contain the substring `"Manager"`, so it emits a **false** *"No Project Manager is assigned"* on
> projects that clearly have one (Anna Berg, PMP). Fix the match (e.g. `"Manage"`, or a role allow-list).

---

## #7 — Recommended actions (owner / deadline / confidence) 🟠 (a contract, not a formula)

**No formula — and correctly so.** This is an LLM synthesis job, not deterministic math. But the doc /
PRD specify the **output shape**:
- PRD US-4 — each action names an **owner, deadline, confidence**.
- Doc L2 (line 250) — *"AI recommendation → practical next action."*
- Success criterion #6 — *"every red/amber project has a clear recommended action."*

**Current state.** The Narrative agent already writes a recommendation, but as a **prose blob** — e.g.
*"Recommendation (Sponsoring Executive / PMO Director, by Within 24 hours): …"*. The owner/deadline are
embedded in text, not structured, which is why it reads awkwardly.

**Build shape** — a **structured recommendation contract** instead of prose:
```
{ action, owner, deadline, confidence }
```
- `owner` — derivable from decision owners / resource assignments.
- `deadline` — derivable from milestone or decision `needed_by`.
- **Depends on #5** — decision data is the natural source of owner + deadline, so #5 should land first.

---

## #6 — Client / commercial risk 🟠/🔵 (split: a proxy we can build vs the true signal we can't)

The doc gives **one line** (line 241): *"Projects at risk of damaging commitments."* No KPI table, no
rule, no threshold — the single most underspecified L1 panel. But "can't build it" was too strong. Split:

**🟠 Customer-exposure proxy — buildable now (a decision we make).** The `customer` field is on every
project (`projects.csv`), and we already compute health. So we can group **at-risk projects (Red/Amber) by
customer** → *"which client relationships have exposed projects"*. This is a re-projection of existing,
backed data — no new signal, no fabrication. It must be **clearly labelled** as relationship-exposure, not
contract-risk. This answers a useful slice of the panel honestly.

**🔵 True commercial risk — genuinely needs the client.** The doc's intent — *damaging commitments* — means
contractual/commercial damage: contract value at risk, margin erosion, SLA-penalty exposure. **None of that
is in the data** — `budget.csv` is internal cost/forecast, not contract value or margin; the only SLA hint
is free-text `"client SLA at risk"` inside one decision's consequence. This part stays a kick-off question
(logged in `prds/poc-ai-pmo-insight.md` → *Open questions*): what signal defines commercial risk, and does
Orbit (or another system) carry contract/margin/SLA data?

**Honest interim**: ship the customer-exposure proxy (labelled), keep the true commercial-risk panel a
flagged placeholder — never fabricate a commercial-risk number.

---

## Suggested sequence

| Order | Item | Rationale |
|:---:|------|-----------|
| 1 | **#4 "no PM" bug fix** | One line; removes a trust-killing falsehood on a project that clearly has a PM |
| 2 | **#4 Key-person concentration** | 🟢 Doc's most-cited rule; data already in `slice.Data`; read it unfiltered + decide the combine/dedup rule |
| 3 | **#5 Decisions** | Formula + data both exist; copy the RAID pattern; unblocks `HealthArea.Decision`; recovers the missed D-1002-1 overdue signal |
| 4 | **#3 Financial exposure roll-up** | Rides the shared `Finding` metric field (also fixes L3 Age); surfaces the €80k already computed |
| 5 | **#7 Actions** | Structured-output contract; depends on #5 for owner/deadline source |
| 6 | **#6 customer-exposure proxy** | 🟠 Re-project at-risk projects by customer (labelled); backed by existing data |
| — | **#6 true commercial risk** | 🔵 Deferred to kick-off — needs a contract/margin/SLA signal not in the data |
