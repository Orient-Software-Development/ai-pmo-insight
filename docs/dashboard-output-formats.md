# Dashboard Output Formats — L1 / L2 / L3

For every output on each dashboard level: **how it renders**, and **what produced it — a formula
(deterministic) or the LLM**. Where a formula drives it, the actual thresholds are stated, with the
plan-doc EXAMPLE numbers alongside and any **code-vs-doc divergence flagged**.

Covers **current + planned**: shipped outputs and the follow-ons from the L1/L2/L3 registers
([`l1-`](l1-executive-portfolio-followups.md) / [`l2-`](l2-project-status-followups.md) /
[`l3-`](l3-data-quality-followups.md)), the latter marked 🔷 planned.

> **Producer legend** — 🔢 formula (deterministic C#, no LLM) · 🤖 LLM · 🔀 hybrid (deterministic +
> LLM). **State legend** — ✅ live · 🟡 partial / has a known bug · 🔷 planned (in a follow-on register).
>
> **Provenance note**: "code" = the constant actually running in the agent today. "doc" = the plan doc's
> stated formula (`inputs/Project Plan with Activity Descriptions.md`). The health-scoring weights/bands
> ship as the doc's **EXAMPLE** values (`HealthScoringOptions.IsPlaceholder = true`) — placeholders until
> the PMO agrees real numbers at kick-off.

---

## Production model — who produces what

Nine agents + two pure-formula services. **Formula vs LLM is a property of the agent**, and every panel
below traces back to one of these.

| # | Agent / service | Producer | Emits |
|---|-----------------|:---:|-------|
| 1 | Data Collector | 🔢 | Parsed records (no findings) |
| 2 | Data Quality | 🔢 | DataQuality findings + a confidence signal |
| 3 | Status | 🔢 | Schedule findings (milestones) |
| 4 | Risk & Issue | 🔀 | RAID findings (deterministic) + minute-extracted risks (LLM, only when minutes exist) |
| 5 | Financial | 🔢 | Budget findings (overrun, burn, exposure) |
| 6 | Resource | 🔢 | Resource findings (allocation, capacity, key-role) |
| 7 | Narrative | 🔀 | One narrative + recommendation; **template-first**, LLM only for complex cases (≥3 signals or a minute-extracted signal) |
| 8 | Challenge | 🤖 | Adversarial critique of the narrative |
| 9 | Review | 🤖 | Reviewer questions per persona |
| — | `HealthScoringService` | 🔢 | RAG bucket + score + override audit (weighted formula) |
| — | `ConfidencePolicy` | 🔢 | High / Medium / Low confidence from the DQ signal |

**Deterministic (formula):** #1, #2, #3, #5, #6, scoring, confidence. **LLM:** #8, #9, and the minute
path of #4 + the complex path of #7. Everything a dashboard shows as a **RAG colour, score, count, %, or
severity chip is formula-produced**; only the **prose** (narrative, challenge critique, review questions)
is LLM/template.

---

## Level 1 — Executive Portfolio

| # | Panel | Rendered as | Producer | State |
|---|-------|-------------|:---:|:---:|
| 1 | Portfolio health (G/A/R) | Segmented RAG bar + legend + counts | 🔢 scoring → count per `FinalBucket` | ✅ |
| 2 | Projects needing intervention | Table, worst-first; severity chip + cited reason | 🔢 order: Red before Amber, then `RawScore` ↑; reason = worst override or worst area | ✅ |
| + | Confidence (avg) + Needs-PM-Review | Number `%` + count | 🔢 mean of per-project `HealthScore.Confidence`; flag if `< ConfidenceFloor` | ✅ |
| 3 | Financial exposure (€) | Currency number | 🔢 Σ(forecast − budget) — **computed, trapped in prose** (§Financial) | 🔷 planned roll-up |
| 4 | Resource / key-person | Chip + count | 🔢 concentration 5+/3–4/<3 (§Resource) | 🔷 planned |
| 5 | Decision backlog | Count + table | 🔢 overdue / due-soon (§Decision, planned agent) | 🔷 planned |
| 6 | Client / commercial risk | Grouped list (proxy) | 🔢 proxy: at-risk projects grouped by `customer` · **true signal 🔵 needs client** | 🔷 proxy planned |
| 7 | Recommended actions | Owner · deadline · action card | 🔀 Narrative `Recommendation` (template or LLM) — **structured, flattened to prose** (§Recommendation) | 🔷 planned surfacing |

## Level 2 — Individual Project Status

| # | Panel | Rendered as | Producer | State |
|---|-------|-------------|:---:|:---:|
| 1 | Overall status (RAG + explanation) | RAG chip + HealthBanner (area breakdown, override audit) | 🔢 `HealthScoringService` | ✅ |
| 8 | Confidence level | `%` in header + High/Med/Low chips per finding | 🔢 `ConfidencePolicy` | ✅ |
| 4 | Risks & issues | Table rows; severity chip + citation | 🔀 RAID rows deterministic; minute risks LLM | ✅ |
| 3 | Key deviations (budget/time/scope/risks/resources) | Findings table | 🔢 Financial + Status + Risk + Resource | 🟡 4/5 — **Scope has no data/rule** |
| 5 | Upcoming milestones | Finding rows | 🔢 Status agent, 14-day window | 🟡 **bug — ignores `Status`/baseline/`is_critical`** (§Schedule) |
| 7 | AI recommendation | Prose blob today | 🔀 Narrative (template/LLM) | 🟡 structured contract exists, flattened |
| 2 | This-period progress | — | 🔢 needs run-over-run delta (planned) + 🔵 "activity" threshold (client) | 🔷 planned |
| 6 | Decisions needed (owner/deadline/consequence) | Placeholder | 🔢 planned Decision agent | 🔷 planned |
| — | Challenge (US-9) | Prose critique list | 🤖 LLM (#8) | ✅ |
| — | Review (US-9) | Per-persona question list | 🤖 LLM (#9) | ✅ |

## Level 3 — Data Quality

| Panel | Rendered as | Producer | State |
|-------|-------------|:---:|:---:|
| Confidence hero | Big `%` + threshold + below-target chip | 🔢 mean `HealthScore.Confidence` vs `ConfidenceFloor` | ✅ |
| Missing / inconsistent items | Table; severity chip + citation | 🔢 Data Quality agent checks (§Data Quality) | ✅ |
| Age column | Days number | 🔢 computed (`LastUpdateAgeDays`) — **trapped in prose** | 🔷 planned (Finding metric field) |
| Suggested remediation | Text per row | 🔢 static rule-map (check-type → fix) | 🔷 planned |
| Confidence-lift ordering | Ordered rows / delta | 🔢 counterfactual over `ConfidencePolicy` | 🔷 planned |
| Areas completeness grid (8 cat) | Grid of `%` | 🔢 present / expected per category (EXAMPLE mandatory list) | 🔷 planned |
| Duplicate identity candidates | Table + Merge / Keep-separate | 🔢 similarity heuristic (name + customer + resource/WBS overlap) — **never auto-merges (US-2)** | 🔷 planned |

**All L3 outputs are formula-produced** (no LLM) — L3 is pure deterministic data-quality checking.

---

## Formula reference (code actual · doc EXAMPLE · divergence)

Exact thresholds behind the 🔢 panels. Constants are from the named agent file.

### Health scoring (`HealthScoringService` + `HealthScoringOptions`)
- **Weights (doc EXAMPLE = shipped placeholder):** Schedule 20% · Budget 20% · Scope 15% · Resources 15% ·
  Risks/issues 15% · Decisions/dependencies 10% · Data quality 5%.
- **Buckets (doc EXAMPLE):** 80–100 Green · 60–79 Amber · 0–59 Red.
- **Overrides (doc EXAMPLE):** critical milestone missed → min Amber · forecast overrun >15% → min Amber/Red ·
  critical unmitigated risk → min Red · key decision overdue+blocking → min Amber · confidence very low →
  "Needs PM Review".
- ⚠️ **Divergence:** `HealthArea` has only 5 buckets (Schedule/Budget/Risk/Resource/DataQuality). **Scope
  (15%) and Decisions (10%) have no area** — 25% of the doc's weight can't currently be scored.

### Confidence (`ConfidencePolicy.cs`)
- Start High; `MissingFieldCount` >2 → −2, >0 → −1; age >90d → −2, >30d → −1; `!SourceConsistent` → −1.
- Level ≥2 High · =1 Medium · else Low. LLM self-report is capped by this.
- **doc:** "confidence level — how much to trust the status" (no formula given). Code is the PRD-added POC default.

### Schedule (`StatusSkill.cs`, HealthArea.Schedule)
- Late/overdue bands (days): ≥30 → Red · ≥7 → Amber · else Green. Upcoming window = 14 days → Green "due soon".
- **doc:** milestone adherence, schedule variance, delay severity, upcoming-milestone risk (next 2–4 weeks).
- 🔴 **Divergence / bug:** reads only the adjusted `DueDate`. **Ignores `Status` (e.g. "Missed"),
  `baseline_date` (the slip), and `is_critical`** — so a critical missed milestone renders as a benign green
  "due soon". `baseline_date`/`is_critical` are in the CSV but dropped by the parser. (L2 §#5a/#5b)

### Budget (`FinancialSkill.cs`, HealthArea.Budget)
- Forecast overrun band: >15% → Red · else Amber. Spend-ahead-of-progress: `spend% − progress% > 10` → Amber.
  Exposure = Σ(forecast − budget) where forecast > budget → Amber.
- **doc:** Green ≤+5% · Amber >5–15% · Red >15%. "40% done / 70% burned = high risk". Financial exposure = overrun amount.
- ⚠️ **Divergence:** code has **no 5% Green tolerance** — any overrun >0 emits at least an Amber finding
  (doc allows ≤5% as Green). Exposure amount + currency live only in the summary **string** (no numeric field).

### Resource (`ResourceSkill.cs`, HealthArea.Resource)
- Over-allocation band: `allocation − capacity > 20` points → Red · else Amber. On-leave + allocation ≥50% → Red.
  Capacity pressure: >1 assignment and total > capacity → Amber. Missing PM: no role contains "Manager" → Amber.
- **doc:** concentration 5+ projects → Red, 3–4 → Amber, <3 → Green (line 148); no allocation → Red; absence "clarify if possible".
- 🔴 **Divergence / bug:** the **doc's headline 5+/3–4/<3 concentration rule is not implemented** (planned #4).
  "Missing PM" check uses `Contains("Manager")` but the data says `"Project Management"` → **false "no PM"**
  finding. (L1 §#4)

### Data Quality (`DataQualitySkill.cs`, HealthArea.DataQuality)
- Missing field (name/%/last-updated) → Amber. Milestone missing due date → Amber. Project staleness >30 days
  → Amber. Orphan reference (unknown project id) → Red. All High confidence (directly observed).
- **doc L3 examples:** "no risk update in **21 days**", budget actuals missing, milestone dates not updated,
  resource plan ≠ time entries.
- ⚠️ **Divergence:** staleness is **project-level at 30 days**, not **per-risk at 21 days** (doc example #1).
  Consistency is **orphan-only** — no budget/time/resource cross-agreement, and no duplicate detection yet. (L3)

### Decision (planned agent — not yet built)
- **doc:** decisions overdue (past `needed_by`, not made) · due soon (next 1–2 weeks) · blocked-by count.
  Override: key decision overdue + blocking → min Amber.
- Data present in `decisions.csv` (status / needed_by / owner / consequence). Blocked on `HealthArea.Decision`. (L1 §#5)

### Recommendation (`NarrativeSkill.cs` → `Recommendation` record)
- **Structured contract already exists**: `{ Owner, Deadline, Action, Rationale }`. Template path fills generic
  values ("Project Manager" / "next 2 weeks" / "this week" / "n/a"); LLM path fills specifics.
- 🟡 **Flattened**: `ToFinding` collapses the record into one prose string
  (`"[status] … Recommendation (owner, by deadline): action — rationale"`). Surfacing owner/deadline as
  structured fields = stop flattening (or add fields to `Finding`). Not "unstructured" — trapped. (L1/L2 §#7)

---

## Cross-cutting: three "trapped in a string" cases share one fix

The `Finding` aggregate carries only `Summary / Area / Severity / Confidence / Citation` — no typed numeric
or metadata field. Three formula outputs are computed correctly but stringified:

| Output | Computed by | Trapped as |
|--------|-------------|-----------|
| Financial exposure (€) | Financial agent | `"…exposure is 80,000."` |
| Staleness age (days) | Data Quality agent | `"…stale… N days ago."` |
| Recommendation owner/deadline | Narrative agent | `"Recommendation (owner, by deadline): …"` |

**One `Finding`-shape change** (typed metric/metadata fields) surfaces all three as structured outputs at
once — the highest-leverage single change across the three dashboards.

---

## One-line summary

- **RAG / score / count / % / severity chips → always a formula.** (scoring, confidence, the 5 deterministic agents)
- **Prose → LLM or template.** Narrative (template-first), Challenge, Review; plus minute-extracted risks.
- **The three known correctness gaps are all in formula agents**, not the LLM: the milestone `Status` bug (Schedule),
  the "no PM" string match (Resource), and the missing 5+/3–4/<3 concentration rule (Resource).
- **Everything a stakeholder would call a "number" is deterministic and auditable** — the LLM only writes the words.
