import { useEffect, useState } from 'react';
import { authFetch } from '../AuthContext';
import { bucketColour } from '../health';
import { EMPTY_DQ, dqView } from '../dataQuality';

// The 8 input categories for the areas-completeness grid (L3 #7) — NOT the 5 HealthArea buckets.
const COMPLETENESS_CATEGORIES = ['Schedule', 'Budget', 'Scope', 'Resources', 'Risks', 'Decisions', 'Minutes', 'Time'];

// Level-3 Data Quality view (add-data-quality-dashboard), built to the v2 wireframe
// (docs/designs/phase5-wireframe-v2.html, data-page="l3"). Consumes GET /api/data-quality/summary.
//
// Presentation-only boundary (same as L1/L2): backed from live data — the confidence hero, the
// missing/inconsistent items table (now incl. Age + Suggested-remediation, #69 items 8/2), and the
// duplicate-identity candidates table with a Merge/Keep-separate control that only RECORDS the choice
// and never auto-merges (US-2, #69 item 4; POC heuristic + client-side record). Still not backed —
// ordering by a quantified confidence "lift" and the eight-category areas-completeness grid — render a
// dashed "not yet captured — follow-on" state, never fabricated values.
export function DataQuality() {
  const [data, setData] = useState(EMPTY_DQ);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  // US-2: the human's Merge/Keep-separate choice is recorded here (client-side, POC) and NEVER auto-merges.
  const [dupDecisions, setDupDecisions] = useState({});

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

  const { confidence, items, duplicates, completeness } = data;
  const mean = Math.round(confidence.mean);
  const below = confidence.belowTarget;

  return (
    <div>
      <p className="eyebrow"><span className="num">L3</span> Data Quality</p>
      <h1>What the AI is missing.</h1>
      <p>The specific inputs that would lift confidence — every item cited to its source. Ranked by
        confidence lift; fixing the top items first raises the portfolio confidence number above.</p>

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
          <span className="sec-kicker">Ranked by confidence lift · {data.totalItems} item{data.totalItems === 1 ? '' : 's'}</span>
        </div>
        <table className="records" aria-label="Data quality items">
          <thead>
            <tr><th>Project</th><th>Issue</th><th>Age</th><th>Suggested remediation</th><th>Conf. lift</th><th>Severity</th></tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr><td colSpan={6}><em>No data-quality issues on record.</em></td></tr>
            ) : (
              items.map((i, idx) => (
                <tr key={`${i.projectKey}-${idx}`} className={`severity ${bucketColour(i.severity)}`}>
                  <td><strong>{i.projectKey}</strong></td>
                  <td>{i.issue}<br /><span className="cite">↳ cites {i.citationLocator}</span></td>
                  <td>{i.ageDays != null ? `${i.ageDays}d` : '—'}</td>
                  <td>{i.remediation ?? '—'}</td>
                  <td>{i.lift > 0 ? `+${i.lift}` : '—'}</td>
                  <td><span className={`sev ${bucketColour(i.severity)}`}>{i.severity}</span></td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <p className="flagged-note">
          <strong>Age</strong> (staleness, days), <strong>suggested remediation</strong>, and the
          <strong> confidence-lift ranking</strong> (how many levels fixing each item would raise the
          project's confidence — <strong>POC</strong>) are backed by the finding metric. Nothing is fabricated.
        </p>
      </section>

      {/* ── Areas-completeness grid (BACKED, POC field set) ──────────────────────────────────── */}
      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Completeness by area</h2>
          <span className="sec-kicker">% of mandatory fields present · POC field set</span>
        </div>
        {completeness.length === 0 ? (
          <p><em>No completeness data.</em></p>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="records" aria-label="Areas completeness by category">
              <thead>
                <tr><th>Project</th>{COMPLETENESS_CATEGORIES.map(c => <th key={c}>{c}</th>)}</tr>
              </thead>
              <tbody>
                {completeness.map(row => (
                  <tr key={row.projectKey}>
                    <td><strong>{row.projectKey}</strong></td>
                    {COMPLETENESS_CATEGORIES.map(c => {
                      const v = row.categories?.[c];
                      return <td key={c}>{v == null || v === 'n/a' ? '—' : `${v}%`}</td>;
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <p className="flagged-note">Completeness = % of each category's records that have all their
          <strong> POC mandatory fields</strong> (kickoff-tunable). "—" = no records (Time has no data source
          yet — L3 #3). Informational only — <strong>not scored</strong>.</p>
      </section>

      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Duplicate identity candidates</h2>
          <span className="sec-kicker">Confirmation required — no silent merges (US-2)</span>
        </div>
        {duplicates.length === 0 ? (
          <p><em>No duplicate candidates detected.</em></p>
        ) : (
          <table className="records" aria-label="Duplicate identity candidates">
            <thead>
              <tr><th>Project</th><th>Possible duplicate</th><th>Similarity</th><th>Decision</th></tr>
            </thead>
            <tbody>
              {duplicates.map(d => {
                const key = `${d.projectKey}::${d.candidate}`;
                const decision = dupDecisions[key];
                return (
                  <tr key={key}>
                    <td><strong>{d.projectKey}</strong><br /><span className="cite">↳ cites {d.citationLocator}</span></td>
                    <td><strong>{d.candidate}</strong>{d.candidateName ? ` — ${d.candidateName}` : ''}</td>
                    <td><span className="conf">{d.score}%</span></td>
                    <td>
                      {decision ? (
                        <>
                          <span className={`sev ${decision === 'merge' ? 'rag-amber' : 'rag-green'}`}>
                            {decision === 'merge' ? 'Merge requested' : 'Kept separate'}
                          </span>{' '}
                          <button type="button" onClick={() =>
                            setDupDecisions(s => { const n = { ...s }; delete n[key]; return n; })}>change</button>
                        </>
                      ) : (
                        <span className="dup-actions">
                          <button type="button" onClick={() => setDupDecisions(s => ({ ...s, [key]: 'merge' }))}>Request merge</button>
                          <button type="button" onClick={() => setDupDecisions(s => ({ ...s, [key]: 'keep' }))}>Keep separate</button>
                        </span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
        <p className="flagged-note">
          Heuristic candidates (<strong>POC</strong> score: name similarity + same customer +
          shared-resource — WBS overlap is a follow-on). Merge / Keep-separate is <strong>recorded here
          only (not yet persisted)</strong> and <strong>never auto-merges</strong> — the human decides (US-2).
        </p>
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
