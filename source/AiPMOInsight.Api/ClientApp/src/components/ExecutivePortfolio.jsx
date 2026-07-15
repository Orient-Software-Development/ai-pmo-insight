import { useEffect, useState } from 'react';
import { authFetch } from '../AuthContext';
import { bucketColour } from '../health';

// Level-1 Executive Portfolio Summary (add-executive-portfolio-dashboard), built to the v2 wireframe
// (docs/designs/phase5-wireframe-v2.html, data-page="l1"). Consumes GET /api/portfolio.
//
// Presentation-only boundary (same as the L2 view): panels the roll-up can back are populated from live
// data (portfolio health G/A/R, confidence + Needs-PM-Review count, projects needing intervention);
// panels that exceed the current finding shape (€ financial exposure, per-decision detail, key-person
// risk, owned/dated recommendations) render a clear "not yet captured — follow-on" state, never
// fabricated figures.
const EMPTY = { red: 0, amber: 0, green: 0, needsPmReview: 0, averageConfidence: 0, intervention: [] };

export function ExecutivePortfolio() {
  const [data, setData] = useState(EMPTY);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let live = true;
    (async () => {
      try {
        const res = await authFetch('/api/portfolio');
        if (!res.ok) throw new Error(`GET /api/portfolio failed (${res.status})`);
        const body = await res.json();
        if (live) setData({ ...EMPTY, ...body });
      } catch (e) {
        if (live) setError(e.message);
      } finally {
        if (live) setLoading(false);
      }
    })();
    return () => { live = false; };
  }, []);

  const total = data.red + data.amber + data.green;

  if (loading) return <p aria-busy="true">Loading portfolio…</p>;
  if (error) return <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>;

  return (
    <div>
      <p className="eyebrow"><span className="num">L1</span> Executive Portfolio Summary</p>
      <h1>The portfolio, at one glance.</h1>
      <p>Where leadership should spend its next hour — health across every project, the ones that need
        intervention now, and (once the data supports them) the actions the AI recommends.</p>

      {/* ── Summary strip: BACKED health + confidence, FLAGGED exposure + decisions ─────────── */}
      <div className="summary-strip">
        <div className="summary-cell total">
          <div className="summary-label">Portfolio health</div>
          <div className="summary-value">{total} <span>projects</span></div>
          <RagBar red={data.red} amber={data.amber} green={data.green} total={total} />
          <div className="rag-legend">
            <span><span className="dot rag-red" /> {data.red} red</span>
            <span><span className="dot rag-amber" /> {data.amber} amber</span>
            <span><span className="dot rag-green" /> {data.green} green</span>
          </div>
        </div>

        <FlaggedCell label="Financial exposure" note="€ exposure not yet captured — findings carry severity, not amounts (follow-on)." />

        <FlaggedCell label="Decisions blocking" note="Per-decision age/owner not yet captured (follow-on)." />

        <div className="summary-cell">
          <div className="summary-label">Confidence (avg)</div>
          <div className="summary-value">{Math.round(data.averageConfidence)}<span>%</span></div>
          <div className="summary-sub">
            {data.needsPmReview} project{data.needsPmReview === 1 ? '' : 's'} flagged “Needs PM Review”
          </div>
        </div>
      </div>

      {/* ── Projects needing intervention (BACKED) ──────────────────────────────────────────── */}
      <section className="block" id="l1-needs">
        <div className="sec-head">
          <h2 className="sec-title">Projects needing intervention</h2>
          <span className="sec-kicker">Red &amp; Amber · worst first</span>
        </div>
        <table className="records">
          <thead>
            <tr><th>Project</th><th>Reason</th><th>Confidence</th><th>Status</th></tr>
          </thead>
          <tbody>
            {data.intervention.length === 0 ? (
              <tr><td colSpan={4}><em>No projects currently need intervention.</em></td></tr>
            ) : (
              data.intervention.map(i => (
                <tr key={i.projectKey} className={`severity ${bucketColour(i.status)}`}>
                  <td><strong>{i.projectKey}</strong></td>
                  <td>{i.reason}<br /><span className="cite">↳ cites {i.citationLocator}</span></td>
                  <td><span className="conf">{Math.round(i.confidence)}%</span></td>
                  <td><span className={`sev ${bucketColour(i.status)}`}>{i.status}</span></td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </section>

      {/* ── FLAGGED sections: match v2 layout, no fabricated data ────────────────────────────── */}
      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Where the pressure is</h2>
          <span className="sec-kicker">Money · Decisions · People</span>
        </div>
        <div className="flagged-grid">
          <FlaggedPanel title="Financial exposure (M€)" />
          <FlaggedPanel title="Decision backlog (days open)" />
          <FlaggedPanel title="Key-person risk (alloc × absence)" />
        </div>
      </section>

      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Recommended actions</h2>
          <span className="sec-kicker">Owner · Deadline · Confidence</span>
        </div>
        <FlaggedPanel title="Owned &amp; dated recommendations" wide />
      </section>
    </div>
  );
}

// Segmented RAG bar; widths are proportions of the scored total (0 total → empty bar).
function RagBar({ red, amber, green, total }) {
  const pct = n => (total === 0 ? 0 : (n / total) * 100);
  return (
    <div className="rag-bar" role="img" aria-label={`${red} red, ${amber} amber, ${green} green`}>
      <span className="seg rag-red" style={{ width: `${pct(red)}%` }} />
      <span className="seg rag-amber" style={{ width: `${pct(amber)}%` }} />
      <span className="seg rag-green" style={{ width: `${pct(green)}%` }} />
    </div>
  );
}

// A summary-strip cell whose data the finding shape can't yet back.
function FlaggedCell({ label, note }) {
  return (
    <div className="summary-cell">
      <div className="summary-label">{label}</div>
      <div className="summary-value flagged">—</div>
      <div className="summary-sub flagged-note">{note}</div>
    </div>
  );
}

// A larger flagged placeholder panel matching a v2 slot the data can't back yet.
function FlaggedPanel({ title, wide }) {
  return (
    <div className={`flagged-panel${wide ? ' wide' : ''}`}>
      <div className="summary-label">{title}</div>
      <p className="flagged-note">
        Not yet captured in the finding shape — a Phase 5 follow-on. Shown here to reserve the layout, with
        no fabricated figures.
      </p>
    </div>
  );
}
