// Pure helpers for the Level-3 Data Quality view. No React, no I/O — kept independently importable so
// the data-quality response mapping reads as a testable unit even though this repo has no JS test
// runner (mirrors health.js from the add-project-status-dashboard change). Severity → RAG chip class is
// reused from health.js's bucketColour.

// Safe empty shape for GET /api/data-quality/summary (before load, on error, or on an empty store).
export const EMPTY_DQ = {
  confidence: { mean: 0, threshold: 0, belowTarget: false },
  items: [],
  totalItems: 0,
  perProject: [],
};

// Total mapping of a data-quality-summary response body to a safe view model: missing/partial fields
// fall back to EMPTY_DQ defaults so the view never reads undefined. `body` is the parsed JSON or null.
export function dqView(body) {
  if (!body) {
    return EMPTY_DQ;
  }
  return {
    confidence: { ...EMPTY_DQ.confidence, ...(body.confidence ?? {}) },
    items: body.items ?? [],
    totalItems: body.totalItems ?? 0,
    perProject: body.perProject ?? [],
  };
}
