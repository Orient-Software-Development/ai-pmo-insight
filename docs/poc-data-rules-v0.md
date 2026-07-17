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

### 1.1 Area weights (sum = 100) — *config, shipped*
| Area | Weight | Note |
|------|:--:|------|
| Schedule | 20 | |
| Budget | **30** | leaning heavier |
| Risk | **30** | leaning heavier |
| Resource | 15 | |
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

**Example A — floor bites.** Schedule Amber, Budget Red, Risk/Resource/DQ Green:
`(70·20 + 30·30 + 100·30 + 100·15 + 100·5) / 100 = 73 → Amber`; but Budget Red trips
`forecast-overrun-critical` → **Red**.

**Example B — no floor.** Schedule Green, Budget Amber, Risk Green (only these three present):
`(100·20 + 70·30 + 100·30) / 80 = 76.25 → Amber`, no Red findings → stays **Amber**.

---

## 4. Per-agent RAG thresholds — *code, shipped*

| Area | Agent | Provisional rule |
|------|-------|------------------|
| **Schedule** | Status | overdue/late **≥ 30 days → Red**, **7–29 → Amber**, 1–6 → Green. Recorded status **Missed → Red**, **At Risk → Amber** (overrides the date). |
| **Budget** | Financial | forecast overrun % = (Forecast−Budget)/Budget. **> 15% → Red**, any overrun **0–15% → Amber**. Also: spend running **> 10 pts** ahead of % complete → Amber. |
| **Risk** | Risk & Issue | RAID label **critical/high/severe/major → Red**; **low/minor/info → Green**; unknown/blank/medium → **Amber** (never silently Green). |
| **Resource** | Resource | allocation **> capacity → Amber** (severe over → Red); on-leave while heavily allocated (≥ 50%) → Red; spread over >1 assignment above capacity → Amber; no PM role on project → Amber. **Key-person concentration: ≥ 5 projects → Red, 3–4 → Amber, < 3 → not flagged.** |
| **Data Quality** | Data Quality | missing name / %-complete / last-updated → Amber; milestone with no due date → Amber; data **stale > 30 days → Amber**; record referencing an unknown project id → Red. (All High confidence — directly observed.) |
| **Decision** | Decision | not-approved & **past NeededBy → Red**; not-approved & **due within 14 days → Amber**; else nothing. |

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

## Change map (where each number lives)
- **§1, §2** → `appsettings.json → HealthScoring` (edit + restart; validated at startup). No code change.
- **§4, §5, §6, §7** → constants in the named agent / slice (`*Skill.cs`, `SummarizeProgress.cs`). Small code change + test.
- Everything is flagged `IsPlaceholder` / "to be confirmed at kickoff" in the UI until the PMO signs off.
