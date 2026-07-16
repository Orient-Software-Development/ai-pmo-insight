import { useEffect, useState } from 'react';
import { authFetch } from '../AuthContext';
import { bucketColour } from '../health';

// Level-1 Executive Portfolio Summary (add-executive-portfolio-dashboard), built to the v2 wireframe
// (docs/designs/phase5-wireframe-v2.html, data-page="l1"). Consumes GET /api/portfolio.
//
// Presentation-only boundary (same as the L2 view): panels the roll-up can back are populated from live
// data (portfolio health G/A/R, confidence + Needs-PM-Review count, projects needing intervention,
// financial exposure €, decision backlog, key-person concentration, and a labelled customer-exposure
// proxy). Panels the roll-up still cannot back (owned/dated recommendations at portfolio level, true
// commercial risk) render a clear "not yet captured — follow-on" state, never fabricated figures.
const EMPTY = {
  red: 0, amber: 0, green: 0, needsPmReview: 0, averageConfidence: 0, intervention: [],
  financialExposure: { amount: 0, currency: null }, decisionBacklog: 0, keyPersons: [], customerExposure: [],
};

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

        <div className="summary-cell">
          <div className="summary-label">Financial exposure</div>
          <div className="summary-value">{formatExposure(data.financialExposure)}</div>
          <div className="summary-sub">forecast over budget, summed across the portfolio</div>
        </div>

        <div className="summary-cell">
          <div className="summary-label">Decisions blocking</div>
          <div className="summary-value">{data.decisionBacklog}</div>
          <div className="summary-sub">overdue or due-soon decisions</div>
        </div>

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

      {/* ── Key-person concentration (BACKED) ───────────────────────────────────────────────── */}
      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Key-person concentration</h2>
          <span className="sec-kicker">People spread across many projects · worst first</span>
        </div>
        {data.keyPersons.length === 0 ? (
          <p><em>No key-person concentration flagged.</em></p>
        ) : (
          <table className="records">
            <thead>
              <tr><th>Person</th><th>Projects</th><th>Band</th></tr>
            </thead>
            <tbody>
              {data.keyPersons.map(k => (
                <tr key={k.person} className={`severity ${bucketColour(k.status)}`}>
                  <td><strong>{k.person}</strong></td>
                  <td><span className="conf">{k.projectCount}</span></td>
                  <td><span className={`sev ${bucketColour(k.status)}`}>{k.status}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <p className="flagged-note">Concentration only (project count). The × absence dimension of
          key-person risk isn’t yet captured in the data — a follow-on.</p>
      </section>

      {/* ── Customer exposure proxy (BACKED, LABELLED) ──────────────────────────────────────── */}
      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Customer exposure</h2>
          <span className="sec-kicker">At-risk projects grouped by customer · relationship exposure</span>
        </div>
        {data.customerExposure.length === 0 ? (
          <p><em>No customers with at-risk projects.</em></p>
        ) : (
          <table className="records">
            <thead>
              <tr><th>Customer</th><th>At-risk projects</th></tr>
            </thead>
            <tbody>
              {data.customerExposure.map(c => (
                <tr key={c.customer}>
                  <td><strong>{c.customer}</strong></td>
                  <td><span className="conf">{c.atRiskCount}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <p className="flagged-note"><strong>Relationship exposure</strong>, not true commercial risk:
          it counts at-risk projects per customer. Contract value / margin / SLA-penalty exposure isn’t in
          the data — a kick-off question, not fabricated here.</p>
      </section>

      {/* ── STILL FLAGGED: portfolio-level owned/dated recommendations ───────────────────────── */}
      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Recommended actions</h2>
          <span className="sec-kicker">Owner · Deadline · Confidence</span>
        </div>
        <FlaggedPanel title="Portfolio-level owned &amp; dated recommendations" wide />
      </section>
    </div>
  );
}

// Formats the portfolio exposure amount with its currency; "—" when nothing is over budget.
function formatExposure({ amount, currency }) {
  if (!amount) return '—';
  const n = Math.round(amount).toLocaleString();
  return currency ? `${currency} ${n}` : n;
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
