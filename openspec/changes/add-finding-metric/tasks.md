> **TDD** — failing test first (red), minimal code (green), refactor; keep the full backend suite green
> before checking off a task. Baseline: 127 Application + 122 Api = 249. Implements shared ticket #46. This
> change is the **shape + persistence + one proof producer** only — the roll-ups/views and the other
> producers (L3 Age, #48 recommendation) are separate tickets that consume this field.

## 1. Baseline (no code)

- [ ] 1.1 Confirm the suite is green at the current baseline (Application + Api counts).
- [ ] 1.2 Re-read `Finding.cs`, `FindingConfiguration.cs`, and the `AddFindingAreaSeverity` migration as the
      precedent for a nullable, additive, string/scalar field + migration.

## 2. Slice A — Domain: optional metric on `Finding` (TDD)

- [ ] 2.1 (red) Domain test in `FindingTests` (or nearest): `Finding.Create` accepts an optional
      `MetricValue` (decimal?) + `MetricUnit` (string?) + `MetricDetail` (string→string map); a finding
      created without them has all three null and is valid; with them, they round-trip on the object.
- [ ] 2.2 (red) Invariant tests still hold with a metric present: `Kind == Analysis` still requires
      `Area` + `Severity`; a non-analysis finding still forces `Area`/`Severity` null; citation still
      mandatory — regardless of whether a metric is set.
- [ ] 2.3 (green) Add `MetricValue` / `MetricUnit` / `MetricDetail` (nullable) to `Finding` + optional
      params on `Finding.Create` (defaulting null). No invariant change.
- [ ] 2.4 Re-run the Application suite; green.

## 3. Slice B — Persistence + migration (EF)

- [ ] 3.1 (green) Map the fields in `FindingConfiguration`: `metric_value` (`numeric` null), `metric_unit`
      (`text` null), `metric_detail` (`jsonb` null) via a `Dictionary<string,string>` ⇄ JSON value
      converter **with a `ValueComparer`** (so EF change-tracking doesn't mis-detect the map).
- [ ] 3.2 (green) Add the migration: `dotnet ef migrations add AddFindingMetric --project
      source/AiPMOInsight.Infrastructure --startup-project source/AiPMOInsight.Api`. Commit the generated
      migration + `AppDbContextModelSnapshot` changes. Confirm three nullable columns, no backfill.
- [ ] 3.3 (red→green) Persistence round-trip test (integration, `TestWebAppFactory`/DbContext): a finding
      with `MetricValue` + `MetricUnit` + a populated `MetricDetail` saves and reads back intact; a finding
      saved with no metric reads back with all three null. (The populated-detail case guards the converter +
      comparer.)
- [ ] 3.4 Confirm Development auto-migrate still boots (the migration applies cleanly).
- [ ] 3.5 Re-run the suite; green.

## 4. Slice C — Proof producer: Financial exposure on the metric (TDD)

- [ ] 4.1 (red) `FinancialAgentTests`: the total-exposure finding carries the exposure **amount** on
      `MetricValue` and the **currency** on `MetricUnit` (in addition to the summary text). Watch it fail.
- [ ] 4.2 (green) In `FinancialSkill`, stamp `MetricValue = exposure`, `MetricUnit = <currency>` on the
      total-exposure finding; keep the summary. Use the budget line's currency; leave `MetricUnit` null if
      absent (no fabricated currency).
- [ ] 4.3 Re-run the suite; green.

## 5. Verify + document

- [ ] 5.1 Full backend suite green; `openspec validate add-finding-metric --strict` passes; `dotnet build`
      clean.
- [ ] 5.2 Note in `docs/l3-data-quality-followups.md` that the `Finding` metric field has landed (the Age
      column can now render from it in the L3 change); mark #46 done.
- [ ] 5.3 Confirm the boundary held: only the shape + persistence + the Financial proof producer changed;
      no roll-up/view, and no other producer (L3 Age, #48 recommendation) touched here.
