> **TDD** — failing test first (red), minimal code (green), refactor; keep the full backend suite green
> before checking off a task. Baseline: 127 Application + 122 Api = 249. Implements shared ticket #46. This
> change is the **shape + persistence + one proof producer** only — the roll-ups/views and the other
> producers (L3 Age, #48 recommendation) are separate tickets that consume this field.

## 1. Baseline (no code)

- [x] 1.1 Baseline green (249). 1.2 Re-read `Finding.cs`, `FindingConfiguration.cs`, `AddFindingAreaSeverity`
      as the nullable-additive precedent.
- [x] 1.2 Precedent confirmed: nullable `Area`/`Severity` mapped string-converted; migration additive.

## 2. Slice A — Domain: optional metric on `Finding` (TDD)

- [x] 2.1 (red) Added 4 tests (numeric metric, detail map, all-null default, invariants-with-metric). Failed
      to compile — API absent (expected red for a shape addition).
- [x] 2.2 (red) Invariant-with-metric test included (Analysis + no area + metric → still throws).
- [x] 2.3 (green) Added `MetricValue` (decimal?) / `MetricUnit` (string?) / `MetricDetail`
      (`IReadOnlyDictionary<string,string>?`) to `Finding` + optional `Finding.Create` params (default null).
      Invariants unchanged.
- [x] 2.4 `FindingTests` green: 17 passed (13 + 4 new).

## 3. Slice B — Persistence + migration (EF)

- [x] 3.1 (green) `FindingConfiguration` maps `metric_value` (numeric null), `metric_unit` (text null),
      `metric_detail` (`jsonb` null) via a `Dictionary<string,string>` ⇄ JSON converter **with a
      `ValueComparer`**. (Note: adding the unmapped `IReadOnlyDictionary` in Slice A broke the whole model
      build — 38 Api tests — until this mapping landed; that's why B follows A immediately.)
- [x] 3.2 (green) `dotnet ef migrations add AddFindingMetric` succeeded (three nullable columns, no
      backfill); `AppDbContextModelSnapshot` regenerated. Generated files ready to commit.
- [x] 3.3 (red→green) Round-trip tests added to `FindingAreaSeverityPersistenceTests`: with value+unit+
      populated detail reads back intact; without metric reads back all-null.
- [x] 3.4 Auto-migrate confirmed — the full Api suite (real Postgres + `DbInitializer` auto-migrate) boots
      and passes, exercising the new migration.
- [x] 3.5 Full Api suite green: 124 passed (122 + 2 round-trip).

## 4. Slice C — Proof producer: Financial exposure on the metric (TDD)

- [x] 4.1 (red) `FinancialAgentTests.Exposure_finding_carries_the_amount_and_currency_on_its_metric` —
      failed (MetricValue null) until the producer stamped it.
- [x] 4.2 (green) `FinancialSkill` stamps `MetricValue = exposure`, `MetricUnit = <line currency>` on the
      total-exposure finding (via a `FindingFactory.Analysis` metric overload); summary kept. Added
      `Currency` to `BudgetLineRecord` + parsed it (`NullIfBlank(cell("Currency"))`); unit null if absent.
- [x] 4.3 Full backend suite green: 132 Application + 124 Api = 256.

## 4. Slice C — Proof producer: Financial exposure on the metric (TDD)

- [ ] 4.1 (red) `FinancialAgentTests`: the total-exposure finding carries the exposure **amount** on
      `MetricValue` and the **currency** on `MetricUnit` (in addition to the summary text). Watch it fail.
- [ ] 4.2 (green) In `FinancialSkill`, stamp `MetricValue = exposure`, `MetricUnit = <currency>` on the
      total-exposure finding; keep the summary. Use the budget line's currency; leave `MetricUnit` null if
      absent (no fabricated currency).
- [ ] 4.3 Re-run the suite; green.

## 5. Verify + document

- [x] 5.1 Full backend suite green (132 Application + 124 Api = 256); `openspec validate --strict` passes;
      build clean (the suite builds).
- [x] 5.2 `docs/l3-data-quality-followups.md` updated — the Finding metric field is marked ✅ landed (#46),
      noting the L3 Age column + #48 recommendation can now consume it.
- [x] 5.3 Boundary held: only `Finding` (+ `Finding.Create`), `FindingConfiguration` + migration,
      `FindingFactory` metric overload, `BudgetLineRecord.Currency` + parser, and `FinancialSkill` (the proof
      producer) changed. No roll-up/view; no other producer (L3 Age, #48 recommendation) touched.
