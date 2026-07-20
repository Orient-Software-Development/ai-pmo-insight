## Why

Every number the analysis agents compute is baked into a finding's `Summary` **string**. The `Finding`
aggregate (`Domain/Findings/Finding.cs`) carries only `Summary / Area / Severity / Confidence / Citation`
— no typed numeric or metadata field. So:

```
   Financial exposure  €80,000  → trapped in "...exposure is 80,000."   (FinancialSkill)
   Staleness age       N days   → trapped in "...stale... N days ago."  (DataQualitySkill)
   Recommendation      owner/deadline/action → flattened into the narrative summary (NarrativeSkill)
```

Consumers (the L1 roll-up, the L3 view, the recommendation renderer) must **string-parse** to recover
those values — fragile, and it breaks silently when the summary wording drifts. This is the single
highest-leverage shared change across the three dashboards (ticket #46, parent #8): add the typed field
**once**, and three consumers — L1 € exposure roll-up, L3 Age column, and #48's structured recommendation —
all read data instead of text, without each needing its own migration.

This change is scoped to the **shape + persistence + one proof-of-mechanism producer** only. The roll-ups
and the other producers (L3 Age, #48 recommendation) are their own tickets that consume this field.

## What Changes

- **`Finding` gains an optional, additive structured metric** (all nullable, never required):
  - `MetricValue` (`decimal?`) + `MetricUnit` (`string?`, e.g. `"EUR"`, `"days"`) — a typed number+unit, so
    consumers can **sum/sort without parsing** (L1 exposure total, L3 Age sorting, later confidence-lift).
  - `MetricDetail` (a small `string`→`string` map, nullable) — for structured metadata that isn't a single
    number, e.g. #48's recommendation `{ owner, deadline, action }`.
- **`Finding.Create` keeps the metric optional.** Existing findings without it stay valid; the existing
  invariants are unchanged whether or not a metric is present — mandatory `Citation`, and
  `Kind == Analysis ⇒ Area + Severity`.
- **EF persistence + migration.** Map the new fields in `FindingConfiguration` (snake_case:
  `metric_value` numeric null, `metric_unit` text null, `metric_detail` as `jsonb` null), mirroring the
  existing nullable `Area`/`Severity` precedent. Add an EF migration (`AddFindingMetric`); commit the
  generated files; Development auto-migrate must still boot.
- **One producer proves the mechanism end-to-end.** `FinancialSkill`'s total-exposure finding stamps the
  exposure **amount + currency** onto `MetricValue`/`MetricUnit` (in addition to the summary text, which
  stays for back-compat). This proves stamp → persist → read-back without waiting on the roll-ups.

Not in scope (separate tickets that consume this field): the L1 financial-exposure / customer-exposure
roll-up (`add-l1-portfolio-signals` slice D/E), the L3 Age column, and #48's structured recommendation
(the Narrative/DataQuality producers of the metric). This change stamps only the Financial exposure as the
smoke test.

## Capabilities

### Modified Capabilities

- `project-findings`: the `Finding` aggregate gains an optional structured metric (`MetricValue` +
  `MetricUnit`, and a `MetricDetail` map) alongside the existing summary, persisted with the finding, so a
  numeric finding carries its number as data — the existing citation and area/severity invariants are
  unchanged.

## Impact

- **Domain:** `Finding` gains `MetricValue` / `MetricUnit` / `MetricDetail` (nullable) + `Finding.Create`
  optional params; no invariant change.
- **Infrastructure:** `FindingConfiguration` maps the three columns; **EF migration `AddFindingMetric`**
  (nullable, additive, no backfill). `AppDbContextModelSnapshot` regenerated.
- **Application:** `FinancialSkill` stamps exposure amount + currency onto the metric (proof producer).
- **API / client:** none in this change (the roll-ups/views that read the metric are separate tickets).
- **Tests (TDD):** domain test (metric optional, invariants hold with/without it); persistence round-trip
  test (with-metric + without-metric); `FinancialSkill` test (exposure amount on the metric). Full backend
  suite stays green (baseline 127 Application + 122 Api = 249).
- **Docs:** tick the Finding-metric item in `docs/l3-data-quality-followups.md`; note #46 done.
- **Risk:** the migration is the highest-risk part — mitigated by nullable + additive + no backfill; Dev
  auto-migrates, prod applies as a deploy step (repo convention).
