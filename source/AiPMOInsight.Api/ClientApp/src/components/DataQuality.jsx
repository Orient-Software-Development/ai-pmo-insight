import { useEffect, useState } from 'react';
import { authFetch } from '../AuthContext';
import { bucketColour } from '../health';
import { EMPTY_DQ, dqView } from '../dataQuality';

// Level-3 Data Quality view (add-data-quality-dashboard), built to the v2 wireframe
// (docs/designs/phase5-wireframe-v2.html, data-page="l3"). Consumes GET /api/data-quality/summary.
//
// Presentation-only boundary (same as L1/L2): panels the roll-up can back are populated from live data
// (the confidence hero — mean confidence, publish threshold, below-target flag; and the
// missing/inconsistent items table — project, issue, severity, citation, worst-first). Panels the
// current DataQuality finding shape cannot back — a per-item Age column, a Suggested-remediation column,
// ordering by a quantified confidence "lift", the eight-category areas-completeness grid, and the
// duplicate-identity candidates table — render a dashed "not yet captured — follow-on" state, never
// fabricated values. No merge / keep-separate control is shipped while no duplicate signal exists (US-2).
export function DataQuality() {
  const [data, setData] = useState(EMPTY_DQ);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let live = true;
    (async () => {
      try {
        const res = await authFetch('/api/data-quality/summary');
        if (!res.ok) throw new Error(`GET /api/data-quality/summary failed (${res.status})`);
        const body = await res.json();
        if (live) setData(dqView(body));
      } catch (e) {
        if (live) setError(e.message);
      } finally {
        if (live) setLoading(false);
      }
    })();
    return () => { live = false; };
  }, []);

  if (loading) return <p aria-busy="true">Loading data quality…</p>;
  if (error) return <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>;

  const { confidence, items } = data;
  const mean = Math.round(confidence.mean);
  const below = confidence.belowTarget;

  return (
    <div>
      <p className="eyebrow"><span className="num">L3</span> Data Quality</p>
      <h1>What the AI is missing.</h1>
      <p>The specific inputs that would lift confidence — every item cited to its source. Ordered by
        severity today; the confidence-lift ranking, remediation text, and duplicate detection are
        follow-ons flagged below.</p>

      {/* ── Confidence hero (BACKED: mean, threshold, below-target) ──────────────────────────── */}
      <div className="conf-hero">
        <div>
          <div className="summary-label">Portfolio data confidence</div>
          <div className={`conf-num${below ? ' below' : ''}`}>{mean}<sup>%</sup></div>
          <div className="summary-sub">
            threshold to publish · <strong>{confidence.threshold}%</strong> ·{' '}
            <span className={`sev ${below ? 'rag-amber' : 'rag-green'}`}>
              {below ? 'below target' : 'on target'}
            </span>
          </div>
        </div>
        <p className="conf-lede">
          Mean of each scored project’s aggregate confidence, against the configured publish threshold
          (the same floor that drives “Needs PM Review”). Raising the items below lifts this number.
        </p>
      </div>

      {/* ── Missing and inconsistent items (BACKED: project, issue, severity, citation) ──────── */}
      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Missing and inconsistent items</h2>
          <span className="sec-kicker">Worst first · severity · {data.totalItems} item{data.totalItems === 1 ? '' : 's'}</span>
        </div>
        <table className="records" aria-label="Data quality items">
          <thead>
            <tr><th>Project</th><th>Issue</th><th>Severity</th></tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr><td colSpan={3}><em>No data-quality issues on record.</em></td></tr>
            ) : (
              items.map((i, idx) => (
                <tr key={`${i.projectKey}-${idx}`} className={`severity ${bucketColour(i.severity)}`}>
                  <td><strong>{i.projectKey}</strong></td>
                  <td>{i.issue}<br /><span className="cite">↳ cites {i.citationLocator}</span></td>
                  <td><span className={`sev ${bucketColour(i.severity)}`}>{i.severity}</span></td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <p className="flagged-note">
          Age, suggested-remediation, and the confidence-lift ranking columns from the wireframe are not
          yet captured in the finding shape — a Phase 5 follow-on. Shown ordered by severity, with no
          fabricated values.
        </p>
      </section>

      {/* ── FLAGGED sections: match v2 layout, no fabricated data ────────────────────────────── */}
      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Completeness by area</h2>
          <span className="sec-kicker">Schedule · Budget · Scope · Resources · Risks · Decisions · Minutes · Time</span>
        </div>
        <div className="flagged-grid">
          <FlaggedPanel title="Per-area completeness (8 categories)" wide />
        </div>
      </section>

      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Duplicate identity candidates</h2>
          <span className="sec-kicker">Confirmation required — no silent merges (US-2)</span>
        </div>
        <FlaggedPanel
          title="Duplicate candidates + Merge / Keep-separate"
          note="The Data Quality agent emits no duplicate-identity signal yet, so no candidates are shown and
            no merge / keep-separate action is offered — never a silent merge. A Phase 5 follow-on."
          wide
        />
      </section>
    </div>
  );
}

// A dashed placeholder matching a v2 slot the current finding shape can't back yet.
function FlaggedPanel({ title, note, wide }) {
  return (
    <div className={`flagged-panel${wide ? ' wide' : ''}`}>
      <div className="summary-label">{title}</div>
      <p className="flagged-note">
        {note ?? 'Not yet captured in the finding shape — a Phase 5 follow-on. Shown here to reserve the ' +
          'layout, with no fabricated figures.'}
      </p>
    </div>
  );
}
