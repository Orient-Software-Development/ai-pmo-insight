## Context

`Finding` (`Domain/Findings/Finding.cs`) is the durable analysis aggregate — an immutable record created
via `Finding.Create`, persisted by `FindingConfiguration` (owned `Citation`, enums as strings, snake_case).
It carries no typed numeric/metadata field, so agents encode computed values into the `Summary` string:
`FinancialSkill` writes `"...exposure is {n:N0}."`, `DataQualitySkill` writes `"...stale... {n} days ago."`,
`NarrativeSkill` flattens `Recommendation { Owner, Deadline, Action }` into the summary.

The nearest precedent is the nullable `Area`/`Severity` pair added by the health-scoring change: optional,
string-converted, additive, mapped in `FindingConfiguration`, introduced by a migration
(`AddFindingAreaSeverity`). This change follows that pattern for the metric.

Ticket #46 (parent #8). Consumers that will read the field are separate tickets: L1 exposure roll-up, L3
Age column, #48 recommendation.

## Goals / Non-Goals

**Goals:**

- Give `Finding` an optional, typed place for computed values so numbers/metadata are data, not prose.
- Persist it additively (nullable, no backfill), keeping every existing invariant and test green.
- Prove the mechanism end-to-end with one producer (`FinancialSkill` exposure) — stamp → persist → read.

**Non-Goals:**

- The roll-ups/views that consume the metric (L1 exposure/customer, L3 Age) — separate tickets.
- The other producers (DataQuality Age, Narrative recommendation via #48) — this change stamps only the
  Financial exposure as the smoke test.
- Removing the numbers from summary strings — summaries stay for back-compat/readability; the metric is
  additive.

## Decisions

**1. Hybrid metric shape: typed `value + unit` PLUS a `detail` string→string map.**
- `MetricValue` (`decimal?`) + `MetricUnit` (`string?`) — a typed number with a unit. Chosen so
  consumers **sum and sort without parsing** (L1 exposure total = `Σ MetricValue`; L3 Age sort by
  `MetricValue`; confidence-lift later). `decimal` matches the money/`decimal` already used in
  `BudgetLineRecord`.
- `MetricDetail` (`IReadOnlyDictionary<string,string>?`) — for structured metadata that isn't a single
  number: #48's recommendation `{ owner, deadline, action }`.

*Alternatives considered:* (a) a single stringly-typed key/value bag only — rejected: numbers become
strings again, losing the "read data not text" benefit for the roll-ups that sum/sort. (b) `value + unit`
only — rejected: can't carry the recommendation, so #48 would need a **second migration**. The hybrid does
it once and unblocks #46 + #48 + L3 together (the issue's "do it once, three consumers benefit").

**2. All three fields are optional and additive; invariants unchanged.** `Finding.Create` gains optional
params defaulting to null. A finding with no metric is valid (all null). The existing rules are untouched:
`Citation` mandatory; `Kind == Analysis ⇒ Area + Severity` non-null; non-analysis findings force
`Area`/`Severity` null. The metric is orthogonal to all of them — a metric may be present on any kind, or
absent.

**3. Persistence: two scalar columns + one `jsonb` column.** `metric_value` (`numeric` null),
`metric_unit` (`text` null) map like the existing nullable scalars. `metric_detail` maps as **`jsonb`**
via an EF value converter (`Dictionary<string,string>` ⇄ JSON) with a value comparer, stored null when
absent. Postgres `jsonb` (Npgsql) is the natural fit — no child table, queryable later if needed.
*Alternative:* an owned collection table for the detail map — rejected as overkill for a tiny, read-mostly
bag. The migration adds three nullable columns, no backfill, no data move.

**4. `FinancialSkill` is the proof producer; other producers deferred.** Only the total-exposure finding
is stamped here (`MetricValue = exposure`, `MetricUnit = currency`), keeping the summary text. This proves
the full path (agent stamps → EF persists `jsonb`+scalars → read-back intact) without pulling in the
roll-ups or the other agents. DataQuality Age and the Narrative recommendation are stamped by their own
tickets (L3 / #48).

**5. Currency source for the exposure unit.** The exposure unit comes from the budget line's currency
(`BudgetLineRecord` carries a currency in the fixture — `EUR`). If a currency isn't available on the line,
`MetricUnit` is left null (value still carried); no fabricated currency.

## Risks / Trade-offs

- **EF migration on a persisted aggregate is the highest-risk part.** Mitigation: three **nullable**
  columns, **additive**, **no backfill**; Dev auto-migrates on boot (guarded to `IsDevelopment()`), prod
  runs the migration as a deliberate deploy step (repo convention). A round-trip test locks save/read for
  both with-metric and without-metric findings.
- **`jsonb` converter needs a value comparer** or EF change-tracking mis-detects mutations. Mitigation:
  register a `ValueComparer` for the dictionary (standard Npgsql/EF pattern); a round-trip test with a
  populated `MetricDetail` guards it.
- **Snapshot/migration drift.** `AppDbContextModelSnapshot` must regenerate cleanly with the migration;
  committing generated files and re-running the suite (which spins the model) catches a mismatch.
- **Scope temptation.** It's tempting to also stamp DataQuality Age / the recommendation now. Held out
  deliberately — they're separate tickets with their own tests; this change proves the mechanism with one
  producer and stops, keeping the migration reviewable.
