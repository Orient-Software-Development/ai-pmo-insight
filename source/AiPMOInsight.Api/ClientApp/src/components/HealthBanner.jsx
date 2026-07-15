import { HEALTH_STATE, bucketColour } from '../health';

// Level-2 RAG health banner + score audit. Renders one of four states (see ../health.js):
//  - SCORED          → coloured banner (FinalBucket + RawScore), per-area breakdown, confidence,
//                      applied-override trail, and the distinct "Needs PM Review" flag.
//  - SCORING_PENDING → neutral note: findings exist but nothing scoreable yet (no colour, not an error).
//  - NOT_SCORED      → neutral note: no findings on record for this key (no colour, not an error).
//  - ERROR           → nothing (the page's own error line reports fetch failures).
// Status is never colour-only: the FinalBucket label and the numeric score always accompany the colour.
export function HealthBanner({ state, score }) {
  if (state === HEALTH_STATE.ERROR) return null;

  if (state === HEALTH_STATE.SCORING_PENDING) {
    return (
      <section className="rag-banner rag-none" aria-label="Project health">
        <p style={{ margin: 0 }}>
          <strong>Scoring pending.</strong> This project has findings but nothing scoreable yet.
        </p>
      </section>
    );
  }

  if (state === HEALTH_STATE.NOT_SCORED) {
    return (
      <section className="rag-banner rag-none" aria-label="Project health">
        <p style={{ margin: 0 }}>
          <strong>No health score.</strong> No findings on record for this project.
        </p>
      </section>
    );
  }

  if (state !== HEALTH_STATE.SCORED || !score) return null;

  const colour = bucketColour(score.finalBucket);
  const rawScore = Math.round(score.rawScore);

  return (
    <section className={`rag-banner ${colour}`} aria-label="Project health">
      <div className="rag-head">
        <span className="rag-badge" data-bucket={score.finalBucket}>
          {score.finalBucket}
        </span>
        <span className="rag-score">score {rawScore}</span>
        <span className="rag-confidence">confidence {formatConfidence(score.confidence)}</span>
        {score.needsPmReview && (
          <span className="rag-review" title="Aggregate confidence is very low — needs a PM's judgement">
            ⚠ Needs PM Review
          </span>
        )}
      </div>

      <AreaBreakdown areas={score.areas} />
      <OverrideTrail overrides={score.appliedOverrides} />
    </section>
  );
}

// Per-area contribution: area, its worst severity, weight, and weighted contribution.
function AreaBreakdown({ areas }) {
  if (!areas || areas.length === 0) return null;
  return (
    <details open>
      <summary>Area breakdown ({areas.length})</summary>
      <table>
        <thead>
          <tr><th>Area</th><th>Severity</th><th>Weight</th><th>Contribution</th></tr>
        </thead>
        <tbody>
          {areas.map(a => (
            <tr key={a.area}>
              <td>{a.area}</td>
              <td><span className={`rag-chip ${bucketColour(a.severity)}`}>{a.severity}</span></td>
              <td>{a.weight}</td>
              <td>{round2(a.contribution)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </details>
  );
}

// Applied-override audit trail: each floor that fired, why, and the finding it cites. Empty → nothing.
function OverrideTrail({ overrides }) {
  if (!overrides || overrides.length === 0) return null;
  return (
    <details>
      <summary>Applied overrides ({overrides.length})</summary>
      <ul>
        {overrides.map((o, i) => (
          <li key={`${o.ruleId}-${i}`}>
            <strong>{o.ruleId}</strong> → minimum <span className={`rag-chip ${bucketColour(o.floor)}`}>{o.floor}</span>
            {' — '}{o.reason}
            {o.citationLocator && <> <small>(cites {o.citationLocator})</small></>}
          </li>
        ))}
      </ul>
    </details>
  );
}

function formatConfidence(c) {
  if (typeof c !== 'number') return '—';
  return c <= 1 ? `${Math.round(c * 100)}%` : String(round2(c));
}

const round2 = n => (typeof n === 'number' ? Math.round(n * 100) / 100 : n);
