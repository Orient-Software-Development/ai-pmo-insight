# L2 Individual Project Status — Follow-on Register

Captures the gap between what the **plan doc** (`inputs/Project Plan with Activity Descriptions.md`)
specifies for **Level 2 — Individual Project Status** and what the L2 view (`ProjectFindings.jsx`) +
the analysis agents actually back today. Companion to
[`l1-executive-portfolio-followups.md`](l1-executive-portfolio-followups.md); shared items
(Decisions, Recommendation, the `HealthArea` enum gap) are cross-referenced, not duplicated.

> **Legend** — ✅ backed & live · 🟢 can do now (formula + fixture data both exist, pure build) ·
> 🟠 can do, but needs an internal parser/shape change + one internal check · 🔴 cannot do yet —
> needs client clarification at kick-off.

## The plan doc's L2 spec (8 panels)

The doc (lines 243–251) lists eight L2 panels. Current status:

| # | Panel (doc) | Status | Where it stands |
|---|-------------|:---:|-----------------|
| 1 | Overall status (RAG + explanation) | ✅ | Header RAG chip + `HealthBanner` (area breakdown, override audit) = the explanation |
| 2 | This-period progress (what moved forward) | 🔴 | Needs run-over-run delta **and** undefined "activity" thresholds — see below |
| 3 | Key deviations (budget/time/scope/risks/resources) | 🟠/🔴 | 4 of 5 areas backed; **Scope has no data and no rule** — see below |
| 4 | Risks & issues (top items) | ✅ | RAID findings + minutes extraction |
| 5 | Upcoming milestones (next 2–4 weeks) | 🟢/🟠 | Emitted, but **mis-frames a missed critical milestone as benign** — see below |
| 6 | Decisions needed (owner/deadline/consequence) | 🟢 | Flagged placeholder today; data + formula both exist |
| 7 | AI recommendation (practical next action) | 🟠 | Narrative prose blob; needs a structured contract |
| 8 | Confidence level (H/M/L) | ✅ | Header + per-finding chips + aggregate |

**Score: 3 backed, 3 partial/buildable, 2 blocked on client input.**

---

## The can-do / cannot-do split

```
   CAN DO NOW (build, no blockers):     #5a milestone-Status,  #6 decisions,  #7 recommendation
   CAN DO (needs 1 internal check):     #5b milestone full  (verify override wiring)
   CANNOT DO (needs client kick-off):   #3 scope,  #2 progress
```

Two verification types — **do not conflate**:
- **Internal** (we check ourselves in code/data): #5b — is the critical-milestone override wired?
- **External** (client decides at kick-off): #3 Scope rule + data, #2 progress thresholds.

---

## 🟢 #5a — Milestone bug: the Status agent inverts the worst schedule signal (do this first)

**The bug.** On ORB-1002 the Status agent renders *"Milestone 'Data cutover dress rehearsal' is due
soon (in 2 days)"* as **Green, informational** — when the data says it is a **critical, missed** checkpoint
that already slipped 7 weeks.

Data (`timeline-milestones.csv`):
```
ORB-1002, Data cutover dress rehearsal,
  baseline_date=2026-05-30,  adjusted_date=2026-07-18,  status=Missed,  is_critical=Yes
```

`StatusSkill.cs` reads **only the adjusted `DueDate`** (18 Jul → 2 days out) and ignores three fields:

| Field | In CSV? | On `MilestoneRecord`? | Agent reads it? |
|-------|:---:|:---:|:---:|
| adjusted_date | ✅ | ✅ `DueDate` | ✅ → "due in 2 days" |
| **status=Missed** | ✅ | ✅ `Status` | ❌ **ignored** |
| baseline_date | ✅ | ❌ dropped | ❌ (can't see the 7-week slip) |
| is_critical | ✅ | ❌ dropped | ❌ (can't feed the override) |

Why it matters: ORB-1002 shows Red overall, but that Red comes from the **minutes** (LLM-extracted "dress
rehearsal did not pass") + budget — **not** from the Status agent flagging the missed critical milestone.
A text extraction is accidentally covering for a broken deterministic signal.

**Build shape (near-free).** `MilestoneRecord.Status` is **already parsed and in memory** — the agent just
never reads it. Make the Status agent read `Status`: a `Missed` / `At Risk` milestone must not render as a
green "due soon". No parser change, no data change.

---

## 🟠 #5b — Milestone full fix: schedule slip + critical-missed override

**What's missing.** To compute the schedule slip (baseline → adjusted) and to fire the doc's override
*"Critical milestone missed → minimum Amber"* (line 185), the record needs two columns the parser currently
drops: `baseline_date` and `is_critical`. Both are present in `timeline-milestones.csv`.

**Build shape.** Add `BaselineDate` + `IsCritical` to `MilestoneRecord` + the `ExcelProjectParser` mapping;
have the Status agent compute slip = adjusted − baseline and emit a critical-missed signal.

**Internal check required before this pays off** (🟠, not 🟢): confirm the
*"critical milestone missed → min Amber"* override is actually wired in `HealthScoringOptions` and identify
which finding-signal it keys on. If the override isn't wired, that is part of this item's scope. **Not a
client question — a code check.**

---

## ✅ #6 — Decisions needed — agent LANDED (`add-decisions-agent`, #45+#47)

> **Update:** the shared Decision agent shipped (see the L1 register #5). Decision findings (overdue /
> due-soon, cited) now flow to `GET /api/projects/{key}` and appear in the L2 findings section. A dedicated
> "decisions needed" panel with structured owner/deadline is a presentation follow-on (the owner/deadline
> can ride the #46 `MetricDetail`). Original notes below.

**Same build as L1 #5** — see [`l1-executive-portfolio-followups.md`](l1-executive-portfolio-followups.md#5--decision-backlog--highest-formula-to-effort-ratio-do-first).
Doc formula clear (lines 134–144); `decisions.csv` carries status / needed_by / owner /
consequence_if_delayed. One build (parse `Decisions` + `DecisionSkill` + `HealthArea.Decision`) satisfies
**both** L1's decision-backlog panel and L2's decisions-needed panel, and recovers the missed D-1002-1
OVERDUE signal. Blocked on adding `HealthArea.Decision` (the enum gap documented in the L1 register).

---

## 🟠 #7 — AI recommendation (practical next action)

**Same as L1 #7** — a structured-output contract, not a formula. The Narrative agent already writes a
recommendation as a **prose blob** (e.g. *"Recommendation (Sponsoring Executive / PMO Director, by Within
24 hours): …"*). Change it to `{ action, owner, deadline, confidence }`; owner + deadline come from the
decision data, so **sequence this after #6**.

---

## 🔴 #3 — Key deviations: Scope is unbacked (needs client verification)

Four of the five deviation areas are backed: **Budget** (Financial), **Time** (Status/Schedule), **Risks**
(Risk & Issue), **Resources** (Resource). **Scope is not**, on two counts:

1. **No data.** `scope-wbs.csv` is a **WBS/task tree with % complete + status** — *not* a scope-change log.
   The doc's Scope KPIs (lines 106–118) are all about *changes*: scope change count, unapproved items,
   change request value, scope stability. **None of that data exists in the fixtures.**
2. **No rule.** Unlike Budget (≤5% / 5–15% / >15%) and Resources (5+/3–4/<3), the doc gives **no RAG
   threshold** for Scope.

**Verify with client at kick-off:** (a) does Orbit carry scope-*change* records (a change log, not just the
WBS)? (b) what is the Scope RAG rule? Until both are answered, Scope cannot be scored — and note the wider
enum gap: `HealthArea` has no `Scope` bucket (the doc weights Scope at 15%), documented in the L1 register.

> Presentation note: the current L2 view renders findings as a flat table, not grouped as "deviations by
> area". Even for the 4 backed areas, a small view-only change would group them under the doc's deviation
> headings. That part is 🟢 (presentation-only) and independent of the Scope data question.

---

## 🔴 #2 — This-period progress (needs run history + a client threshold)

The deepest gap. Two blockers at once:

1. **Build.** "What moved forward this period" needs a **run-over-run delta** — comparing this run to a
   prior run. The store already appends findings per `RunId`, so run history exists, but nothing diffs two
   runs, and a fresh demo has only one run to compare.
2. **Client decision.** The doc's activity signal — *"No moving forward / very slow / medium / okay?"*
   (line 151) — has **no thresholds**. Already logged in the PRD open questions.

Cannot be built cleanly until both the diff logic exists and the client defines "moving forward vs slow".

---

## Suggested sequence

| Order | Item | Rationale |
|:---:|------|-----------|
| 1 | **#5a milestone-Status** | Both a bug *and* near-free; the worst project's worst signal is currently green — fix reads a field already in memory |
| 2 | **#6 Decisions** | Data + formula exist; one build serves L1 + L2; unblocks `HealthArea.Decision`; recovers D-1002-1 |
| 3 | **#7 Recommendation** | Structured contract; depends on #6 for owner/deadline |
| 4 | **#5b milestone full** | After an internal check that the critical-missed override is wired |
| — | **#3 Scope** | Deferred to kick-off — Orbit scope-change data + Scope RAG rule both undefined |
| — | **#2 Progress** | Deferred — needs run-diff build + client "activity" thresholds |

**Presentation-only quick win, independent of all the above**: group the 4 backed deviation areas under
the doc's headings in the L2 view (no new data).
