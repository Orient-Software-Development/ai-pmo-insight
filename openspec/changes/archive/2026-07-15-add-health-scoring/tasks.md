> Implement test-first (red → green → refactor). Keep the suite green before checking off a task.
> Terminology: "RAG" = Red/Amber/Green health colour throughout, never retrieval-augmented generation.

## 1. Domain: Area + Severity on findings (TDD)

- [x] 1.1 Add `HealthArea` (Schedule, Budget, Risk, Resource, DataQuality — extendable) and `Severity` (Green, Amber, Red) enums under `source/AiPMOInsight.Domain/Findings/`.
- [x] 1.2 (red) Add `Finding` tests pinning: an `Analysis` finding carries non-null `Area` + `Severity`; a Narrative/Challenge/Review finding leaves both null; `Finding.Create` throws when an `Analysis` finding is created without `Area` or `Severity` (mirror the citation invariant).
- [x] 1.3 Add nullable `HealthArea? Area` and `Severity? Severity` to `Finding`; extend `Finding.Create` (or an overload) to accept them and enforce the `Kind==Analysis ⇒ non-null` assertion.
- [x] 1.4 (green) Domain tests pass.

## 2. Persistence for the new finding columns (TDD)

- [x] 2.1 (red) Repository/round-trip test: an `Analysis` finding saved with `Area`+`Severity` reads back with the same values; non-analysis findings read back null.
- [x] 2.2 Map the two columns in the EF `Finding` configuration; add an EF migration + `AppDbContextModelSnapshot` update. (Pre-live: DB may be reset rather than back-filled.)
- [x] 2.3 (green) Persistence round-trip test passes; existing findings tests still pass.

## 3. Agents emit Area + Severity (TDD)

- [x] 3.1 (red) Extend `StatusSkill`, `FinancialSkill`, `ResourceSkill`, `DataQualitySkill` tests: each `Analysis` finding they emit carries the expected `Area` and a `Severity` mapped from the band they already compute (e.g. Financial overrun >15% → Red); summaries and finding counts are unchanged (additive).
- [x] 3.2 Introduce a Severity-band helper per agent (reuse the existing threshold logic — e.g. `StatusSkill.Severity(days)`) and pass `Area`+`Severity` through `FindingFactory` when creating findings.
- [x] 3.3 (green) Agent tests pass; assert enrichment is additive (same N findings, same summaries).

## 4. Scoring configuration + validation (TDD)

- [x] 4.1 (red) Options tests: `HealthScoringOptions` binds weights, Severity→number mapping, RAG thresholds, override rules, confidence floor; startup validation fails when weights don't sum to the configured total or thresholds are out of order, naming the offending key.
- [x] 4.2 Add `HealthScoringOptions` + binder under `source/AiPMOInsight.Application/Features/HealthScoring/` (or Infrastructure for the file source); decide YAML (`YamlDotNet`) vs. appsettings JSON per design Open Questions and record the choice. **Decision: appsettings JSON** — no new dependency (YamlDotNet avoided), matching the existing `LlmOptions`/`JwtOptions` binding pattern.
- [x] 4.3 Ship a default config carrying the PRD **EXAMPLE** values, header-commented as a placeholder; wire a startup log line stating the config is the placeholder set until overridden.
- [x] 4.4 (green) Options + validation tests pass.

## 5. Scoring service: weighted score + bucketing (TDD)

- [x] 5.1 (red) `HealthScoringServiceTests`: weighted sum over per-area worst-severity yields the expected `rawScore`; bucketing maps score → RAG by configured thresholds (incl. a boundary case).
- [x] 5.2 Implement latest-run resolution **per project key** over `IFindingRepository` (exclude older `RunId`s; a run may span projects — resolve `(projectKey, max RunId)`).
- [x] 5.3 Implement grouping by `Area`, area-score reduction via the Severity→number mapping, weighted sum, and threshold bucketing → `rawScore` + `rawBucket`.
- [x] 5.4 (green) Scoring + bucketing tests pass.

## 6. Override engine + audit result (TDD)

- [x] 6.1 (red) Table-driven override tests: a single override raises the bucket above raw; colliding overrides resolve to the worst-case floor (min Red beats min Amber); a floor never lowers severity; an absent signal does not fire (no synthetic warning).
- [x] 6.2 (red) Confidence test: aggregate confidence below the floor yields a "Needs PM Review" status distinct from the RAG colour and taking precedence.
- [x] 6.3 (red) Audit-shape test: result carries `rawScore`, `rawBucket`, ordered `appliedOverrides` (rule + tripping finding/citation), `finalBucket`, aggregate `confidence`, and the per-area breakdown; an override-changed bucket shows both pre- and post-override values.
- [x] 6.4 Implement the override engine (worst-case floor precedence, deterministic order from config), the "Needs PM Review" rule over persisted `Confidence`, and the `HealthScore` result type.
- [x] 6.5 (green) All override, confidence, and audit tests pass.

## 7. Read endpoint (TDD)

- [x] 7.1 (red) Endpoint test: `GET` a project's health score returns the full audit result; unknown project → 404; a project with no findings → a defined empty/no-score response.
- [x] 7.2 Add a `ScoreProject` query handler + a read endpoint under `source/AiPMOInsight.Api/Endpoints/` (authorized, consistent with existing read surfaces).
- [x] 7.3 (green) Endpoint tests pass.

## 8. Docs + validate

- [x] 8.1 Add a `## Health scoring` note to `CLAUDE.md`: enrichment of findings, scoring as a re-runnable query over the latest run, worst-case-floor overrides, EXAMPLE-config posture.
- [x] 8.2 Flip the Phase 4 row in `docs/roadmap.md` and note the EXAMPLE numbers remain client-pending.
- [x] 8.3 Run `dotnet test` end-to-end; confirm existing pipeline/findings tests still pass alongside the new ones. (212 tests green: 100 Application + 112 Api.)
- [x] 8.4 Run `openspec validate add-health-scoring --strict`; fix any reported issue. (Reports "valid".)
- [x] 8.5 (Hand-off) In the PR, state clearly that the shipped weights/thresholds/overrides are EXAMPLE placeholders pending PMO kickoff, and that dashboard consumption is Phase 5. (Hand-off text prepared for the PR body — see implementation summary.)
