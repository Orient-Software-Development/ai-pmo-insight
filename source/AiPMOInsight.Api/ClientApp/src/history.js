// Pure helpers for the /history rich-detail view. No React, no I/O — kept independently importable so
// the response→viewmodel mapping reads as testable units even though this repo has no JS test runner
// (mirrors health.js / dataQuality.js). RAG colour + health-state mapping are reused from health.js.

export const UPLOAD_STATUS = { ANALYZED: 'Analyzed', NOT_ANALYZED: 'NotAnalyzed' };

// Coarse status for a loaded upload detail. Analysis is synchronous and there is no run-status entity,
// so only "has findings" (Analyzed) vs "known upload, no findings" (NotAnalyzed) is derivable — a live
// Running/Failed pill is a follow-on, not fabricated here.
export function uploadStatus(view) {
  return allFindings(view).length > 0 ? UPLOAD_STATUS.ANALYZED : UPLOAD_STATUS.NOT_ANALYZED;
}

// Run provenance derived from the findings already loaded: the run id, the run date, and the distinct
// prompt hashes across the run's LLM findings (deterministic agents carry no promptVersion). The LLM
// model id is intentionally absent — the findings response does not carry it (flagged in the view).
export function runProvenance(view) {
  const all = allFindings(view);
  if (all.length === 0) {
    return { runId: null, createdAt: null, promptHashes: [] };
  }
  const runId = all[0].runId ?? null;
  const dates = all.map(f => f.createdAt).filter(Boolean).sort();
  const promptHashes = [...new Set(all.map(f => f.promptVersion).filter(Boolean))];
  return { runId, createdAt: dates[0] ?? null, promptHashes };
}

// Distinct project keys present across the run's findings — drives the per-project score-audit fan-out.
export function projectKeys(view) {
  return [...new Set(allFindings(view).map(f => f.projectKey).filter(Boolean))];
}

function allFindings(view) {
  if (!view) {
    return [];
  }
  return [
    ...(view.findings ?? []),
    ...(view.narrative ?? []),
    ...(view.challenge ?? []),
    ...(view.review ?? []),
  ];
}
