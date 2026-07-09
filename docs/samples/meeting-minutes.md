# Steering Committee — ORB-1002 Customer Data Migration

**Date:** 2026-06-18
**Attendees:** Anna Berg (PM, dialling in — on holiday from 2026-06-16 to 2026-06-27),
Sven Aalto (Migration Specialist), Client Sponsor (M. Larsen), NextWave PMO (observer)

## 1. Status

Project is **Red**. The data cutover dress rehearsal (milestone M-1002-2), baselined for
2026-05-30, **did not pass** — referential integrity errors surfaced across ~4% of customer
records once transformation rules ran end to end. This is worse than the profiling report
suggested back in March. Rehearsal is now forecast for 2026-07-18 at the earliest.

## 2. Budget

Spend has crossed the planned €480k (actuals at €505k) and the forecast at completion is now
€560k — roughly a 17% overrun. Driver is unplanned overtime on data cleansing plus a likely
extra cleansing sprint.

## 3. Key points raised

- Sven flagged that the legacy source quality risk (R-1002-1) is materialising and still has
  **no agreed mitigation** — needs an owner decision this week.
- With Anna on leave for two weeks and also covering four other projects, the committee noted the
  team is exposed to a **key-person dependency**. Cover arrangements were not settled.
- Client sign-off on the mapping rules is still outstanding, blocking the rehearsal re-run.

## 4. Decisions needed

- **D-1002-1 (OVERDUE):** approve the revised go-live date of 2026-08-01. Was needed by
  2026-06-20. Until approved, cutover cannot be scheduled and the delivery team sits idle.
- **D-1002-2:** fund an additional data-cleansing sprint (~€60k). Sponsor to decide by 2026-07-15.

## 5. Actions

| Action | Owner | Due |
|--------|-------|-----|
| Circulate integrity-error breakdown to sponsor | Sven Aalto | 2026-06-20 |
| Secure PM cover for holiday period | Anna Berg | 2026-06-19 |
| Confirm go-live date decision | Steering Committee | 2026-06-20 |
