# POC data rules — Provisional v0

> **POC placeholders — not client-agreed.** These are our *starting* numbers/formulas, to walk through
> with the PMO and replace with real values. Companion to [kickoff-questions.md](kickoff-questions.md).
>
> Anchored to the agreed steering (2026-07): **balanced, leaning Budget + Risk** · **strong override
> floors** · **Scope display-only (not scored)**.
>
> Status column: **shipped** = already in code today; **proposed** = written here, code matches;
> change points noted per section (config = `appsettings.json → HealthScoring`; code = the named agent).

---

## 1. Health scoring model

### 1.1 Area weights (sum = 100) — *config, shipped* (matches `appsettings.json → HealthScoring:Weights`)
| Area | Weight | Note |
|------|:--:|------|
| Schedule | 20 | |
| Budget | **25** | joint-highest |
| Risk | **25** | joint-highest |
| Resource | 15 | |
| **Decision** | **10** | overdue / blocking decisions (added by #45) |
| Data Quality | 5 | |
| Scope | — | display-only, **not scored** |

### 1.2 Severity → score — *config, shipped*
| Green | Amber | Red |
|:--:|:--:|:--:|
| 100 | 70 | 30 |

### 1.3 RAG bands (weighted score, inclusive lower bound) — *config, shipped*
| Band | Rule |
|------|------|
| 🟢 Green | score ≥ 80 |
| 🟡 Amber | 60 ≤ score < 80 |
| 🔴 Red | score < 60 |

### 1.4 Confidence / "Needs PM Review" — *config, shipped*
- Confidence scores: Low 30 · Medium 70 · High 100.
- Aggregate confidence **< 50** → flag **"Needs PM Review"** (orthogonal to the RAG colour).

---

## 2. Override "worst-case floors" (strong) — *config, shipped*

Applied after the weighted average; the most severe floor wins; a floor never *improves* the colour.

| Rule | Trigger | Floor |
|------|---------|:--:|
| `critical-milestone-missed` | any **Schedule** finding Red | min **Amber** |
| `forecast-overrun-critical` | any **Budget** finding Red | min **Red** |
| `critical-unmitigated-risk` | any **Risk** finding Red | min **Red** |
| `key-decision-overdue` | any **Decision** finding Red | min **Amber** |

---

## 3. Formula + worked examples

**Score** = weighted average of each *present* area's worst-severity score, normalised by the weights of
the areas present (absent areas don't dilute):

```
rawScore = Σ(areaScore × weight) / Σ(weight)      # over areas that have findings
finalBucket = worst( bucket(rawScore), any tripped override floor )
```

**Example A — floor bites.** Schedule Amber, Budget Red, Risk/Resource/DQ Green (no Decision finding, so
those five areas are present, total weight 90):
`(70·20 + 30·25 + 100·25 + 100·15 + 100·5) / 90 ≈ 74 → Amber`; but Budget Red trips
`forecast-overrun-critical` → **Red**.

**Example B — no floor.** Schedule / Budget / Risk all Amber (only these three present, total weight 70):
`(70·20 + 70·25 + 70·25) / 70 = 70 → Amber`; no Red finding → no floor → stays **Amber**.

---

## 4. Per-agent RAG thresholds — *code, shipped*

| Area | Agent | Provisional rule |
|------|-------|------------------|
| **Schedule** | Status | overdue/late **≥ 30 days → Red**, **7–29 → Amber**, 1–6 → Green. Recorded status **Missed → Red**, **At Risk → Amber** (overrides the date). A **critical** milestone (`IsCritical`) in trouble (overdue/missed/at-risk) is **escalated to Red** regardless of the day-band. **Slip** (adjusted due − baseline) is surfaced as info ("slipped N days") but does **not** by itself raise severity in v0. |
| **Budget** | Financial | forecast overrun % = (Forecast−Budget)/Budget. **> 15% → Red**, any overrun **0–15% → Amber**. Also: spend running **> 10 pts** ahead of % complete → Amber. |
| **Risk** | Risk & Issue | RAID label **critical/high/severe/major → Red**; **low/minor/info → Green**; unknown/blank/medium → **Amber** (never silently Green). |
| **Resource** | Resource | allocation **> capacity → Amber** (severe over → Red); on-leave while heavily allocated (≥ 50%) → Red; spread over >1 assignment above capacity → Amber; no PM role on project → Amber. **Key-person concentration: ≥ 5 projects → Red, 3–4 → Amber, < 3 → not flagged.** |
| **Data Quality** | Data Quality | missing name / %-complete / last-updated → Amber; milestone with no due date → Amber; data **stale > 30 days → Amber**; record referencing an unknown project id → Red. (All High confidence — directly observed.) |
| **Decision** | Decision | not-approved & **past NeededBy → Red**; not-approved & **due within 14 days → Amber**; else nothing. |

> **Open (kickoff):** should milestone **slip magnitude** raise severity on its own (e.g. slip > N weeks → Amber/Red)? Today slip is display-only; only `IsCritical`-in-trouble escalates. Also confirm the real export carries `BaselineDate` + `IsCritical` (see A2).

---

## 5. Scope — display-only POC (*not scored*) — *code, shipped*

POC **"unapproved-creep"** rule (per steering, shown but excluded from the RAG score):
| Situation | Result |
|-----------|:--:|
| Unapproved scope **increase** (status ∉ {Approved, Rejected}; Type Add or impact > 0) | 🔴 Red |
| **Approved** change | 🟡 Amber |
| Open non-increase (e.g. a removal) awaiting decision | 🟡 Amber |
| Rejected / none | — (nothing) |

→ Flip to *scored* later by giving Scope a weight (taken from the other areas) — one config change.

---

## 6. This-period progress (POC) — *code, shipped*

Metric = **raw-score delta** between a project's two most recent runs.

| Delta (points) | Pace label |
|------|------|
| < −2 | Declined |
| −2 … < 2 | No movement |
| 2 … < 8 | Slow |
| 8 … < 15 | Medium |
| ≥ 15 | On track |

---

## 7. Time windows — *code, shipped*
- Decisions "due soon" look-ahead: **14 days**.
- Upcoming milestones ("next 2–4 weeks"): **28 days**.

---

## 8. L3 Data Quality checks — *config (`DataQualityOptions`), shipped*

Mirrors the `HealthScoringOptions` pattern: bound from `appsettings.json → DataQuality`, `Validate()`d
at startup, `IsPlaceholder: true`.

### 8.1 Per-risk staleness
A RAID item not updated within **21 days** (`RiskStaleThresholdDays`) → an Amber DQ finding citing the
row, carrying the age in days.

### 8.2 Duplicate-identity candidate score (0–100)
A project pair at or above **60** (`DuplicateScoreThreshold`) is flagged a duplicate candidate (Amber,
never auto-merged — the human records Merge / Keep-separate). Score = weighted sum, must total 100:

| Signal | Weight (`DuplicateWeights`) |
|--------|:--:|
| Name-token similarity (Jaccard) | 50 |
| Same customer | 30 |
| Shared resource (any person on both projects) | 20 |

*(WBS overlap, in the original register, isn't a signal yet — no WBS data parsed.)*

### 8.3 Suggested remediation — static rule-map, *code, shipped* (no LLM, not a numeric setting)
| Check | Remediation |
|-------|-------------|
| Missing name / % complete / last-updated | "Enter the project name / % complete / last-updated date." |
| Milestone missing a due date | "Add a due date to the milestone." |
| Budget line missing actuals | "Enter actual spend to date for this budget line." |
| Stale project data (> 30 days) | "Re-export the latest project data from Orbit." |
| Stale RAID item (> 21 days) | "Review and update the RAID item." |
| Orphan project-id reference | "Correct the project-id reference, or add the missing project row." |
| Resource allocated, no time logged / time logged, not on plan | "Confirm the allocation, or log time for this person." / "Add the person to the plan, or correct the time entry." |

### 8.4 Areas-completeness grid (8 input categories, *not* the 5 `HealthArea` buckets)
Per project, % of that category's records with all **POC mandatory fields** present:

| Category | Mandatory fields (POC) |
|----------|------------------------|
| Schedule | name, due date |
| Budget | budget, forecast, actual |
| Scope | title, type, status |
| Resources | person, role, allocation > 0 |
| Risks | description, severity, status |
| Decisions | title, owner, needed-by, status |
| Minutes | text |
| Time | hours logged > 0 |

Rendered informationally (Green, **excluded from scoring**); "—" when a category has zero records.

### 8.5 Confidence-lift ranking — design decision (not a client number)
Items are ranked by **global lift** across the whole portfolio (not normalised per project first) — a
lift of 2 on one project outranks a lift of 1 on another, regardless of that project's starting
confidence. Surfaces the single highest-leverage fix first; a project with several small-lift items
never out-ranks one project's single big-lift item. Flagged as a design choice, not a client input.

---

## Change map (where each number lives)
- **§1, §2** → `appsettings.json → HealthScoring` (edit + restart; validated at startup). No code change.
- **§4, §5, §6, §7** → constants in the named agent / slice (`*Skill.cs`, `SummarizeProgress.cs`). Small code change + test.
- **§8.1, §8.2** → `appsettings.json → DataQuality` (edit + restart; validated at startup). No code change.
- **§8.3, §8.4** (the remediation text and the mandatory-field set) are code, not config — changing them
  needs a small code change + test, same as §4–§7.
- Everything is flagged `IsPlaceholder` / "to be confirmed at kickoff" in the UI until the PMO signs off.
