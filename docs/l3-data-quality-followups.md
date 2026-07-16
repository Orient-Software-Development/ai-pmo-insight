# L3 Data Quality — Follow-on Register

Captures the gap between what the **plan doc** (`inputs/Project Plan with Activity Descriptions.md`)
specifies for **Level 3 — Data Quality** and what the L3 view (`DataQuality.jsx`) + the Data Quality
agent (#2) + the `SummarizeDataQuality` roll-up actually back today. Companion to
[`l1-executive-portfolio-followups.md`](l1-executive-portfolio-followups.md) and
[`l2-project-status-followups.md`](l2-project-status-followups.md).

> **Legend** — ✅ backed & live · 🟢 can do now (data + logic exist, add a check) · 🟠 can do, needs an
> internal design decision + build (no client input) · 🔵 genuinely needs client input at kick-off.
>
> **Revision (verified against `ConfidencePolicy.cs`):** an earlier draft of this register put four items
> under "cannot do — needs design/decision or client input". On inspection, **none are truly
> client-blocked** — they were our own deferred design decisions mislabelled as client blockers. The old
> 🔴 section has been rewritten below as *"Needs a decision we can make ourselves"* with the chosen
> approach locked in for each. Only **one** genuine client question remains (does real Orbit data have
> duplicates + which fields are mandatory), and both ship fine as `EXAMPLE` defaults.

## The framing that matters

L3 is **not empty** — the machinery runs and cites correctly. But the Data Quality agent
(`DataQualitySkill.cs`) catches a **generic** set (missing fields, project staleness, orphan references),
which is a **different set** from the doc's four *named* examples (per-risk staleness, budget-actuals
missing, milestone-not-updated, resource-plan-vs-time). The shape is right; the specific checks the doc
calls out are mostly not the ones implemented.

## The doc's L3 spec

### Four named examples (lines 252–256)

| Doc example | Status | Why |
|-------------|:---:|-----|
| "No risk update in **21 days**" | 🟢 | Agent checks **project** last-updated at **30 days**, not **per-risk** at 21. Data exists (`risks.csv last_updated`; README seeded rows >21d old) — needs a per-risk staleness check |
| "Budget actuals missing" | 🟠 | No budget-completeness check exists; fixture also has no missing actuals to catch (needs test data) |
| "Milestone dates not updated" | ⚠️ | Agent catches a **missing** due date, not a **stale / not-updated** one |
| "Resource plan ≠ time entries" | 🟢 | Only orphan detection today; the real cross-source check isn't built. Data exists (`resources` occupancy vs `time-used` hours) |

### DQ KPI table (lines 36–50)

| KPI | Status |
|-----|:---:|
| Data completeness score | ⚠️ `MissingFieldCount` computed as a signal, not surfaced as a score |
| Last update age | ✅ computed + shown |
| Missing KPI count | ⚠️ partial |
| Source consistency score | ⚠️ only orphan detection, not budget/time/resource agreement |
| Confidence level | ✅ backed (the confidence hero) |

## What the DQ agent actually emits today (and it works)

`DataQualitySkill.cs` — all cited, High confidence:
- ✅ Missing fields (project name, percent-complete, last-updated)
- ✅ Milestone missing a due date
- ✅ Project staleness > 30 days (`StaleThresholdDays`)
- ✅ Orphan reference (child row referencing an unknown project id → inconsistent)

Plus a `DataQualitySignal` (`MissingFieldCount`, `LastUpdateAgeDays`, `SourceConsistent`) that downstream
agents turn into their findings' confidence via `ConfidencePolicy`.

## The L3 view panels (issue #35 wishlist)

| Panel | Status |
|-------|:---:|
| Confidence hero (mean + publish threshold `ConfidenceFloor` + below-target) | ✅ backed |
| Items table — Project · Issue · Severity · Citation | ✅ backed |
| Items table — **Age** column | 🟠 age computed but trapped in the summary string |
| Items table — **Suggested remediation** | 🟢 static rule-map — decide + build now (see below) |
| Items table — **Lift** ordering | 🟠 arithmetic over `ConfidencePolicy` — medium (see below) |
| **Areas completeness grid** (8 categories) | 🟠 present/expected metric + `EXAMPLE` mandatory list (see below) |
| **Duplicate identity candidates** + Merge/Keep-separate | 🟢 heuristic we design + we seed the fixture (see below) |

## The can-do / cannot-do split

```
   CAN DO NOW (add a check):        per-risk staleness,  resource-vs-time consistency,
                                    suggested remediation (static map),  duplicate identity (US-2)
   CAN DO (needs a decision + build): Age column (Finding-shape),  budget-actuals check,
                                    lift ordering (ConfidencePolicy arithmetic),  areas grid
   GENUINELY NEEDS CLIENT (kick-off): only "is duplication real in Orbit + which fields mandatory"
                                    — and both ship as EXAMPLE defaults, so nothing is truly blocked
```

---

## 🟢 CAN DO NOW — data + logic exist, add a check

### Per-risk staleness ("no risk update in N days") — matches doc example #1
- **Data**: `risks.csv` carries a per-row `last_updated`; `samples/README.md` deliberately seeds some
  risk/issue rows >21 days old to exercise this rule.
- **Gap**: `DataQualitySkill` only checks the **project** `LastUpdated` at a **30-day** threshold. It never
  looks at per-risk `last_updated`.
- **Build**: add a per-risk (and per-issue) staleness check emitting a DQ finding per stale item. The
  threshold N (doc says 21) is an EXAMPLE number → confirm with the PMO at kick-off.

### Resource-plan vs time-entries consistency — the doc's "source consistency score"
- **Data**: `resources.csv` (occupancy / allocation) + `time-used.csv` (registered hours) both present.
- **Gap**: the agent's only consistency check is **orphan detection** (unknown project id). It does not
  compare allocation against actual hours.
- **Build**: add a cross-source check — flag where a resource is allocated but has no/low time entries, or
  vice-versa. This is the doc KPI *"do budget, time, resources and project status agree?"*.

---

## 🟠 CAN DO — needs a `Finding`-shape change (internal)

### Age column (L3) — rides the shared Finding-metric change (see below)
The staleness age **is computed** (`LastUpdateAgeDays`) and even appears in the summary text (*"stale…
N days ago"*), but there is no structured numeric field on `Finding` to render it as a column. Needs the
shared metric-field change below.

### Budget-actuals-missing check
A simple completeness check to add to the DQ agent. Note the fixture has no missing actuals, so building
this also needs a deterministic test fixture with a gap (mirrors how
`OrbitFixtureBuilder.WorkbookWithDataQualityGap` seeds a milestone-with-no-due-date).

---

## Needs a decision we can make ourselves (was mislabelled 🔴 "cannot do")

All four items below were previously flagged as blocked on "design/decision or client input". On
inspection none are truly client-blocked — each is a design decision the build team can make, and three
of four are buildable now. The chosen approach is locked in here.

### 🟢 Suggested remediation — static rule-map (decide + build now)

The DQ checks are a **finite, known set** (missing name / missing % / missing due-date / stale / orphan),
and the agent already knows the check type when it builds the summary. Attach a remediation from a
lookup keyed on that type:

| Check type | Remediation (deterministic) |
|-----------|------------------------------|
| missing due date | "Set a due date for milestone X in Orbit" |
| stale (N days) | "Update the project status in Orbit (last touched N days ago)" |
| orphan reference | "Reconcile: row cites a project id not in the Projects sheet" |
| missing %-complete | "Enter percent-complete for the project" |
| missing name / last-updated | "Fill the missing field in the Projects sheet" |

- **No LLM** — deterministic, testable, no hallucination, no cost; the repo's established agent pattern.
- **We decide the map**; client input = zero. Rides the shared `Finding` metadata change (remediation is
  another field).
- **Effort: low.** LLM-generated remediation stays a later option for fuzzy (minutes-extracted) findings
  only.

### 🟠 Confidence-lift ordering — arithmetic over `ConfidencePolicy`, not a model

`ConfidencePolicy.FromSignals` is a **pure function**: confidence = a level knocked down by
`MissingFieldCount` (>2 → −2, >0 → −1), staleness (>90d → −2, >30d → −1), and `!SourceConsistent` (−1).
So lift is a **counterfactual re-evaluation**, not an ML model:

```
   For each DQ item:  lift = confidence(signal) − confidence(signal with this item fixed)
   e.g. fix the orphan → SourceConsistent flips true → +1 level → Low → Medium
```

- **No new model** — re-evaluate the existing function with one signal decremented.
- The **quantization is a feature**: it honestly reports *"fixing this one won't move the needle — you
  need 3 to cross the threshold"* when `MissingFieldCount` stays >2. Matches the doc's *"lift confidence
  back above the target threshold"*.
- **Caveat**: lift is **per-project** (confidence is per-project). A portfolio-wide lift ranking needs a
  small aggregation decision.
- **Effort: medium**, gated on nothing external. (Earlier draft called this "genuinely hard" — that was
  wrong; verified against `ConfidencePolicy.cs`.)

### 🟠 Areas completeness grid (8 categories) — present/expected + EXAMPLE mandatory list

- **Metric**: completeness = `fields present / fields expected` per category. The doc KPI *"missing KPI
  count — which mandatory fields are missing"* already implies a mandatory-field checklist. **Seed
  sensible defaults marked `EXAMPLE`** — same pattern as the scoring weights (ship defaults, PMO tunes at
  kick-off). Not a blocker.
- **The 8 columns** map to the **input categories** (Schedule / Budget / Scope / Resources / Risks /
  Decisions / Minutes / Time), **not** the 5 `HealthArea` buckets — so the grid is over data categories,
  which sidesteps part of the enum debate. (Areas that also need *scoring* still need the enum work — see
  the L1 register.)
- **Defer to kick-off**: *which* fields are truly mandatory — shipped as EXAMPLE defaults, so not blocked.
- **Effort: medium.**

### 🟢 Duplicate identity candidates (US-2) — heuristic we design + fixture we seed

The old blocker was "no test data" — but **we own the fixtures**. The wireframe gives the pattern (Titan
Rollout / Titan Rollout Phase 1: shared sponsor, WBS overlap, time logged against both IDs).

```
   1. Heuristic (we design):  score project pairs on
        name similarity  +  same customer  +  resource overlap  +  WBS overlap
   2. Test data (we add):     seed ORB-XXXX + ORB-XXXXa as a known duplicate pair
   3. Emit:                   a "duplicate candidate" finding (confidence-scored)
   4. UI (US-2):              candidates table + Merge / Keep-separate that RECORDS the
                              decision — NEVER auto-merges (US-2 never-silently-merge)
```

- **We decide** the heuristic + threshold; **we add** the test fixture — fully buildable/demoable now.
- **Genuine client question (deferred, not blocking)**: *does real Orbit data actually have duplicates?*
  (already a PRD open question) — a "is it worth it" question, not a "can we build it" one. Ships fine on
  seeded data for the POC demo.
- **Effort: medium-high**, but it's the item **most tied to a named user story (US-2)** → highest demo
  value.

> The plan doc's agent #2 spec is *"checks missing data, inconsistent project IDs, old updates"* and PRD
> **US-2** is the duplicate-merge confirmation flow. Duplicate detection is the L3 piece most tied to a
> named user story — and, contrary to the earlier draft, it is buildable now on seeded data.

### 🔵 The one genuine client question

Two sub-questions genuinely want the client, and both ship as `EXAMPLE` defaults so neither blocks a
build: (1) *is duplication a real problem in Orbit data* (worth-it, not can-we); (2) *which fields are
truly mandatory* for the completeness grid. Logged for kick-off alongside the scoring-weights question.

---

## 🔗 Cross-cutting: `Finding` has no structured metric field (affects L1 + L3)

Every number the agents compute is baked into the `Summary` **string** because the `Finding` aggregate
(`Domain/Findings/Finding.cs`) carries only `Summary / Area / Severity / Confidence / Citation` — no typed
numeric or metadata field.

```
   L1 #3  Financial exposure €80,000   → trapped in "...exposure is 80,000."   (FinancialSkill)
   L3     Staleness age (N days)        → trapped in "...stale... N days ago."  (DataQualitySkill)
   L3     Confidence lift               → not computed at all
```

**One `Finding`-shape change** (add optional typed metric fields — e.g. `MetricValue` / `MetricUnit`, or a
small key/value bag) unlocks **L1 financial exposure roll-up + L3 Age column together**, and gives the
lift model somewhere to live later. This is the **highest-leverage single change across all three
dashboards** — do it once, three panels benefit.

> Trade-off to weigh when scoping: a typed metric bag vs. re-parsing numbers out of summary strings. The
> string parse is fragile (format drift breaks it silently); the shape change is a migration but durable.

---

## Suggested sequence for L3

| Order | Item | Rationale |
|:---:|------|-----------|
| 1 | **Per-risk staleness** | 🟢 data exists; matches doc example #1; small check |
| 2 | **Suggested remediation** | 🟢 static rule-map; rides the shared `Finding` metadata change; kills the biggest flagged placeholder for near-zero risk |
| 3 | **Resource-vs-time consistency** | 🟢 data exists; the doc's "source consistency score" KPI |
| 4 | **Finding metric field → Age column** | 🟠 shared change; also unlocks L1 € exposure |
| 5 | **Duplicate identity (US-2)** | 🟢 buildable now (heuristic + seed a fixture); highest demo value — named user story |
| 6 | **Confidence-lift ordering** | 🟠 arithmetic over `ConfidencePolicy`; decide per-project vs portfolio aggregation |
| 7 | **Areas completeness grid** | 🟠 present/expected + `EXAMPLE` mandatory list; partly rides the enum work |

## Status across the three dashboards (index)

| Dashboard | Register |
|-----------|----------|
| L1 Executive Portfolio | [`l1-executive-portfolio-followups.md`](l1-executive-portfolio-followups.md) |
| L2 Individual Project Status | [`l2-project-status-followups.md`](l2-project-status-followups.md) |
| L3 Data Quality | this document |

**Shared threads** running through all three: the `HealthArea` **enum gap** (Scope 15% + Decisions 10%
have no bucket), the **Decisions** build (serves L1 backlog + L2 decisions-needed), the **structured
recommendation** contract (L1 + L2), and the **`Finding` metric field** (L1 € + L3 Age).
