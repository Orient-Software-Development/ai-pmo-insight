# Kickoff questions — from POC to production

> **Status: POC.** Every weight, threshold, and RAG rule shipped today is an **EXAMPLE placeholder**
> (flagged `IsPlaceholder` in config and "to be confirmed" in the UI). Nothing here is a decision we
> made *for* the client — it's a starting point to react to. This checklist is what we need the
> PMO/client to **provide (data)** or **agree (rules)** before go-live.
>
> Related: L1/L2/L3 dashboard follow-on registers #67/#68/#69 (all buildable items shipped; this
> document is what's left — client input), #45 (`HealthArea` enum incl. Scope), multi-file on hold
> pending the export convention. Current shipped POC numbers/formulas: `poc-data-rules-v0.md`.

---

## The one big question first

**What does the real Orbit export actually look like — format and columns?**

Everything downstream depends on this. Today the parser assumes **one workbook, many tabs**
(`Projects`, `Milestones`, `Budget`, `Resources`, `RAID`, `Decisions`, `Scope`) with a guessed
PascalCase column layout. If the real export differs (multiple files, different names), the parser
must be re-pointed and several features unblock.

---

## A. Data — what fields does Orbit actually provide?

| # | Question | Why we need it | Blocks |
|---|----------|----------------|--------|
| A1 | Export **format**: one workbook/many tabs, or multiple files? Exact **tab + column names**? | Parser currently guesses the schema | Multi-file analyze, all parsing |
| A2 | Do milestones carry a **BaselineDate** + a **critical/IsCritical** flag? | Needed to compute schedule **slip** ("7-week slip") and flag critical milestones | #68 item 2 (slip) |
| A3 | Is there a **scope-change log**? What columns (type / status / effort impact)? | Scope panel currently runs on invented data | Scope (real) |
| A4 | Do **decisions** carry Owner / NeededBy / Consequence? | The "Decisions needed" panel renders these columns | Panel 6 fidelity |
| A5 | Are there **Customer** and **Currency** columns on projects/budget? | L1 customer-exposure grouping + € financial exposure | L1 panels |
| A6 | Is data exported **per period** (multiple snapshots over time)? | "This-period progress" compares two runs | Panel 2 (real, not seeded) |
| A7 | Is there an **uploader / author** field, and one file **per project** or per portfolio? | History provenance, multi-file batch grouping | History detail, multi-file |

---

## B. Health scoring (RAG) — the core rules to agree

All values below are the **current EXAMPLE placeholders** (`appsettings.json → HealthScoring`,
`IsPlaceholder: true`). Client to confirm or replace.

### B1. Area weights (must sum to 100)
| Area | Current (EXAMPLE) | Client value |
|------|:--:|:--:|
| Schedule | 20 | ? |
| Budget | 25 | ? |
| Risk | 25 | ? |
| Resource | 15 | ? |
| Decision | 10 | ? |
| Data Quality | 5 | ? |
| **Scope** | *not scored (display-only)* | include? at what weight? |

### B2. Severity → score
| Severity | Current | Client value |
|------|:--:|:--:|
| Green | 100 | ? |
| Amber | 70 | ? |
| Red | 30 | ? |

### B3. RAG bands (inclusive lower bound on the weighted score)
| Band | Current | Client value |
|------|:--:|:--:|
| Green | ≥ 80 | ? |
| Amber | ≥ 60 | ? |
| Red | below Amber | ? |

### B4. Override "worst-case floor" rules
The score can be floored regardless of the weighted average. Current EXAMPLE rules
(`HealthScoring:Overrides`):
- Critical milestone **Missed** → minimum **Amber**
- Budget forecast **overrun (critical)** → minimum **Red**
- Critical **unmitigated risk** → minimum **Red**
- **Key decision overdue** → minimum **Amber**

→ *Does the client agree with these floors? Any to add/remove/re-band?*

### B5. Confidence
- "Needs PM Review" fires when aggregate confidence **< 50** (EXAMPLE). Right threshold?
- Confidence scores: Low 30 / Medium 70 / High 100 (EXAMPLE). Right values?

---

## C. Per-agent thresholds (what turns each area Red / Amber / Green)

Current logic lives in `source/…/Features/Analysis/Agents/*Skill.cs`. Confirm the bands:

| Area | Current EXAMPLE rule | Client rule |
|------|----------------------|-------------|
| **Schedule** (Status) | days late/overdue: ≥ 30 = Red, ≥ 7 = Amber, else Green | ? |
| **Budget** (Financial) | forecast-overrun % bands (see `FinancialSkill`) | ? |
| **Risk** (Risk & Issue) | RAID severity → RAG mapping | ? |
| **Resource** | over-allocation (allocation > capacity); key-person = 1 person across N projects | ? |
| **Data Quality** | which gaps count (missing due date, staleness > 30d, orphan reference); per-risk staleness > 21d (`RiskStaleThresholdDays`); duplicate-candidate score ≥ 60 (`DuplicateScoreThreshold`), weighted 50% name-similarity / 30% same-customer / 20% shared-resource (`DuplicateWeights`) | ? |
| **Decision** | overdue = Red, due-soon = Amber (see D below) | ? |

---

## D. Scope (currently POC, display-only)

- **D1.** What is the client's **Scope RAG rule**? (Current POC "unapproved-creep": unapproved
  increase = Red, approved/open = Amber, none = Green.)
- **D2.** Should Scope be **scored into the RAG colour** (it is *excluded* today), and at what weight?
- **D3.** Confirm the scope-change data shape (ties to A3).

---

## E. This-period progress (currently POC)

- **E1.** The pace label thresholds — what score movement counts as **no movement / slow / medium /
  on track**? (Current EXAMPLE bands on the raw-score delta: <−2 Declined, <2 No movement, <8 Slow,
  <15 Medium, else On track.)
- **E2.** Should "progress" be driven by **score delta** (current), **% complete**, or something else?
- **E3.** Confirms A6 (need ≥ 2 periods of data).

---

## F. Time windows

- **F1.** "Decisions needed" look-ahead — currently **14 days**. Right?
- **F2.** "Upcoming milestones — next 2–4 weeks" — implemented as **28 days**. Right?

---

## G. Operational (not data rules, but chose-once)

- **G1.** LLM provider per agent — **Anthropic** or **OpenAI**? (Adapters ready; needs the model id +
  API key via secret, per-agent overridable.)
- **G2.** Any per-user / per-team data scoping, or is the shared-workspace model (all authenticated
  users see all projects) acceptable?

---

## H. L1 — client/commercial risk (genuinely underspecified)

The one L1 panel with no formula at all: *"projects at risk of damaging commitments."* We ship a
**relationship-exposure proxy** (at-risk projects grouped by customer) — labelled as a proxy, not
commercial risk.

- **H1.** Does Orbit (or another system) carry **contract value / margin / SLA-penalty** data? Without
  it, true commercial risk can't be computed — only the customer-grouping proxy.
- **H2.** If that data exists, what's the RAG rule for "damaging commitments"?

## I. L3 — data quality (non-blocking, ships as EXAMPLE either way)

- **I1.** Is duplicate project identity a **real problem** in Orbit data (worth building for), or rare
  enough to skip? (Not "can we build it" — we already have. A prioritisation question.)
- **I2.** Which fields are **truly mandatory** per category for the areas-completeness grid (§8.4 in
  `poc-data-rules-v0.md`)? Current set is our own placeholder.

---

## Priority

1. **A1** (export format) — unblocks the most.
2. **B1–B4** (scoring model) — the numbers that decide every RAG colour.
3. **A2 / A3** (baseline+critical, scope data) — unblock slip and real Scope.
4. **H1** (commercial-risk data) — the one L1 panel with zero formula today.
5. Everything else (I1/I2, per-agent thresholds, time windows) can default to the POC placeholder
   until confirmed.
