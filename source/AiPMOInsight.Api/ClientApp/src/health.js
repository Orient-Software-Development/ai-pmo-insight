// Pure helpers for the Level-2 health banner. No React, no I/O — kept independently importable so the
// health-endpoint response mapping and the RAG colour choice read as testable units even though this
// repo has no JS test runner (see the add-project-status-dashboard change, task 1.3).

// The four render states the project-status view distinguishes for GET /api/projects/{key}/health.
// SCORING_PENDING and NOT_SCORED are defined, non-error outcomes — never surfaced as errors.
export const HEALTH_STATE = {
  SCORED: 'SCORED', // 200 with a non-null score
  SCORING_PENDING: 'SCORING_PENDING', // 200 with a null score: findings exist, nothing scoreable yet
  NOT_SCORED: 'NOT_SCORED', // 404: no findings on record for the key
  ERROR: 'ERROR', // network / 5xx / 401 — surfaced via the page's error line, not a banner
};

// Total mapping of a health-endpoint response to a render state (design Decision 3).
// `status` is the HTTP status; `body` is the parsed JSON ({ projectKey, score }) or null/undefined.
export function healthState(status, body) {
  if (status === 200) {
    return body && body.score != null ? HEALTH_STATE.SCORED : HEALTH_STATE.SCORING_PENDING;
  }
  if (status === 404) {
    return HEALTH_STATE.NOT_SCORED;
  }
  return HEALTH_STATE.ERROR;
}

// RAG colour class for a FinalBucket value. Case-insensitive; anything unrecognised → neutral fallback,
// so a null/absent bucket never renders as a coloured (misleading) banner.
export function bucketColour(finalBucket) {
  switch (String(finalBucket).toLowerCase()) {
    case 'red':
      return 'rag-red';
    case 'amber':
      return 'rag-amber';
    case 'green':
      return 'rag-green';
    default:
      return 'rag-none';
  }
}
